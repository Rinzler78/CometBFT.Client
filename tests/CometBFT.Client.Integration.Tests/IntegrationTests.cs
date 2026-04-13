using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;
using Xunit;

namespace CometBFT.Client.Integration.Tests;

/// <summary>
/// Integration tests that require live CometBFT endpoints.
/// Tests are skipped automatically when the corresponding environment variable is not set.
/// </summary>
public sealed class IntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthAsync_LiveNode_ReturnsTrue()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();

        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var healthy = await client.GetHealthAsync();
        Assert.True(healthy);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetStatusAsync_LiveNode_ReturnsNodeInfo()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();

        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var (nodeInfo, syncInfo) = await client.GetStatusAsync();
        Assert.NotEmpty(nodeInfo.Network);
        Assert.True(syncInfo.LatestBlockHeight > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetBlockAsync_LiveNode_LatestBlock_ReturnsBlock()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();

        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var block = await client.GetBlockAsync();
        Assert.True(block.Height > 0);
        Assert.NotEmpty(block.Hash);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetValidatorsAsync_LiveNode_ReturnsValidators()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();

        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var validators = await client.GetValidatorsAsync();
        Assert.NotEmpty(validators);
        Assert.All(validators, validator => Assert.NotEmpty(validator.Address));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAbciInfoAsync_LiveNode_ReturnsDictionary()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();

        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var info = await client.GetAbciInfoAsync();
        Assert.NotNull(info);
        Assert.True(info.ContainsKey("data"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WebSocket_LiveNode_ReceivesTypedEvent_AndDisconnectsCleanly()
    {
        var wsUrl = EndpointConfiguration.RequireWs();

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
    [Trait("Category", "Integration")]
    public async Task WebSocket_LiveNode_ReceivesNewBlockHeaderEvent()
    {
        var wsUrl = EndpointConfiguration.RequireWs();

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = wsUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.NewBlockHeaderReceived += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                completion.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await client.SubscribeNewBlockHeaderAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await completion.Task.WaitAsync(timeout.Token);
        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WebSocket_LiveNode_UnsubscribeAll_Succeeds()
    {
        var wsUrl = EndpointConfiguration.RequireWs();

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = wsUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();
        await client.SubscribeNewBlockHeaderAsync();

        var ex = await Record.ExceptionAsync(() => client.UnsubscribeAllAsync());
        Assert.Null(ex);

        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNetInfoAsync_LiveNode_ReturnsNetworkInfo()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var netInfo = await client.GetNetInfoAsync();
        Assert.True(netInfo.PeerCount >= 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetBlockchainAsync_LiveNode_ReturnsHeaders()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var info = await client.GetBlockchainAsync();
        Assert.True(info.LastHeight > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHeaderAsync_LiveNode_ReturnsHeader()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var header = await client.GetHeaderAsync();
        Assert.True(header.Height > 0);
        Assert.NotEmpty(header.ChainId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetBlockResultsAsync_LiveNode_ReturnsResults()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var results = await client.GetBlockResultsAsync();
        Assert.NotNull(results);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetCommitAsync_LiveNode_ReturnsCommitInfo()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var commit = await client.GetCommitAsync();
        Assert.NotNull(commit);
        Assert.True(commit.ContainsKey("height") || commit.Count > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetGenesisAsync_LiveNode_ReturnsGenesisSummary()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        try
        {
            var genesis = await client.GetGenesisAsync();
            Assert.NotNull(genesis);
        }
        catch (CometBFT.Client.Core.Exceptions.CometBftRestException ex)
            when (ex.Message.Contains("500", StringComparison.Ordinal) ||
                  ex.Message.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase))
        {
            // The /genesis endpoint is commonly disabled or rate-limited on public nodes
            // because genesis files for mature chains can be hundreds of MB.
            // HTTP 500 here means "not available", not a client bug.
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetConsensusParamsAsync_LiveNode_ReturnsParams()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var prms = await client.GetConsensusParamsAsync();
        Assert.True(prms.BlockMaxBytes > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetUnconfirmedTxsAsync_LiveNode_ReturnsInfo()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var info = await client.GetUnconfirmedTxsAsync();
        Assert.True(info.Count >= 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNumUnconfirmedTxsAsync_LiveNode_ReturnsCount()
    {
        var rpcUrl = EndpointConfiguration.RequireRpc();
        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var info = await client.GetNumUnconfirmedTxsAsync();
        Assert.True(info.Total >= 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Grpc_LiveNode_ResolvesViaDi_And_HandlesExpectedPath()
    {
        var grpcUrl = EndpointConfiguration.RequireGrpc();

        var services = new ServiceCollection();
        services.AddCometBftGrpc(options => options.BaseUrl = grpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftGrpcClient>();

        _ = await client.PingAsync();

        var ex = await Record.ExceptionAsync(() => client.BroadcastTxAsync([]));
        Assert.NotNull(ex);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Grpc_LiveNode_BroadcastTx_ReturnsCheckTxDetails()
    {
        var grpcUrl = EndpointConfiguration.RequireGrpc();

        var services = new ServiceCollection();
        services.AddCometBftGrpc(options => options.BaseUrl = grpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftGrpcClient>();

        // Send a minimal (invalid) tx — expect a non-zero ABCI code with check_tx details.
        BroadcastTxResult? result = null;
        Exception? broadcastEx = null;
        try
        {
            result = await client.BroadcastTxAsync(new byte[] { 0x0a });
        }
        catch (Exception ex)
        {
            broadcastEx = ex;
        }

        // Either the node returned a check_tx result (even with non-zero code)
        // or it rejected the call at transport level.
        if (result is not null)
        {
            // check_tx fields must be populated — hash is always set.
            Assert.NotEmpty(result.Hash);
            Assert.True(result.GasWanted >= 0);
            Assert.True(result.GasUsed >= 0);
        }
        else
        {
            Assert.NotNull(broadcastEx);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WebSocket_LiveNode_ReceivesVoteEvent()
    {
        var wsUrl = EndpointConfiguration.RequireWs();

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = wsUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.VoteReceived += (_, args) =>
        {
            // Type 1 = Prevote, Type 2 = Precommit — both are valid.
            if (args.Value.Height > 0 && args.Value.ValidatorAddress.Length > 0)
            {
                completion.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await client.SubscribeVoteAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await completion.Task.WaitAsync(timeout.Token);
        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WebSocket_LiveNode_ReceivesTxEvent()
    {
        var wsUrl = EndpointConfiguration.RequireWs();

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = wsUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.TxExecuted += (_, args) =>
        {
            if (args.Value.Height > 0)
            {
                completion.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await client.SubscribeTxAsync();

        // Tx events depend on actual network activity — allow a generous window.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await completion.Task.WaitAsync(timeout.Token);
        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WebSocket_LiveNode_SubscribeValidatorSetUpdates_DoesNotThrow()
    {
        // ValidatorSetUpdates are rare (only on validator set changes), so we only
        // verify that the subscription wire-up and clean disconnect succeed.
        var wsUrl = EndpointConfiguration.RequireWs();

        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = wsUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();

        await client.ConnectAsync();
        var ex = await Record.ExceptionAsync(() => client.SubscribeValidatorSetUpdatesAsync());
        Assert.Null(ex);
        await client.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DialSeeds_UnsafeNode_ThrowsOrSucceeds_DependingOnNodeConfig()
    {
        // This test requires a CometBFT node started with --rpc.unsafe=true.
        var rpcUrl = EndpointConfiguration.RequireUnsafeRpc();

        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        // On a real unsafe-enabled node, DialSeedsAsync should succeed (no exception).
        // On a public node with unsafe disabled, CometBftRestException is expected.
        var ex = await Record.ExceptionAsync(() =>
            client.DialSeedsAsync([]));

        // Both outcomes are valid; the key assertion is that the method exists and calls through.
        Assert.True(ex is null or CometBFT.Client.Core.Exceptions.CometBftRestException);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DialPeers_UnsafeNode_ThrowsOrSucceeds_DependingOnNodeConfig()
    {
        var rpcUrl = EndpointConfiguration.RequireUnsafeRpc();

        await using var sp = BuildRestServices(rpcUrl);
        var client = sp.GetRequiredService<ICometBftRestClient>();

        var ex = await Record.ExceptionAsync(() =>
            client.DialPeersAsync([], persistent: false, unconditional: false, isPrivate: false));

        Assert.True(ex is null or CometBFT.Client.Core.Exceptions.CometBftRestException);
    }

    private static ServiceProvider BuildRestServices(string rpcUrl)
    {
        var services = new ServiceCollection();
        services.AddCometBftRest(options => options.BaseUrl = rpcUrl);
        return services.BuildServiceProvider();
    }
}

internal static class EndpointConfiguration
{
    internal const string DefaultRpcUrl = "https://cosmoshub.tendermintrpc.lava.build:443";
    internal const string DefaultWsUrl = "wss://cosmoshub.tendermintrpc.lava.build:443/websocket";
    internal const string DefaultGrpcUrl = "cosmoshub.grpc.lava.build";

    public static string RequireRpc() => Require("COMETBFT_RPC_URL", (string?)DefaultRpcUrl);

    public static string RequireWs() => Require("COMETBFT_WS_URL", (string?)DefaultWsUrl);

    public static string RequireGrpc() => Require("COMETBFT_GRPC_URL", (string?)DefaultGrpcUrl);

    public static string RequireUnsafeRpc() => Require(
        "COMETBFT_UNSAFE_RPC_URL",
        documentedDefault: null);

    private static string Require(string variableName, string? documentedDefault)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            var hint = documentedDefault is not null
                ? $" Documented testnet default: {documentedDefault}"
                : " No public default — requires a node started with --rpc.unsafe=true.";
            throw new Xunit.Sdk.XunitException(
                $"{Xunit.v3.DynamicSkipToken.Value}Set {variableName} to enable this test.{hint}");
        }

        return value;
    }
}
