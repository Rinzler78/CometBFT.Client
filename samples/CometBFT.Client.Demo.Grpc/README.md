# CometBFT.Client.Demo.Grpc

Live Spectre.Console dashboard polling [CometBFT](https://github.com/cometbft/cometbft) gRPC BroadcastAPI every 10 seconds.

## Run

```bash
# Default testnet (Cosmos Hub via Lava)
./scripts/demo-grpc.sh

# Custom endpoint — CometBFT v0.38+ or legacy Tendermint Core (auto-detected)
COMETBFT_GRPC_URL=https://localhost:26657 ./scripts/demo-grpc.sh
# or
./scripts/demo-grpc.sh --grpc-url https://localhost:26657
```

## Dashboard panels

| Panel | API call |
|-------|----------|
| BroadcastAPI | `PingAsync` — latency + last timestamp |
| BroadcastAPI — check_tx fields | `BroadcastTxAsync` — Code, Log, Codespace, GasWanted, GasUsed, Hash |
| Live Blocks | Polling mode (streaming not available in CometBFT v0.38) |
