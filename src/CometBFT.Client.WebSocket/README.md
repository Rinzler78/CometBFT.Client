# CometBFT.Client.WebSocket

WebSocket subscription client for [CometBFT](https://github.com/cometbft/cometbft) nodes.
Targets protocol version **v0.39.1** — 18 subscribable events covered.

## Installation

Install via the unified package:

```
dotnet add package Rinzler78.CometBFT.Client
```

This package includes all transports (REST, WebSocket, gRPC) and DI extensions.

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

## Event subscriptions (domain events)

| `tm.event` | Subscribe method | Event handler |
|-----------|------------------|---------------|
| `NewBlock` | `SubscribeNewBlockAsync` | `NewBlockReceived` |
| `NewBlockHeader` | `SubscribeNewBlockHeaderAsync` | `NewBlockHeaderReceived` |
| `Tx` | `SubscribeTxAsync` | `TxExecuted` |
| `Vote` | `SubscribeVoteAsync` | `VoteReceived` |
| `ValidatorSetUpdates` | `SubscribeValidatorSetUpdatesAsync` | `ValidatorSetUpdated` |

## Connection lifecycle events

| Event | When it fires |
|-------|---------------|
| `Disconnected` | TCP connection drops; a reconnection attempt is already in progress |
| `Reconnected` | Connection restored and all active subscriptions replayed. Does **not** fire on the initial connection. |
| `ErrorOccurred` | A message-processing error occurred; the connection loop is kept alive |

```csharp
ws.Disconnected += (_, _) =>
{
    Console.WriteLine("Disconnected — reconnecting…");
    UpdateStatusBadge("Reconnecting…");
};
ws.Reconnected += (_, _) =>
{
    Console.WriteLine("Reconnected — subscriptions replayed");
    UpdateStatusBadge("Reconnected");
};
ws.ErrorOccurred += (_, e) => Console.WriteLine($"[ERR] {e.Value.Message}");
```

When `Disconnected` fires, the underlying `Websocket.Client` has already started its
reconnect loop using `ErrorReconnectTimeout` (default 10 s). Active subscriptions tracked
in `_activeSubscriptions` are replayed automatically on the next successful connection.

## Observable streams (v2.1.0+)

| `tm.event` | Subscribe method | Stream property | Payload type |
|-----------|------------------|-----------------|--------------|
| `NewBlockEvents` 🔴 | `SubscribeNewBlockEventsAsync` | `NewBlockEventsStream` | `NewBlockEventsData` |
| `CompleteProposal` | `SubscribeCompleteProposalAsync` | `CompleteProposalStream` | `CompleteProposalData` |
| `ValidatorSetUpdates` | `SubscribeValidatorSetUpdatesAsync` | `ValidatorSetUpdatesStream` | `ValidatorSetUpdatesData` |
| `NewEvidence` | `SubscribeNewEvidenceAsync` | `NewEvidenceStream` | `NewEvidenceData` |
| 9 consensus-internal topics | `SubscribeConsensusInternalAsync` | `ConsensusInternalStream` | `CometBftEvent` |

`ConsensusInternalStream` merges: TimeoutPropose, TimeoutWait, Lock, Unlock, Relock, PolkaAny, PolkaNil, PolkaAgain, MissingProposalBlock.

### DeFi indexing example

```csharp
await client.ConnectAsync();
await client.SubscribeNewBlockEventsAsync();

client.NewBlockEventsStream
    .SelectMany(d => d.Events)
    .Where(e => e.Type == "ibc_transfer")
    .Subscribe(e => Console.WriteLine($"IBC transfer: {e.Attributes[0].Value}"));
```

## Features

- Automatic reconnection with configurable backoff
- `Disconnected` / `Reconnected` lifecycle events for UI and monitoring integration
- Active subscriptions replayed automatically on reconnect
- Typed event handlers — no raw JSON
- `IObservable<T>` streams for reactive composition
- Thread-safe subscribe/unsubscribe
