#!/usr/bin/env bash
# Run the full test suite with a consolidated line-coverage gate.
#
# Phases:
#   1. Unit tests (always) — coverage gate >= 90 % global + per file
#   2. Integration tests   — against live CometBFT endpoints
#   3. E2E tests           — against live CometBFT endpoints
#
# Endpoint env vars default to Cosmos Hub (Lava). Override to target a different node:
#   COMETBFT_RPC_URL  COMETBFT_WS_URL  COMETBFT_GRPC_URL
#
# Usage: ./scripts/test.sh [additional dotnet test args]
set -euo pipefail

COMETBFT_RPC_URL="${COMETBFT_RPC_URL:-https://cosmoshub.tendermintrpc.lava.build:443}"
COMETBFT_WS_URL="${COMETBFT_WS_URL:-wss://cosmoshub.tendermintrpc.lava.build:443/websocket}"
COMETBFT_GRPC_URL="${COMETBFT_GRPC_URL:-cosmoshub.grpc.lava.build}"
export COMETBFT_RPC_URL COMETBFT_WS_URL COMETBFT_GRPC_URL

RESULTS_DIR="TestResults"
rm -rf "$RESULTS_DIR"

# ── Phase 1: unit tests + coverage gate ───────────────────────────────────────
echo "==> Phase 1: unit tests"
dotnet test CometBFT.Client.sln \
  --configuration Release \
  --collect "XPlat Code Coverage" \
  --logger trx \
  --results-directory "$RESULTS_DIR" \
  --filter "Category!=Integration&Category!=E2E" \
  "$@"

dotnet run --project tools/CometBFT.Client.CoverageGate/CometBFT.Client.CoverageGate.csproj -- "$RESULTS_DIR"

# ── Package validation: packed NuGet restore must succeed ────────────────────
echo "==> Package validation: single-package restore"
./scripts/validate-package-restore.sh

# ── Phase 2: integration tests ────────────────────────────────────────────────
echo "==> Phase 2: integration tests (${COMETBFT_RPC_URL})"
dotnet test tests/CometBFT.Client.Integration.Tests/CometBFT.Client.Integration.Tests.csproj \
  --configuration Release \
  --no-build \
  --logger trx \
  --results-directory "$RESULTS_DIR" \
  "$@"

# ── Phase 3: E2E tests ────────────────────────────────────────────────────────
echo "==> Phase 3: E2E tests"
dotnet test tests/CometBFT.Client.E2E.Tests/CometBFT.Client.E2E.Tests.csproj \
  --configuration Release \
  --no-build \
  --logger trx \
  --results-directory "$RESULTS_DIR" \
  "$@"
