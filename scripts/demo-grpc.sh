#!/usr/bin/env bash
set -euo pipefail

COMETBFT_GRPC_URL="${COMETBFT_GRPC_URL:-https://cosmoshub.grpc.lava.build}"
export COMETBFT_GRPC_URL

dotnet run --project samples/CometBFT.Client.Demo.Grpc \
  --configuration Release "$@"
