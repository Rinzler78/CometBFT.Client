#!/usr/bin/env bash
# Runs tests inside a self-contained Docker image (no bind mount for sources).
# Coverage artefacts are extracted from the container to ./coverage/ after the run.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

echo "Building Docker image cometbft-client:test ..."
docker build \
  -t cometbft-client:test \
  -f "${SCRIPT_DIR}/Dockerfile" \
  "${REPO_ROOT}"

echo "Creating container for test run ..."
CONTAINER_ID=$(docker create \
  -e COMETBFT_RPC_URL \
  -e COMETBFT_WS_URL \
  -e COMETBFT_GRPC_URL \
  cometbft-client:test \
  ./scripts/test.sh "$@")

echo "Running tests (container: ${CONTAINER_ID}) ..."
docker start --attach "${CONTAINER_ID}"
EXIT_CODE=$?

echo "Extracting coverage artefacts ..."
docker cp "${CONTAINER_ID}:/workspace/coverage" "${REPO_ROOT}/coverage" 2>/dev/null \
  && echo "Coverage artefacts written to ./coverage/" \
  || echo "No coverage directory to extract (may be expected on build-only runs)."

docker rm "${CONTAINER_ID}" > /dev/null

exit "${EXIT_CODE}"
