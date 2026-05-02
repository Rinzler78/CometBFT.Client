using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CometBFT.Client.Integration.Tests;

public sealed class IntegrationTests(LocalCometBftNodeFixture fixture) : IClassFixture<LocalCometBftNodeFixture>
{
    [Fact]
    [Trait("Category", "IntegrationLocal")]
    public async Task Rest_Journey_QueriesCoreEndpoints_And_BroadcastsTx()
    {
        if (fixture.SkipReason is not null) Assert.Skip(fixture.SkipReason);
        await using var provider = fixture.CreateRestProvider();
        var client = provider.GetRequiredService<ICometBftRestClient>();

        Assert.True(await client.GetHealthAsync());

        var (nodeInfo, syncInfo) = await client.GetStatusAsync();
        Assert.NotEmpty(nodeInfo.Network);
        Assert.True(syncInfo.LatestBlockHeight > 0);

        var block = await client.GetBlockAsync();
        Assert.True(block.Height > 0);

        var header = await client.GetHeaderAsync();
        Assert.True(header.Height > 0);

        var validators = await client.GetValidatorsAsync();
        Assert.NotEmpty(validators);

        var chain = await client.GetBlockchainAsync();
        Assert.True(chain.LastHeight > 0);

        // kvstore requires "key=value" format; bare strings are rejected (code 2).
        var tx = TxFactory.ToBase64(TxFactory.FromString($"integration={Guid.NewGuid():N}"));
        var result = await client.BroadcastTxSyncAsync(tx);
        Assert.Equal(0u, result.Code);
    }

    [Fact]
    [Trait("Category", "IntegrationLocal")]
    public async Task Grpc_Journey_Pings_And_BroadcastsTx()
    {
        if (fixture.SkipReason is not null) Assert.Skip(fixture.SkipReason);
        if (string.IsNullOrWhiteSpace(fixture.GrpcUrl))
        {
            Assert.Skip("Local CometBFT image does not expose gRPC on this environment.");
        }

        await using var provider = fixture.CreateGrpcProvider();
        var client = provider.GetRequiredService<ICometBftGrpcClient>();

        Assert.True(await client.PingAsync());

        var result = await client.BroadcastTxAsync(System.Text.Encoding.UTF8.GetBytes($"grpc-{Guid.NewGuid():N}"));
        Assert.NotEmpty(result.Hash);
        Assert.True(result.GasWanted >= 0);
        Assert.True(result.GasUsed >= 0);
    }

    [Fact]
    [Trait("Category", "IntegrationLocal")]
    public async Task WebSocket_Journey_ReceivesBlockAndHeaderEvents()
    {
        if (fixture.SkipReason is not null) Assert.Skip(fixture.SkipReason);
        await using var provider = fixture.CreateWebSocketProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        var blockReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var headerReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.NewBlockReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                blockReceived.TrySetResult(true);
            }
        };

        client.NewBlockHeaderReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                headerReceived.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await Task.WhenAll(client.SubscribeNewBlockAsync(), client.SubscribeNewBlockHeaderAsync());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.WhenAll(
            blockReceived.Task.WaitAsync(timeout.Token),
            headerReceived.Task.WaitAsync(timeout.Token));

        await client.UnsubscribeAllAsync();
        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "IntegrationLocal")]
    public async Task WebSocket_Journey_ReceivesVoteAndTxEvents()
    {
        if (fixture.SkipReason is not null) Assert.Skip(fixture.SkipReason);
        await using var wsProvider = fixture.CreateWebSocketProvider();
        await using var restProvider = fixture.CreateRestProvider();
        var wsClient = wsProvider.GetRequiredService<ICometBftWebSocketClient>();
        var restClient = restProvider.GetRequiredService<ICometBftRestClient>();

        var voteReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var txReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        wsClient.VoteReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                voteReceived.TrySetResult(true);
            }
        };

        wsClient.TxExecuted += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                txReceived.TrySetResult(true);
            }
        };

        await wsClient.ConnectAsync();
        await Task.WhenAll(wsClient.SubscribeVoteAsync(), wsClient.SubscribeTxAsync());

        // kvstore requires "key=value" format; bare strings are rejected (code 2) and never committed.
        var tx = TxFactory.ToBase64(TxFactory.FromString($"ws={Guid.NewGuid():N}"));
        var result = await restClient.BroadcastTxSyncAsync(tx);
        Assert.Equal(0u, result.Code);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await Task.WhenAll(
            voteReceived.Task.WaitAsync(timeout.Token),
            txReceived.Task.WaitAsync(timeout.Token));

        await wsClient.UnsubscribeAllAsync();
        await wsClient.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "IntegrationLocal")]
    public async Task WebSocket_Journey_SubscribeValidatorSetUpdates_DoesNotThrow()
    {
        if (fixture.SkipReason is not null) Assert.Skip(fixture.SkipReason);
        await using var provider = fixture.CreateWebSocketProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();
        await client.ConnectAsync();

        var ex = await Record.ExceptionAsync(() => client.SubscribeValidatorSetUpdatesAsync());
        Assert.Null(ex);

        await client.UnsubscribeAllAsync();
        await client.DisconnectAsync();
    }
}
