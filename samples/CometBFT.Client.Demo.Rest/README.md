# CometBFT.Client.Demo.Rest

Live Spectre.Console dashboard that polls all public [CometBFT](https://github.com/cometbft/cometbft) REST endpoints every 10 seconds.

## Run

```bash
# Default testnet (Cosmos Hub via Lava)
./scripts/demo-rest.sh

# Custom endpoint
TENDERMINT_RPC_URL=http://localhost:26657 ./scripts/demo-rest.sh
# or
./scripts/demo-rest.sh --rpc-url http://localhost:26657

# Unsafe endpoints (node must have RPC unsafe enabled)
./scripts/demo-rest.sh --unsafe
```

## Dashboard panels

| Panel | Endpoints |
|-------|-----------|
| Health / Status | `health`, `status` |
| Latest Block | `block`, `block_results` |
| Validators | `validators` |
| ABCI Info | `abci_info` |
| Network / Mempool | `net_info`, `unconfirmed_txs` |
| Unsafe (opt-in) | `dial_seeds`, `dial_peers` |
