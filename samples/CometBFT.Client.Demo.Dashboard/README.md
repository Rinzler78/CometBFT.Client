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
| Connection status badge | Internal | On connect / subscribe result |

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

## Rate-Limit Handling

Public relays typically enforce `max_subscriptions_per_client = 5`. The dashboard
subscribes to 7 topics, so 2 rejections are expected and handled gracefully.

`Resilient()` wraps each subscribe call: on rejection the failure is logged to the
event log and counted. After all 7 settle, the connection badge shows:

- `Connected` — all 7 topics accepted
- `Degraded (n/7 topics)` — some topics rejected by the relay

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
        └── event handlers → vm.Post(…)
```

`MainWindowViewModel.Post(action)` marshals all mutations to the Avalonia UI thread via
`Dispatcher.UIThread.Post`.
