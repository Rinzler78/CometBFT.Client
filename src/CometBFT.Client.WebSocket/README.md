# CometBFT.Client.WebSocket

WebSocket subscription client for [CometBFT](https://github.com/cometbft/cometbft) nodes.
Targets protocol version **v0.38.9** — all 5 public subscriptions covered.

## Installation

```
dotnet add package CometBFT.Client.WebSocket
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;

var services = new ServiceCollection();
services.AddCometBftWebSocket(o => o.BaseUrl = "ws://localhost:26657/websocket");
var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ICometBftWebSocketClient>();

await client.ConnectAsync();
client.NewBlockReceived += (_, args) =>
    Console.WriteLine($"Block #{args.Value.Height}: {args.Value.Hash}");
client.TxExecuted += (_, args) =>
    Console.WriteLine($"Tx {args.Value.Hash}: code={args.Value.Code}");
```

## Subscriptions

| Event | Handler |
|-------|---------|
| `NewBlock` | `NewBlockReceived` |
| `NewBlockHeader` | `NewBlockHeaderReceived` |
| `Tx` | `TxExecuted` |
| `Vote` | `VoteReceived` |
| `ValidatorSetUpdates` | `ValidatorSetUpdated` |

## Features

- Automatic reconnection with configurable backoff
- Typed event handlers — no raw JSON
- Thread-safe subscribe/unsubscribe
