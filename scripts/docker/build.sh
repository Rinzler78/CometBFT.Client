#!/usr/bin/env bash
# Builds the project inside a self-contained Docker image (no bind mount).
# Sources are COPYed into the image at docker build time.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

echo "Building Docker image cometbft-client:build ..."
docker build \
  -t cometbft-client:build \
  -f "${SCRIPT_DIR}/Dockerfile" \
  "${REPO_ROOT}"

echo "Running build inside container ..."
docker run --rm cometbft-client:build ./scripts/build.sh "$@"
