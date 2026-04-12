#!/usr/bin/env bash
# Build the solution in Release configuration.
# Usage: ./scripts/build.sh [additional dotnet build args]
# Note: chmod +x scripts/build.sh before first run.
set -euo pipefail
exec dotnet build CometBFT.Client.sln --configuration Release "$@"
