#!/usr/bin/env bash
# Validate that the packed NuGet artifact restores as a single self-contained package.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="${ROOT_DIR}/artifacts/package-restore-validation"
PACKAGE_PROJECT="${ROOT_DIR}/src/CometBFT.Client.Extensions/CometBFT.Client.Extensions.csproj"
PACKAGE_ID="Rinzler78.CometBFT.Client"

VERSION="$(PACKAGE_PROJECT="${PACKAGE_PROJECT}" python3 - <<'PY'
from pathlib import Path
import os
import xml.etree.ElementTree as ET
path = Path(os.environ['PACKAGE_PROJECT'])
root = ET.parse(path).getroot()
version = None
for elem in root.iter():
    if elem.tag.endswith('Version'):
        version = elem.text
        break
print(version or '')
PY
)"

if [[ -z "${VERSION}" ]]; then
  echo "Could not determine package version from ${PACKAGE_PROJECT}." >&2
  exit 1
fi

rm -rf "${ARTIFACT_DIR}"
mkdir -p "${ARTIFACT_DIR}"

dotnet pack "${PACKAGE_PROJECT}" --configuration Release --output "${ARTIFACT_DIR}" >/dev/null

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

pushd "${WORK_DIR}" >/dev/null
export NUGET_PACKAGES="${WORK_DIR}/.packages"

dotnet new classlib -n RestoreProbe --framework net10.0 >/dev/null
cd RestoreProbe

cat > NuGet.Config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="validation-local" value="${ARTIFACT_DIR}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

dotnet add package "${PACKAGE_ID}" --version "${VERSION}" --source "${ARTIFACT_DIR}" >/dev/null

dotnet restore --configfile NuGet.Config >/dev/null

dotnet build --configuration Release --no-restore >/dev/null

echo "Validated NuGet package restore for ${PACKAGE_ID} ${VERSION}."

popd >/dev/null
