using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;
using CometBFT.Client.TestInfrastructure;
using Xunit;

namespace CometBFT.Client.Fixture.Tests;

public sealed class CapturedFixtureJourneyTests
{
    [Fact]
    [Trait("Category", "Fixture")]
    public async Task Rest_CapturedPayloads_MapToDomainTypes()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));
        server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(JsonResponse(ReadFixture("RestFixtures/rest_status.json")));
        server
            .Given(Request.Create().WithPath("/block").UsingGet())
            .RespondWith(JsonResponse(ReadFixture("RestFixtures/rest_block.json")));
        server
            .Given(Request.Create().WithPath("/block_results").UsingGet())
            .RespondWith(JsonResponse(ReadFixture("RestFixtures/rest_block_results.json")));
        server
            .Given(Request.Create().WithPath("/header").UsingGet())
            .RespondWith(JsonResponse(ReadFixture("RestFixtures/rest_header.json")));
        server
            .Given(Request.Create().WithPath("/validators").UsingGet())
            .RespondWith(JsonResponse(ReadFixture("RestFixtures/rest_validators.json")));
        server
            .Given(Request.Create().WithPath("/blockchain").UsingGet())
            .RespondWith(JsonResponse(ReadFixture("RestFixtures/rest_blockchain.json")));
        server
            .Given(Request.Create().WithPath("/unconfirmed_txs").UsingGet())
            .RespondWith(JsonResponse(ReadFixture("RestFixtures/rest_unconfirmed_txs.json")));

        var services = new ServiceCollection();
        services.AddCometBftRest(options =>
        {
            options.BaseUrl = server.Url!;
            options.Timeout = TimeSpan.FromSeconds(5);
            options.MaxRetryAttempts = 0;
            options.RetryDelay = TimeSpan.Zero;
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftRestClient>();

        Assert.True(await client.GetHealthAsync());

        var (nodeInfo, syncInfo) = await client.GetStatusAsync();
        Assert.NotEmpty(nodeInfo.Network);
        Assert.True(syncInfo.LatestBlockHeight > 0);

        var block = await client.GetBlockAsync();
        Assert.True(block.Height > 0);
        Assert.NotEmpty(block.Hash);

        var results = await client.GetBlockResultsAsync();
        Assert.NotNull(results);

        var header = await client.GetHeaderAsync();
        Assert.True(header.Height > 0);

        var validators = await client.GetValidatorsAsync();
        Assert.NotEmpty(validators);

        var chain = await client.GetBlockchainAsync();
        Assert.True(chain.LastHeight > 0);

        var mempool = await client.GetUnconfirmedTxsAsync();
        Assert.True(mempool.Total >= 0);
    }

    [Fact]
    [Trait("Category", "Fixture")]
    public async Task WebSocket_CapturedPayloads_RaiseTypedEvents()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options =>
        {
            options.BaseUrl = server.Url;
            options.SubscribeAckTimeout = TimeSpan.FromSeconds(2);
            options.ReconnectTimeout = TimeSpan.FromSeconds(1);
            options.ErrorReconnectTimeout = TimeSpan.FromSeconds(1);
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        var blockReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var txReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var voteReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.NewBlockReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                blockReceived.TrySetResult(true);
            }
        };
        client.TxExecuted += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                txReceived.TrySetResult(true);
            }
        };
        client.VoteReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                voteReceived.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await Task.WhenAll(
            client.SubscribeNewBlockAsync(),
            client.SubscribeTxAsync(),
            client.SubscribeVoteAsync());

        await server.PushAsync(ReadFixture("WebSocketFixtures/new_block_with_txs.json"));
        await server.PushAsync(ReadFixture("WebSocketFixtures/tx_event.json"));
        await server.PushAsync(ReadFixture("WebSocketFixtures/vote_event.json"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(
            blockReceived.Task.WaitAsync(timeout.Token),
            txReceived.Task.WaitAsync(timeout.Token),
            voteReceived.Task.WaitAsync(timeout.Token));

        await client.DisconnectAsync();
    }

    private static string ReadFixture(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path);
    }

    private static IResponseBuilder JsonResponse(string body) =>
        Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(body);
}
