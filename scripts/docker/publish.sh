#!/usr/bin/env bash
# Publishes NuGet packages inside a self-contained Docker image (no bind mount).
# NUGET_API_KEY is injected via environment variable — never passed as a CLI argument.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

echo "Building Docker image cometbft-client:publish ..."
docker build \
  -t cometbft-client:publish \
  -f "${SCRIPT_DIR}/Dockerfile" \
  "${REPO_ROOT}"

echo "Running publish inside container ..."
docker run --rm \
  -e NUGET_API_KEY \
  cometbft-client:publish \
  ./scripts/publish.sh "$@"
