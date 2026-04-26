# CometBFT.Client.Demo.Dashboard

Real-time Avalonia 12 desktop dashboard combining WebSocket event subscriptions and REST polling against a live CometBFT node.

## Run

```bash
# Against the default public Cosmos Hub testnet (no setup required)
dotnet run --project samples/CometBFT.Client.Demo.Dashboard

# Against a custom node
COMETBFT_RPC_URL=https://my-node:443 \
COMETBFT_WS_URL=wss://my-node:443/websocket \
dotnet run --project samples/CometBFT.Client.Demo.Dashboard
```

Or use the launcher script:

```bash
./scripts/demo.sh
```

## What it displays

| Panel | Data source | Update trigger |
|-------|-------------|----------------|
| KPI row (latest block, tx count, validator count, peers) | REST | Every new block (WebSocket) |
| Block feed | WebSocket `NewBlockReceived` | Live |
| Transaction feed | WebSocket `TxExecuted` | Live |
| Validator list with voting-power bars | REST | `ValidatorSetUpdated` event + startup |
| Node info (moniker, version, network) | REST | Every 30 s |
| Net info (peer count) | REST | Every 30 s |
| Event log (color-coded by category) | All sources | Live |
| Connection status badge | Internal | On connect / subscribe result / disconnect / reconnect |

## Burst-Subscribe Pattern

CometBFT relays batch-flush subscription ACKs. Subscribing serially stalls each ACK
by 30–45 s per topic. The dashboard issues all 7 subscriptions concurrently:

```csharp
await Task.WhenAll(
    Resilient("subscribe NewBlock",           ws.SubscribeNewBlockAsync(ct)),
    Resilient("subscribe NewBlockHeader",     ws.SubscribeNewBlockHeaderAsync(ct)),
    Resilient("subscribe Tx",                 ws.SubscribeTxAsync(ct)),
    Resilient("subscribe Vote",               ws.SubscribeVoteAsync(ct)),
    Resilient("subscribe ValidatorSetUpdates",ws.SubscribeValidatorSetUpdatesAsync(ct)),
    Resilient("subscribe NewBlockEvents",     ws.SubscribeNewBlockEventsAsync(ct)),
    Resilient("subscribe NewEvidence",        ws.SubscribeNewEvidenceAsync(ct)));
```

## Connection Status Lifecycle

The connection badge cycles through these states:

| Badge | Meaning |
|-------|---------|
| `Connecting…` | `ConnectAsync` in progress |
| `Subscribing…` | connected; 7 burst-subscribes in flight |
| `Connected` | all 7 topics accepted and active |
| `Degraded (n/7 topics)` | some topics rejected (relay rate-limit) |
| `Reconnecting…` | TCP dropped; `Disconnected` event fired |
| `Reconnected` | TCP restored; subscriptions replayed |
| `Disconnected` | service stopped or fatal error |

## Rate-Limit Handling

Public relays typically enforce `max_subscriptions_per_client = 5`. The dashboard
subscribes to 7 topics, so 2 rejections are expected and handled gracefully.

`Resilient()` wraps each subscribe call: on rejection the failure is logged to the
event log and counted. After all 7 settle, the connection badge shows:

- `Connected` — all 7 topics accepted
- `Degraded (n/7 topics)` — some topics rejected by the relay

## Time Format

All timestamps use the **local timezone with a full date** (`yyyy-MM-dd HH:mm:ss`).
Block timestamps from CometBFT are UTC `DateTimeOffset` values; the dashboard converts
them via `.ToLocalTime()` before display. Including the date prevents ambiguity when a
reconnection crosses midnight.

## Relay Reliability Note

The default endpoint (`cosmoshub.tendermintrpc.lava.build`) is a Lava Network public relay.
It ACKs `subscribe` requests optimistically but may fail the backend subscription shortly
after a session rotation, producing continuous `Provider relay error` frames in the event log.
For sustained streaming, override the endpoint with a direct CometBFT node:

```bash
COMETBFT_RPC_URL=https://rpc.cosmos.directory/cosmoshub \
COMETBFT_WS_URL=wss://rpc.cosmos.directory:443/cosmoshub/websocket \
./scripts/demo.sh
```

## Architecture

```
DashboardBackgroundService          MainWindowViewModel
  └── ExecuteAsync()                  └── ObservableCollection<BlockRow>
        ├── ConnectAsync()            └── ObservableCollection<TxRow>
        ├── Task.WhenAll(7 subs)      └── ObservableCollection<ValidatorRow>
        ├── PeriodicTimer (30 s)      └── ObservableCollection<EventLogRow>
        │     └── Task.WhenAll(       └── ConnectionStatus / IsConnected
        │           RefreshNodeInfo,
        │           RefreshNetInfo)
        ├── domain event handlers → vm.Post(…)
        │     NewBlockReceived, NewBlockHeaderReceived, TxExecuted,
        │     VoteReceived, ValidatorSetUpdated, ErrorOccurred
        └── lifecycle event handlers → vm.Post(…)
              Disconnected → SetConnectionStatus("Reconnecting…")
              Reconnected  → SetConnectionStatus("Reconnected")
```

`MainWindowViewModel.Post(action)` marshals all mutations to the Avalonia UI thread via
`Dispatcher.UIThread.Post`.
