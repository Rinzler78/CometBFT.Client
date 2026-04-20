#!/usr/bin/env bash
set -euo pipefail

COMETBFT_RPC_URL="${COMETBFT_RPC_URL:-https://cosmoshub.tendermintrpc.lava.build:443}"
COMETBFT_GRPC_URL="${COMETBFT_GRPC_URL:-https://cosmoshub.grpc.lava.build:443}"
COMETBFT_WS_URL="${COMETBFT_WS_URL:-wss://cosmoshub.tendermintrpc.lava.build:443/websocket}"

export COMETBFT_RPC_URL COMETBFT_GRPC_URL COMETBFT_WS_URL

dotnet run --project samples/CometBFT.Client.Demo.Dashboard \
  --configuration Release "$@"
