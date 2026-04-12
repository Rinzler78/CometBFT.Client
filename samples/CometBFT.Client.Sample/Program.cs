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
    await ws.SubscribeNewBlockAsync();
    await ws.SubscribeTxAsync();

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
