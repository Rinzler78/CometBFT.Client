#!/usr/bin/env bash
# Runs the REST demo inside a self-contained Docker image (no bind mount for sources).
# Set COMETBFT_RPC_URL in the environment to override the default testnet endpoint.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

docker build \
  -t cometbft-client:demo-rest \
  -f "${SCRIPT_DIR}/Dockerfile" \
  "${REPO_ROOT}"

docker run --rm \
  -e COMETBFT_RPC_URL \
  cometbft-client:demo-rest \
  ./scripts/demo-rest.sh "$@"
