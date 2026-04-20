# CometBFT.Client.Extensions

Dependency injection extensions for the CometBFT.Client library suite.
Targets [CometBFT](https://github.com/cometbft/cometbft) protocol version **v0.38.9**.

## Installation

```
dotnet add package Rinzler78.CometBFT.Client
```

This single package pulls in all three transports (REST, WebSocket, gRPC) and their dependencies.

## Breaking Change Notice

The removal of `AddCometBftSdkGrpc`, `ICometBftSdkGrpcClient`, and
`CometBftSdkGrpcOptions` is a **breaking API change**.
Any release that includes this change must be published as a **major** version.

If you previously used the Cosmos SDK gRPC service
(`cosmos.base.tendermint.v1beta1`), migrate that dependency to
`Rinzler78.Cosmos.Client` and keep this package for CometBFT-native transports only.

## Usage

### Unified registration (recommended)

```csharp
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Extensions;

var services = new ServiceCollection();

// Register all three CometBFT-native transports in one call
services.AddCometBftClient(options =>
{
    options.RestBaseUrl      = "http://localhost:26657";
    options.WebSocketBaseUrl = "ws://localhost:26657/websocket";
    options.GrpcBaseUrl      = "http://localhost:9090";
});
```

### Individual transport registration

```csharp
// REST
services.AddCometBftRest(options =>
{
    options.BaseUrl           = "http://localhost:26657";
    options.Timeout           = TimeSpan.FromSeconds(30);
    options.MaxRetryAttempts  = 3;
});

// WebSocket
services.AddCometBftWebSocket(options =>
{
    options.BaseUrl = "ws://localhost:26657/websocket";
});

// gRPC — CometBFT BroadcastAPI (Ping + BroadcastTx)
services.AddCometBftGrpc(options =>
{
    options.BaseUrl = "http://localhost:9090";
});
```

## Extension methods

| Method | Registers |
|--------|-----------|
| `AddCometBftClient` | REST + WebSocket + gRPC (all three CometBFT-native transports) |
| `AddCometBftRest` | `ICometBftRestClient` + Polly HTTP pipeline |
| `AddCometBftWebSocket` | `ICometBftWebSocketClient` |
| `AddCometBftGrpc` | `ICometBftGrpcClient` + gRPC channel (CometBFT BroadcastAPI) |

> **Cosmos SDK gRPC** (`cosmos.base.tendermint.v1beta1`) is NOT part of this package.
> That service is a Cosmos SDK addition and belongs in `Rinzler78.Cosmos.Client`.
