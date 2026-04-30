#!/usr/bin/env bash
# Pack and optionally publish NuGet packages.
# Usage:
#   ./scripts/publish.sh --dry-run
#   ./scripts/publish.sh --version 2.2.0 --api-key <key>
#   ./scripts/publish.sh --version 2.2.0 --api-key <key> --source <url>
# Note: chmod +x scripts/publish.sh before first run.
set -euo pipefail

API_KEY="${NUGET_API_KEY:-}"
SOURCE="https://api.nuget.org/v3/index.json"
DRY_RUN=false
VERSION_OVERRIDE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-key)  API_KEY="$2";           shift 2;;
    --source)   SOURCE="$2";            shift 2;;
    --dry-run)  DRY_RUN=true;           shift;;
    --version)  VERSION_OVERRIDE="$2";  shift 2;;
    *)          shift;;
  esac
done

# /p:_ProjectReferencePackAssets=all marks the ProjectReferences in
# CometBFT.Client.Extensions.csproj as PrivateAssets during pack so NuGet does
# not emit them as fictional package dependencies. The Core/Rest/WebSocket/Grpc
# DLLs are still bundled inside the nupkg by the IncludeProjectReferenceBuildOutputs
# target; only the nuspec dependency list is cleaned up.
PACK_ARGS=(CometBFT.Client.sln --configuration Release --output ./artifacts /p:_ProjectReferencePackAssets=all)
if [[ -n "${VERSION_OVERRIDE}" ]]; then
  PACK_ARGS+=(/p:PackageVersion="${VERSION_OVERRIDE}")
fi

dotnet pack "${PACK_ARGS[@]}"

# Assert the generated nuspec has no ghost sub-package dependencies.
python3 - "./artifacts" <<'PY'
import zipfile, glob, sys
artifact_dir = sys.argv[1]
nupkgs = [p for p in glob.glob(f'{artifact_dir}/*.nupkg') if '.symbols.' not in p]
if not nupkgs:
    sys.exit(f'No .nupkg found in {artifact_dir}')
ghosts = ['CometBFT.Client.Core', 'CometBFT.Client.Grpc', 'CometBFT.Client.Rest', 'CometBFT.Client.WebSocket']
errors = []
for nupkg in nupkgs:
    with zipfile.ZipFile(nupkg) as z:
        nuspec_names = [n for n in z.namelist() if n.endswith('.nuspec') and '.symbols.' not in n]
        if not nuspec_names:
            errors.append(f'{nupkg}: no .nuspec found inside archive')
            continue
        content = z.read(nuspec_names[0]).decode()
    found = [g for g in ghosts if f'id="{g}"' in content]
    if found:
        errors.append(f'{nupkg}: ghost dependencies {found}')
if errors:
    sys.exit('ERROR: Ghost dependencies detected:\n' + '\n'.join(errors))
print(f'Nuspec clean — no ghost dependencies ({len(nupkgs)} package(s) checked).')
PY

if [ "$DRY_RUN" = false ] && [ -n "$API_KEY" ]; then
  dotnet nuget push "./artifacts/*.nupkg" --api-key "$API_KEY" --source "$SOURCE" --skip-duplicate
else
  echo "Dry run complete. Packages in ./artifacts/. Pass --api-key or set NUGET_API_KEY to publish."
fi
