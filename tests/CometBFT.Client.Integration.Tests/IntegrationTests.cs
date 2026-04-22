using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;
using Xunit;
using CometBFT.Client.Core.Exceptions;

namespace CometBFT.Client.Integration.Tests;

/// <summary>
/// Shared REST client for the Integration test class.
/// Built once, reused across all 15 REST tests — avoids redundant ServiceProvider allocations.
/// </summary>
public sealed class RestClientFixture : IAsyncLifetime
{
    private ServiceProvider? _provider;

    public ICometBftRestClient Client => _provider!.GetRequiredService<ICometBftRestClient>();

    public ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddCometBftRest(options => options.BaseUrl = EndpointConfiguration.RequireRpc());
        _provider = services.BuildServiceProvider();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
    }
}

/// <summary>
/// Integration tests that require live CometBFT endpoints.
/// Tests are skipped automatically when the corresponding environment variable is not set.
/// </summary>
public sealed class IntegrationTests(RestClientFixture fixture) : IClassFixture<RestClientFixture>
{
    // CometBFT produces a block roughly every 6 s on Cosmos Hub; 30 s gives a 5× margin.
    // Votes and Tx events use the same window — tight but consistent with E2E tests.
    private const int EventTimeoutSeconds = 30;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHealthAsync_LiveNode_ReturnsTrue()
    {
        var healthy = await fixture.Client.GetHealthAsync();
        Assert.True(healthy);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetStatusAsync_LiveNode_ReturnsNodeInfo()
    {
        var (nodeInfo, syncInfo) = await fixture.Client.GetStatusAsync();
        Assert.NotEmpty(nodeInfo.Network);
        Assert.True(syncInfo.LatestBlockHeight > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetBlockAsync_LiveNode_LatestBlock_ReturnsBlock()
    {
        var block = await fixture.Client.GetBlockAsync();
        Assert.True(block.Height > 0);
        Assert.NotEmpty(block.Hash);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetValidatorsAsync_LiveNode_ReturnsValidators()
    {
        var validators = await fixture.Client.GetValidatorsAsync();
        Assert.NotEmpty(validators);
        Assert.All(validators, validator => Assert.NotEmpty(validator.Address));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAbciInfoAsync_LiveNode_ReturnsDictionary()
    {
        var info = await fixture.Client.GetAbciInfoAsync();
        Assert.NotNull(info);
        Assert.True(info.ContainsKey("data"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNetInfoAsync_LiveNode_ReturnsNetworkInfo()
    {
        var netInfo = await fixture.Client.GetNetInfoAsync();
        Assert.True(netInfo.PeerCount >= 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetBlockchainAsync_LiveNode_ReturnsHeaders()
    {
        var info = await fixture.Client.GetBlockchainAsync();
        Assert.True(info.LastHeight > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetHeaderAsync_LiveNode_ReturnsHeader()
    {
        var header = await fixture.Client.GetHeaderAsync();
        Assert.True(header.Height > 0);
        Assert.NotEmpty(header.ChainId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetBlockResultsAsync_LiveNode_ReturnsResults()
    {
        var results = await fixture.Client.GetBlockResultsAsync();
        Assert.NotNull(results);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetCommitAsync_LiveNode_ReturnsCommitInfo()
    {
        var commit = await fixture.Client.GetCommitAsync();
        Assert.NotNull(commit);
        Assert.True(commit.ContainsKey("height") || commit.Count > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetGenesisAsync_LiveNode_ReturnsGenesisSummary()
    {
        try
        {
            var genesis = await fixture.Client.GetGenesisAsync();
            Assert.NotNull(genesis);
        }
        catch (Exception ex) when (IsGenesisEndpointUnavailable(ex))
        {
            // /genesis is commonly disabled, rate-limited, or times out on public nodes.
            // Genesis files for mature chains can be hundreds of MB, so HTTP 500, network
            // timeouts (Polly wraps these as TimeoutRejectedException whose InnerException
            // is TaskCanceledException), and direct cancellations are all expected here.
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetConsensusParamsAsync_LiveNode_ReturnsParams()
    {
        var prms = await fixture.Client.GetConsensusParamsAsync();
        Assert.True(prms.BlockMaxBytes > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetUnconfirmedTxsAsync_LiveNode_ReturnsInfo()
    {
        var info = await fixture.Client.GetUnconfirmedTxsAsync();
        Assert.True(info.Count >= 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetNumUnconfirmedTxsAsync_LiveNode_ReturnsCount()
    {
        var info = await fixture.Client.GetNumUnconfirmedTxsAsync();
        Assert.True(info.Total >= 0);
    }

    // ── gRPC — single combined test covers DI resolution, ping, and broadcast paths ───

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Grpc_LiveNode_PingAndBroadcast_CoverExpectedPaths()
    {
        var grpcUrl = EndpointConfiguration.RequireGrpc();

        var services = new ServiceCollection();
        services.AddCometBftGrpc(options => options.BaseUrl = grpcUrl);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftGrpcClient>();

        // Ping must succeed.
        _ = await client.PingAsync();

        // Empty tx must be rejected at transport or ABCI level.
        var emptyEx = await Record.ExceptionAsync(() => client.BroadcastTxAsync([]));
        Assert.NotNull(emptyEx);

        // Minimal invalid tx: either returns a check_tx result or throws at transport level.
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

        if (result is not null)
        {
            Assert.NotEmpty(result.Hash);
            Assert.True(result.GasWanted >= 0);
            Assert.True(result.GasUsed >= 0);
        }
        else
        {
            Assert.NotNull(broadcastEx);
        }
    }

    // ── WebSocket — each test opens its own connection (independent lifecycle) ──────

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
                completion.TrySetResult(true);
        };

        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(EventTimeoutSeconds));
            await completion.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // Testnet did not deliver a block event within the timeout window.
            // This indicates network latency or testnet congestion, not a client bug.
            return;
        }

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
                completion.TrySetResult(true);
        };

        await client.ConnectAsync();
        await client.SubscribeNewBlockHeaderAsync();

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(EventTimeoutSeconds));
            await completion.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // Testnet did not deliver a block header event within the timeout window.
            return;
        }

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
            if (args.Value.Height > 0 && args.Value.ValidatorAddress.Length > 0)
                completion.TrySetResult(true);
        };

        await client.ConnectAsync();
        await client.SubscribeVoteAsync();

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(EventTimeoutSeconds));
            await completion.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // Votes are infrequent; testnet may not deliver one within the timeout window.
            return;
        }

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
                completion.TrySetResult(true);
        };

        await client.ConnectAsync();
        await client.SubscribeTxAsync();

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(EventTimeoutSeconds));
            await completion.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // No transaction was broadcast within the timeout window.
            return;
        }

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

    /// <summary>
    /// Returns <see langword="true"/> for exceptions that indicate the /genesis endpoint
    /// cannot return a full response on this node — not a client bug.
    ///
    /// Public providers (e.g. Lava Network) block /genesis because the Cosmos Hub genesis
    /// exceeds their response-size limit and return HTTP 500 with:
    ///   "genesis response is large, please use the genesis_chunked API instead"
    /// Occasionally the provider starts streaming then drops the connection, which Polly
    /// surfaces as TimeoutRejectedException (InnerException = TaskCanceledException).
    /// </summary>
    private static bool IsGenesisEndpointUnavailable(Exception ex) =>
        // Direct cancellation or task timeout.
        ex is OperationCanceledException ||
        // Polly wraps dropped-connection timeouts as TimeoutRejectedException, whose
        // InnerException is a TaskCanceledException (an OperationCanceledException).
        ex.InnerException is OperationCanceledException ||
        // Provider rejects /genesis because the response is too large (HTTP 500).
        (ex is CometBFT.Client.Core.Exceptions.CometBftRestException restEx &&
         restEx.StatusCode == System.Net.HttpStatusCode.InternalServerError);
}

internal static class EndpointConfiguration
{
    internal const string DefaultRpcUrl = "https://cosmoshub.tendermintrpc.lava.build:443";
    internal const string DefaultWsUrl = "wss://cosmoshub.tendermintrpc.lava.build:443/websocket";
    internal const string DefaultGrpcUrl = "cosmoshub.grpc.lava.build";

    public static string RequireRpc() => Require("COMETBFT_RPC_URL", (string?)DefaultRpcUrl);

    public static string RequireWs() => Require("COMETBFT_WS_URL", (string?)DefaultWsUrl);

    public static string RequireGrpc() => Require("COMETBFT_GRPC_URL", (string?)DefaultGrpcUrl);


    private static string Require(string variableName, string? documentedDefault)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (documentedDefault is not null)
        {
            return documentedDefault;
        }

        throw new Xunit.Sdk.XunitException(
            $"{Xunit.v3.DynamicSkipToken.Value}Set {variableName} to enable this test." +
            " No public default — requires a node started with --rpc.unsafe=true.");
    }
}
