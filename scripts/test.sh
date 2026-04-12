#!/usr/bin/env bash
# Run tests with a consolidated line-coverage gate.
# Usage: ./scripts/test.sh [additional dotnet test args]
set -euo pipefail

RESULTS_DIR="TestResults"

rm -rf "$RESULTS_DIR"

dotnet test CometBFT.Client.sln \
  --configuration Release \
  --collect "XPlat Code Coverage" \
  --logger trx \
  --results-directory "$RESULTS_DIR" \
  --filter "Category!=Integration&Category!=E2E" \
  "$@"

dotnet run --project tools/CometBFT.Client.CoverageGate/CometBFT.Client.CoverageGate.csproj -- "$RESULTS_DIR"
