#!/usr/bin/env bash
# Runs the unified Avalonia dashboard demo inside a self-contained Docker image.
# Set COMETBFT_RPC_URL / COMETBFT_WS_URL to override defaults.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

docker build \
  -t cometbft-client:demo \
  -f "${SCRIPT_DIR}/Dockerfile" \
  "${REPO_ROOT}"

docker run --rm \
  -e COMETBFT_RPC_URL \
  -e COMETBFT_WS_URL \
  cometbft-client:demo \
  ./scripts/demo.sh "$@"
