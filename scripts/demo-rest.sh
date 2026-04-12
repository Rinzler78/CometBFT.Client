#!/usr/bin/env bash
set -euo pipefail

COMETBFT_RPC_URL="${COMETBFT_RPC_URL:-https://cosmoshub.tendermintrpc.lava.build:443}"
export COMETBFT_RPC_URL

dotnet run --project samples/CometBFT.Client.Demo.Rest \
  --configuration Release "$@"
