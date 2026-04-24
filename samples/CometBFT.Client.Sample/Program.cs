using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;

// ── Dependency injection setup ──────────────────────────────────────────────
var services = new ServiceCollection();

services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// REST client — connects to a local CometBFT node on the default RPC port.
services.AddCometBftRest(options =>
{
    options.BaseUrl = Environment.GetEnvironmentVariable("COMETBFT_RPC_URL")
        ?? "http://localhost:26657";
    options.Timeout = TimeSpan.FromSeconds(30);
    options.MaxRetryAttempts = 3;
});

// WebSocket client — subscribes to real-time events.
services.AddCometBftWebSocket(options =>
{
    options.BaseUrl = Environment.GetEnvironmentVariable("COMETBFT_WS_URL")
        ?? "ws://localhost:26657/websocket";
    options.ReconnectTimeout = TimeSpan.FromSeconds(30);
});

// gRPC client — connects to the gRPC port (CometBFT default: 9090).
services.AddCometBftGrpc(options =>
{
    options.BaseUrl = Environment.GetEnvironmentVariable("COMETBFT_GRPC_URL")
        ?? "http://localhost:9090";
});

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();

// ── REST demo ────────────────────────────────────────────────────────────────
logger.LogInformation("=== REST Demo ===");
var rest = provider.GetRequiredService<ICometBftRestClient>();

try
{
    var healthy = await rest.GetHealthAsync();
    logger.LogInformation("Node healthy: {Healthy}", healthy);

    var (nodeInfo, syncInfo) = await rest.GetStatusAsync();
    logger.LogInformation(
        "Network: {Network} | Version: {Version} | Height: {Height} | CatchingUp: {CatchingUp}",
        nodeInfo.Network, nodeInfo.Version, syncInfo.LatestBlockHeight, syncInfo.CatchingUp);

    var latestBlock = await rest.GetBlockAsync();
    logger.LogInformation(
        "Latest block #{Height} | Hash: {Hash} | Txs: {TxCount}",
        latestBlock.Height, latestBlock.Hash, latestBlock.Txs.Count);

    var validators = await rest.GetValidatorsAsync(page: 1, perPage: 5);
    logger.LogInformation("Active validators (first 5): {Count}", validators.Count);
    foreach (var v in validators)
    {
        logger.LogInformation("  Validator {Address} | VotingPower: {Power}", v.Address, v.VotingPower);
    }

    var abciInfo = await rest.GetAbciInfoAsync();
    logger.LogInformation(
        "ABCI app: {App} | version: {Version}",
        abciInfo["data"], abciInfo["version"]);
}
catch (CometBftRestException ex)
{
    logger.LogWarning("REST error (is a CometBFT node running?): {Message}", ex.Message);
}
catch (HttpRequestException ex)
{
    logger.LogWarning("HTTP error (is a CometBFT node running?): {Message}", ex.Message);
}

// ── gRPC demo ────────────────────────────────────────────────────────────────
logger.LogInformation("=== gRPC Demo ===");
var grpc = provider.GetRequiredService<ICometBftGrpcClient>();

try
{
    var pong = await grpc.PingAsync();
    logger.LogInformation("gRPC ping: {Pong}", pong);
}
catch (CometBftGrpcException ex)
{
    logger.LogWarning("gRPC error (is gRPC port open?): {Message}", ex.Message);
}
finally
{
    await grpc.DisposeAsync();
}

// ── WebSocket demo ───────────────────────────────────────────────────────────
logger.LogInformation("=== WebSocket Demo (5 second window) ===");
var ws = provider.GetRequiredService<ICometBftWebSocketClient>();

ws.NewBlockReceived += (_, e) =>
    logger.LogInformation("WS: new block #{Height}", e.Value.Height);

ws.TxExecuted += (_, e) =>
    logger.LogInformation("WS: tx executed hash={Hash}", e.Value.Hash);

try
{
    await ws.ConnectAsync();
    // Subscribe concurrently — relays batch-flush ACKs, serial awaits stall 30–45 s each.
    await Task.WhenAll(
        ws.SubscribeNewBlockAsync(),
        ws.SubscribeTxAsync(),
        ws.SubscribeNewBlockEventsAsync());

    using var newBlockEventsSub = ws.NewBlockEventsStream
        .Subscribe(d => logger.LogInformation(
            "WS: NewBlockEvents #{Height} — {Count} ABCI events", d.Height, d.Events.Count));

    logger.LogInformation("Subscribed — listening for 5 seconds...");
    await Task.Delay(TimeSpan.FromSeconds(5));

    await ws.UnsubscribeAllAsync();
    await ws.DisconnectAsync();
}
catch (CometBftWebSocketException ex)
{
    logger.LogWarning("WebSocket error (is a CometBFT node running?): {Message}", ex.Message);
}
finally
{
    await ws.DisposeAsync();
}

logger.LogInformation("Sample complete.");

// ── NewBlockEvents — DeFi indexing pattern (v2.1.0) ─────────────────────────
//
// NewBlockEventsStream fires on every committed block with ALL ABCI events.
// Use SelectMany + Where to filter on specific event types without REST polling.
//
//    await ws.ConnectAsync();
//    await ws.SubscribeNewBlockEventsAsync();
//
//    ws.NewBlockEventsStream
//        .SelectMany(d => d.Events)
//        .Where(e => e.Type == "ibc_transfer")
//        .Subscribe(e => OnIbcTransfer(e));
//
// ── Extension guide (v2.0.0) ─────────────────────────────────────────────────
//
// CometBFT.Client v2 is designed for extension without redefinition.
//
// 1. Extend a domain type:
//
//    record CosmosBlock<TTx>(
//        long Height, string Hash, DateTimeOffset Time, string Proposer,
//        IReadOnlyList<TTx> Txs,
//        string AppHash,   // Cosmos-specific
//        string ChainId)   // Cosmos-specific
//        : Block<TTx>(Height, Hash, Time, Proposer, Txs)
//        where TTx : notnull;
//
// 2. Extend the REST client interface:
//
//    interface ICosmosRestClient
//        : ICometBftRestClient<CosmosBlock<string>, TxResult, Validator> { }
//
// 3. Register with the 5-param overload — same Polly pipeline, no boilerplate:
//
//    services.AddCometBftRest<CosmosBlock<string>, TxResult, Validator,
//        ICosmosRestClient, CosmosRestClient>(o => { ... });
//
// 4. Extend the WebSocket client interface:
//
//    interface ICosmosWebSocketClient
//        : ICometBftWebSocketClient<CosmosTx, CosmosBlock<CosmosTx>, CosmosTxResult, CosmosValidator> { }
//
// 5. Register with the 6-param overload:
//
//    services.AddCometBftWebSocket<CosmosTx, CosmosBlock<CosmosTx>, CosmosTxResult, CosmosValidator,
//        ICosmosWebSocketClient, CosmosWebSocketClient>(o => { ... }, codec);
