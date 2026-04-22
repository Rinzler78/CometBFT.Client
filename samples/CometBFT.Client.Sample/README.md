# CometBFT.Client.Sample

Minimal console application demonstrating end-to-end usage of the CometBFT.Client library suite against a [CometBFT](https://github.com/cometbft/cometbft) node.

## Run

```bash
dotnet run --project samples/CometBFT.Client.Sample
```

## What it does

1. Registers all three transports (REST, WebSocket, gRPC) via `Microsoft.Extensions.DependencyInjection`
2. Resolves each client from the DI container
3. Executes a basic health check and status query via REST
4. Subscribes to `NewBlock` and `NewBlockEvents` events via WebSocket
5. Sends a `Ping` via gRPC

Useful as a copy-paste starting point for integrating CometBFT.Client into your own application.

## NewBlockEvents — DeFi indexing pattern

```csharp
await ws.ConnectAsync();
await ws.SubscribeNewBlockEventsAsync();

ws.NewBlockEventsStream
    .SelectMany(d => d.Events)
    .Where(e => e.Type == "ibc_transfer")
    .Subscribe(e => Console.WriteLine($"IBC transfer at block #{d.Height}"));
```
