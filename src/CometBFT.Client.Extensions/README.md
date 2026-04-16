# CometBFT.Client.Extensions

Dependency injection extensions for the CometBFT.Client library suite.
Targets [CometBFT](https://github.com/cometbft/cometbft) protocol version **v0.38.9**.

## Installation

```
dotnet add package Rinzler78.CometBFT.Client
```

This single package pulls in all three transports (REST, WebSocket, gRPC) and their dependencies.

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Extensions;

var services = new ServiceCollection();

// Register REST transport
services.AddCometBftRest(options =>
{
    options.BaseUrl = "http://localhost:26657";
    options.Timeout = TimeSpan.FromSeconds(30);
    options.MaxRetryAttempts = 3;
});

// Register WebSocket transport
services.AddCometBftWebSocket(options =>
{
    options.BaseUrl = "ws://localhost:26657/websocket";
});

// Register gRPC transport
services.AddCometBftGrpc(options =>
{
    options.BaseUrl = "http://localhost:9090";
});
```

## Extension methods

| Method | Registers |
|--------|-----------|
| `AddCometBftRest` | `ICometBftRestClient` + Polly HTTP pipeline |
| `AddCometBftWebSocket` | `ICometBftWebSocketClient` |
| `AddCometBftGrpc` | `ICometBftGrpcClient` + gRPC channel (CometBFT BroadcastAPI) |
| `AddCometBftSdkGrpc` | `ICometBftSdkGrpcClient` + gRPC channel (Cosmos SDK `cosmos.base.tendermint.v1beta1`) |
