using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;
using Xunit;

namespace CometBFT.Client.E2E.Tests;

/// <summary>
/// End-to-end tests against public CometBFT testnet endpoints.
/// Tests are skipped automatically when the corresponding endpoint variable is absent.
/// </summary>
public sealed class E2eTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task Rest_Flow_FromDi_ToDomainObjects_Works()
    {
        var rpcUrl = EndpointConfiguration.Require("COMETBFT_RPC_URL");

        var services = new ServiceCollection();
        services.AddCometBftRest(options => options.BaseUrl = rpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftRestClient>();

        Assert.True(await client.GetHealthAsync());

        var (nodeInfo, syncInfo) = await client.GetStatusAsync();
        var block = await client.GetBlockAsync();
        var validators = await client.GetValidatorsAsync();

        Assert.NotEmpty(nodeInfo.Network);
        Assert.True(syncInfo.LatestBlockHeight > 0);
        Assert.True(block.Height > 0);
        Assert.NotEmpty(validators);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Rest_Flow_Extended_Endpoints_Work()
    {
        var rpcUrl = EndpointConfiguration.Require("COMETBFT_RPC_URL");

        var services = new ServiceCollection();
        services.AddCometBftRest(options => options.BaseUrl = rpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftRestClient>();

        // Block results
        var results = await client.GetBlockResultsAsync();
        Assert.NotNull(results);

        // Header
        var header = await client.GetHeaderAsync();
        Assert.True(header.Height > 0);

        // Blockchain range
        var chain = await client.GetBlockchainAsync();
        Assert.True(chain.LastHeight > 0);

        // Unconfirmed txs
        var mempool = await client.GetUnconfirmedTxsAsync();
        Assert.True(mempool.Total >= 0);

        // ABCI info
        var abci = await client.GetAbciInfoAsync();
        Assert.NotNull(abci);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task WebSocket_Flow_Receives_Typed_Block_Event()
    {
        var wsUrl = EndpointConfiguration.Require("COMETBFT_WS_URL");

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = wsUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.NewBlockReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                completion.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await completion.Task.WaitAsync(timeout.Token);
        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task WebSocket_Flow_MultipleSubscriptions_ReceiveEvents_And_UnsubscribeAll()
    {
        var wsUrl = EndpointConfiguration.Require("COMETBFT_WS_URL");

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = wsUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        var blockCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var headerCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.NewBlockReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                blockCompletion.TrySetResult(true);
            }
        };

        client.NewBlockHeaderReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                headerCompletion.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();
        await client.SubscribeNewBlockHeaderAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await Task.WhenAll(
            blockCompletion.Task.WaitAsync(timeout.Token),
            headerCompletion.Task.WaitAsync(timeout.Token));

        // UnsubscribeAll should succeed after receiving events.
        await client.UnsubscribeAllAsync();
        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Grpc_Flow_Resolves_Client_And_Handles_Expected_Broadcast_Path()
    {
        var grpcUrl = EndpointConfiguration.Require("COMETBFT_GRPC_URL");

        var services = new ServiceCollection();
        services.AddCometBftGrpc(options => options.BaseUrl = grpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftGrpcClient>();

        _ = await client.PingAsync();

        var ex = await Record.ExceptionAsync(() => client.BroadcastTxAsync([]));
        Assert.NotNull(ex);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Rest_Unsafe_DialSeeds_CallsThrough()
    {
        var rpcUrl = EndpointConfiguration.Require("COMETBFT_UNSAFE_RPC_URL");

        var services = new ServiceCollection();
        services.AddCometBftRest(options => options.BaseUrl = rpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftRestClient>();

        // Both success and expected "unsafe disabled" REST error are valid outcomes.
        var ex = await Record.ExceptionAsync(() => client.DialSeedsAsync([]));
        Assert.True(ex is null or CometBFT.Client.Core.Exceptions.CometBftRestException);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Grpc_Flow_BroadcastTx_CheckTxFields_Populated()
    {
        var grpcUrl = EndpointConfiguration.Require("COMETBFT_GRPC_URL");

        var services = new ServiceCollection();
        services.AddCometBftGrpc(options => options.BaseUrl = grpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftGrpcClient>();

        // Verify ping works before attempting broadcast.
        var alive = await client.PingAsync();
        Assert.True(alive);

        // Send a minimal invalid tx. The node should either:
        //   (a) Return a BroadcastTxResult with Code != 0 and populated check_tx fields, or
        //   (b) Throw a CometBftGrpcException at transport level.
        BroadcastTxResult? result = null;
        Exception? broadcastEx = null;
        try
        {
            result = await client.BroadcastTxAsync(new byte[] { 0x0a, 0x01, 0x00 });
        }
        catch (Exception ex)
        {
            broadcastEx = ex;
        }

        if (result is not null)
        {
            // check_tx shape must be complete regardless of ABCI code.
            Assert.NotEmpty(result.Hash);
            Assert.True(result.GasWanted >= 0, "GasWanted must be non-negative");
            Assert.True(result.GasUsed >= 0, "GasUsed must be non-negative");
        }
        else
        {
            // Transport-level failure is acceptable on a public testnet.
            Assert.IsType<CometBFT.Client.Core.Exceptions.CometBftGrpcException>(broadcastEx);
        }
    }
}

internal static class EndpointConfiguration
{
    public static string Require(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Xunit.Sdk.XunitException($"{Xunit.v3.DynamicSkipToken.Value}Set {variableName} to enable this test.");
        }

        return value;
    }
}
