# CometBFT.Client.Demo.WebSocket

Live Spectre.Console dashboard driven by [CometBFT](https://github.com/cometbft/cometbft) WebSocket events (no polling).

## Run

```bash
# Default testnet (Cosmos Hub via Lava)
./scripts/demo-ws.sh

# Custom endpoint
TENDERMINT_WS_URL=ws://localhost:26657/websocket ./scripts/demo-ws.sh
# or
./scripts/demo-ws.sh --ws-url ws://localhost:26657/websocket
```

## Dashboard panels

| Panel | Subscription |
|-------|-------------|
| Live Blocks | `NewBlock` |
| Live Transactions | `Tx` |
| Latest Header | `NewBlockHeader` |
| Validator Set Updates | `ValidatorSetUpdates` |
| Log | `Vote` + connection events |
