# CometBFT.Client

[![CI](https://github.com/Rinzler78/CometBFT.Client/actions/workflows/ci.yml/badge.svg)](https://github.com/Rinzler78/CometBFT.Client/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Rinzler78.CometBFT.Client.svg)](https://www.nuget.org/packages/Rinzler78.CometBFT.Client)
[![Protocol](https://img.shields.io/badge/CometBFT-v0.39.1-blue)](https://github.com/cometbft/cometbft/releases/tag/v0.39.1)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Production-ready .NET 10 client library for [CometBFT](https://github.com/cometbft/cometbft) (formerly Tendermint), targeting protocol version **v0.39.1**.

Provides REST/JSON-RPC, WebSocket subscription, and gRPC transports with full dependency injection support.

## Installation

```
dotnet add package Rinzler78.CometBFT.Client
```

This single package includes all three transports (REST, WebSocket, gRPC) and DI extensions.

## Breaking Change Notice

The removal of `ICometBftSdkGrpcClient`, `CometBftSdkGrpcClient`,
`CometBftSdkGrpcOptions`, and `AddCometBftSdkGrpc()` is a **breaking API change**.
Under this repository's SemVer rules, any release that contains this removal must be a
**major** release.

### Migration from v0.2.0

If your code used the Cosmos SDK gRPC service (`cosmos.base.tendermint.v1beta1`):

- replace `AddCometBftSdkGrpc(...)` with the equivalent registration from
  `Rinzler78.Cosmos.Client`
- replace `ICometBftSdkGrpcClient` with the Cosmos-layer client from
  `Rinzler78.Cosmos.Client`
- keep using `Rinzler78.CometBFT.Client` for CometBFT-native transports only:
  REST, WebSocket, and gRPC `BroadcastAPI` (`Ping`, `BroadcastTx`)

## Quick Start

### Unified registration (recommended)

Register all three transports in a single call:

```csharp
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Extensions;

var services = new ServiceCollection();
services.AddCometBftClient(options =>
{
    options.RestBaseUrl    = "http://localhost:26657";
    options.WebSocketBaseUrl = "ws://localhost:26657/websocket";
    options.GrpcBaseUrl    = "http://localhost:9090";
});
```

`AddCometBftClient` registers `ICometBftRestClient`, `ICometBftWebSocketClient`,
and `ICometBftGrpcClient` in one call.
Use the individual `Add*` methods below only when you need per-transport customisation.

### REST Transport

```csharp
services.AddCometBftRest(options =>
{
    options.BaseUrl           = "http://localhost:26657";
    options.Timeout           = TimeSpan.FromSeconds(30);
    options.MaxRetryAttempts  = 3;
});

var client = provider.GetRequiredService<ICometBftRestClient>();

// Health check
bool healthy = await client.GetHealthAsync();

// Node status
var (nodeInfo, syncInfo) = await client.GetStatusAsync();
Console.WriteLine($"Chain: {nodeInfo.Network}, Height: {syncInfo.LatestBlockHeight}");

// Latest block
var block = await client.GetBlockAsync();
Console.WriteLine($"Block #{block.Height}: {block.Hash}");

// Validators
var validators = await client.GetValidatorsAsync();
Console.WriteLine($"Active validators: {validators.Count}");

// Broadcast a transaction
var result = await client.BroadcastTxSyncAsync("<base64-tx-bytes>");
Console.WriteLine($"Code: {result.Code}, Hash: {result.Hash}");
```

### WebSocket Transport

```csharp
services.AddCometBftWebSocket(options =>
{
    options.BaseUrl               = "ws://localhost:26657/websocket";
    options.ReconnectTimeout      = TimeSpan.FromSeconds(30); // reconnect after clean disconnect
    options.ErrorReconnectTimeout = TimeSpan.FromSeconds(10); // reconnect after error
    options.SubscribeAckTimeout   = TimeSpan.FromSeconds(30); // wait for subscription ack
});

var ws = provider.GetRequiredService<ICometBftWebSocketClient>();

// Subscribe to events
ws.NewBlockReceived       += (_, e) => Console.WriteLine($"Block  #{e.Value.Height}");
ws.NewBlockHeaderReceived += (_, e) => Console.WriteLine($"Header #{e.Value.Height}");
ws.TxExecuted             += (_, e) => Console.WriteLine($"Tx {e.Value.Hash}");
ws.VoteReceived           += (_, e) => Console.WriteLine($"Vote h={e.Value.Height} r={e.Value.Round}");
ws.ValidatorSetUpdated    += (_, e) => Console.WriteLine($"ValidatorSet: {e.Value.Count} validators");
ws.ErrorOccurred          += (_, e) => Console.WriteLine($"[ERR] {e.Value.Message}");

await ws.ConnectAsync();

// Subscribe to desired event streams
await ws.SubscribeNewBlockAsync();
await ws.SubscribeNewBlockHeaderAsync();
await ws.SubscribeTxAsync();
await ws.SubscribeVoteAsync();
await ws.SubscribeValidatorSetUpdatesAsync();

await Task.Delay(Timeout.Infinite);

// Graceful shutdown
await ws.UnsubscribeAllAsync();
await ws.DisconnectAsync();
```

**Generic WebSocket** — decode transaction bytes into a custom type via `ITxCodec<TTx>`:

```csharp
services.AddCometBftWebSocket<MyTx>(options => { ... }, new MyTxCodec());
// Resolves ICometBftWebSocketClient<MyTx>
```

### gRPC Transport

Two gRPC clients are available depending on which proto the target node exposes.

**`ICometBftGrpcClient`** — CometBFT native BroadcastAPI proto
(`cometbft.rpc.grpc`, legacy `tendermint.rpc.grpc`). Use this when the node
only exposes the raw CometBFT gRPC surface:

```csharp
services.AddCometBftGrpc(options =>
{
    options.BaseUrl  = "http://localhost:9090";
    // options.Protocol = GrpcProtocol.Auto; // Auto | CometBft | TendermintLegacy
});

var grpc = provider.GetRequiredService<ICometBftGrpcClient>();

bool alive = await grpc.PingAsync();
```

> **gRPC scope** — CometBFT’s native gRPC surface exposes only `Ping` and `BroadcastTx`.
> All other node data (status, blocks, validators, syncing) is available via the REST client.
> The `cosmos.base.tendermint.v1beta1` gRPC service is a Cosmos SDK addition;
> it belongs in `Rinzler78.Cosmos.Client`.

## Architecture

| Package | Responsibility |
|---------|---------------|
| `CometBFT.Client.Core` | Domain types, interfaces, options, exceptions |
| `CometBFT.Client.Rest` | HTTP/JSON-RPC 2.0 client with Polly resilience |
| `CometBFT.Client.WebSocket` | WebSocket subscription client with auto-reconnect |
| `CometBFT.Client.Grpc` | gRPC client — CometBFT BroadcastAPI (`ICometBftGrpcClient`: `Ping`, `BroadcastTx`) |
| `CometBFT.Client.Extensions` | `IServiceCollection` DI registration extensions |

## Running the Demos

Each demo connects to validated public Cosmos Hub endpoints by default — no configuration needed.
Override any endpoint via environment variable or CLI flag.

### Unified Dashboard (Avalonia GUI)

Real-time desktop dashboard combining WebSocket events and REST polling:

```bash
# Zero-config (Cosmos Hub mainnet)
./scripts/demo.sh

# Custom endpoints
COMETBFT_RPC_URL=https://cosmoshub.tendermintrpc.lava.build:443 \
COMETBFT_WS_URL=wss://cosmoshub.tendermintrpc.lava.build:443/websocket \
./scripts/demo.sh

# CLI flags
./scripts/demo.sh \
  --rpc-url=https://cosmoshub.tendermintrpc.lava.build:443 \
  --ws-url=wss://cosmoshub.tendermintrpc.lava.build:443/websocket

# Via Docker
./scripts/docker/demo.sh
```

The dashboard displays: latest block KPI, block feed, transaction feed, validator list with
voting-power bars, node info, and a live event log — all updating in real time.
It uses WebSocket + REST only; gRPC is not required for the dashboard.

### Console Sample

Minimal console application exercising all three transports sequentially:

```bash
cd samples/CometBFT.Client.Sample
dotnet run

# Override endpoints
COMETBFT_RPC_URL=https://... dotnet run
```

## Building and Publishing

```bash
# Build
./scripts/build.sh

# Test (coverage gate >= 90 %)
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

- [CometBFT v0.39.1 Release](https://github.com/cometbft/cometbft/releases/tag/v0.39.1)
- [CometBFT RPC Documentation](https://docs.cometbft.com/v0.38/rpc/)

## Validated Public Endpoints

| Transport | URL |
|---|---|
| REST / RPC | `https://cosmoshub.tendermintrpc.lava.build:443` |
| WebSocket | `wss://cosmoshub.tendermintrpc.lava.build:443/websocket` |
| gRPC | `https://cosmoshub.grpc.lava.build:443` |

## Contributing

1. Fork and create a `feature/<name>` branch from `develop`.
2. Commit using [Conventional Commits](https://www.conventionalcommits.org/).
3. Open a PR to `develop`. CI must pass before merge.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full development workflow, coding conventions,
Polly policy values, and test standards.

## License

MIT License. See [LICENSE](LICENSE).
