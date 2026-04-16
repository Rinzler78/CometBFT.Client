# CometBFT.Client

[![CI](https://github.com/Rinzler78/CometBFT.Client/actions/workflows/ci.yml/badge.svg)](https://github.com/Rinzler78/CometBFT.Client/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Rinzler78.CometBFT.Client.svg)](https://www.nuget.org/packages/Rinzler78.CometBFT.Client)
[![Protocol](https://img.shields.io/badge/CometBFT-v0.38.9-blue)](https://github.com/cometbft/cometbft/releases/tag/v0.38.9)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Production-ready .NET 10 client library for [CometBFT](https://github.com/cometbft/cometbft) (formerly Tendermint), targeting protocol version **v0.38.9**.

Provides REST/JSON-RPC, WebSocket subscription, and gRPC transports with full dependency injection support.

## Installation

```
dotnet add package Rinzler78.CometBFT.Client
```

This single package includes all three transports (REST, WebSocket, gRPC) and DI extensions.

## Quick Start

### REST Transport

```csharp
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;

var services = new ServiceCollection();
services.AddCometBftRest(options =>
{
    options.BaseUrl = "http://localhost:26657";
    options.Timeout = TimeSpan.FromSeconds(30);
    options.MaxRetryAttempts = 3;
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ICometBftRestClient>();

// Health check
bool healthy = await client.GetHealthAsync();

// Get node status
var (nodeInfo, syncInfo) = await client.GetStatusAsync();
Console.WriteLine($"Chain: {nodeInfo.Network}, Height: {syncInfo.LatestBlockHeight}");

// Get latest block
var block = await client.GetBlockAsync();
Console.WriteLine($"Block #{block.Height}: {block.Hash}");

// Query validators
var validators = await client.GetValidatorsAsync();
Console.WriteLine($"Active validators: {validators.Count}");

// Broadcast a transaction
var result = await client.BroadcastTxSyncAsync("<base64-tx-bytes>");
Console.WriteLine($"Broadcast code: {result.Code}, hash: {result.Hash}");
```

### WebSocket Transport (real-time events)

```csharp
services.AddCometBftWebSocket(options =>
{
    options.BaseUrl = "ws://localhost:26657/websocket";
});

var wsClient = provider.GetRequiredService<ICometBftWebSocketClient>();

wsClient.NewBlockReceived += (_, args) =>
    Console.WriteLine($"New block: #{args.Value.Height}");

wsClient.TxExecuted += (_, args) =>
    Console.WriteLine($"Tx executed: {args.Value.Hash}");

await wsClient.ConnectAsync();
await wsClient.SubscribeNewBlockAsync();
await wsClient.SubscribeTxAsync();

// Keep alive...
await Task.Delay(Timeout.Infinite);
```

### gRPC Transport

**CometBFT BroadcastAPI** (legacy Tendermint proto):

```csharp
services.AddCometBftGrpc(options =>
{
    options.BaseUrl = "http://localhost:9090";
});

var grpcClient = provider.GetRequiredService<ICometBftGrpcClient>();

bool alive = await grpcClient.PingAsync();
Console.WriteLine($"gRPC ping: {alive}");
```

**Cosmos SDK gRPC** (`cosmos.base.tendermint.v1beta1` — available on most Cosmos nodes):

```csharp
services.AddCometBftSdkGrpc(options =>
{
    options.BaseUrl = "http://localhost:9090";
});

var sdkClient = provider.GetRequiredService<ICometBftSdkGrpcClient>();

var (nodeInfo, syncInfo) = await sdkClient.GetStatusAsync();
var block = await sdkClient.GetLatestBlockAsync();
var validators = await sdkClient.GetLatestValidatorsAsync();
```

## Architecture

| Package | Responsibility |
|---------|---------------|
| `CometBFT.Client.Core` | Domain types, interfaces, options, exceptions |
| `CometBFT.Client.Rest` | HTTP/JSON-RPC 2.0 client with Polly resilience |
| `CometBFT.Client.WebSocket` | WebSocket subscription client with auto-reconnect |
| `CometBFT.Client.Grpc` | gRPC client — CometBFT BroadcastAPI (`ICometBftGrpcClient`) and Cosmos SDK service (`ICometBftSdkGrpcClient`) |
| `CometBFT.Client.Extensions` | `IServiceCollection` DI registration extensions |

## Running the Demos

Each demo connects to validated public CometBFT endpoints by default — no configuration needed.
Override any endpoint via environment variable or CLI flag.

### REST demo

```bash
# Zero-config (testnet)
./scripts/demo-rest.sh

# Custom endpoint
TENDERMINT_RPC_URL=https://cosmoshub.tendermintrpc.lava.build:443 ./scripts/demo-rest.sh
./scripts/demo-rest.sh --rpc-url=https://cosmoshub.tendermintrpc.lava.build:443

# Via Docker
./scripts/docker/demo-rest.sh
TENDERMINT_RPC_URL=https://cosmoshub.tendermintrpc.lava.build:443 ./scripts/docker/demo-rest.sh
```

### WebSocket demo

```bash
./scripts/demo-ws.sh
TENDERMINT_WS_URL=wss://cosmoshub.tendermintrpc.lava.build:443/websocket ./scripts/demo-ws.sh
./scripts/docker/demo-ws.sh
```

### gRPC demo

```bash
./scripts/demo-grpc.sh
TENDERMINT_GRPC_URL=cosmoshub.grpc.lava.build ./scripts/demo-grpc.sh
./scripts/docker/demo-grpc.sh
```

## Building and Publishing

```bash
# Build
./scripts/build.sh

# Test (coverage gate ≥ 90 %)
./scripts/test.sh

# Publish to NuGet (dry-run)
./scripts/publish.sh --dry-run

# Publish with API key (local)
NUGET_API_KEY=<key> ./scripts/publish.sh

# Publish via Docker (key never passed as CLI arg)
NUGET_API_KEY=<key> ./scripts/docker/publish.sh
```

> `NUGET_API_KEY` is always read from the environment — never pass it as a command-line argument.

## CometBFT Reference

- [CometBFT v0.38.9 Release](https://github.com/cometbft/cometbft/releases/tag/v0.38.9)
- [CometBFT RPC Documentation](https://docs.cometbft.com/v0.38/rpc/)

## Validated Public Endpoints

- RPC: `https://cosmoshub.tendermintrpc.lava.build:443`
- WebSocket: `wss://cosmoshub.tendermintrpc.lava.build:443/websocket`
- gRPC: `cosmoshub.grpc.lava.build`

## Contributing

1. Fork and create a `feature/<name>` branch from `develop`.
2. Commit using [Conventional Commits](https://www.conventionalcommits.org/).
3. Open a PR to `develop`. CI must pass before merge.

## License

MIT License. See [LICENSE](LICENSE).
