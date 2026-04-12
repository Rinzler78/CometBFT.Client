#!/usr/bin/env bash
# Pack and optionally publish NuGet packages.
# Usage:
#   ./scripts/publish.sh --dry-run
#   ./scripts/publish.sh --api-key <key>
#   ./scripts/publish.sh --api-key <key> --source <url>
# Note: chmod +x scripts/publish.sh before first run.
set -euo pipefail

API_KEY="${NUGET_API_KEY:-}"
SOURCE="https://api.nuget.org/v3/index.json"
DRY_RUN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-key) API_KEY="$2"; shift 2;;
    --source) SOURCE="$2"; shift 2;;
    --dry-run) DRY_RUN=true; shift;;
    *) shift;;
  esac
done

dotnet pack CometBFT.Client.sln --configuration Release --output ./artifacts --no-build

if [ "$DRY_RUN" = false ] && [ -n "$API_KEY" ]; then
  dotnet nuget push "./artifacts/*.nupkg" --api-key "$API_KEY" --source "$SOURCE" --skip-duplicate
else
  echo "Dry run complete. Packages in ./artifacts/. Pass --api-key or set NUGET_API_KEY to publish."
fi
