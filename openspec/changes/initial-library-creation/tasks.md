# Tasks: Initial Creation — CometBFT.Client

Protocol source: https://github.com/cometbft/cometbft (latest stable release)

---

> **⚠️ ABSOLUTE RULE: complete Phase 0 in full before writing any C# code.**
> The repository, branches, hooks and skeleton CI must be in place first.
> Any code commit made before Phase 0 is complete is invalid.

> **⚠️ ABSOLUTE RULE: README.md and documentation are mandatory before any code commit.**
> `README.md` (minimal stub) and `CHANGELOG.md` must exist from the first code commit.
> Any `public` type or member without XML doc generates a build error (`TreatWarningsAsErrors=true`).
> No commit may introduce public code without the corresponding documentation (XML doc + README updated if necessary).

---

## Diff Specs ↔ Tasks — Alignment 2026-04-10

This section normalizes the acceptance criteria after review. When older wording below conflicts with this section, this section wins until all tasks are completed.

Current status: the bootstrap phases are materially complete and the repository is already usable. The unchecked items below represent the real remaining work required before this change can be archived.

- **Coverage policy normalized**: acceptance is now `>= 90 %` **global line coverage** and `>= 90 %` **line coverage per source file**. Older wording about branch/method/per-assembly thresholds is superseded.
- **Coverage report upload removed**: generating coverage output for local/CI validation remains required; uploading the report as a CI artifact is **not** required.
- **Integration scope expanded explicitly**: tasks previously covered mostly REST integration. Missing WebSocket and gRPC integration tasks are added below.
- **E2E scope kept mandatory**: REST, WebSocket, and gRPC E2E flows remain required and are called out explicitly in CI.
- **Transport completeness gap made explicit**: the spec requires complete public transport coverage for REST, WebSocket, and gRPC. Any remaining delta must be tracked explicitly in client APIs, tests, demos, CI, and final validation.
- **Demo gaps made explicit**: REST demo must include `GetBlockResultsAsync`; WebSocket demo must expose `NewBlockHeader` and `ValidatorSetUpdates`; gRPC demo must include the missing dashboard elements required by spec.
- **Docker hardening kept visible**: self-contained Docker wrappers without bind mounts remain a valid follow-up expectation from the original feature branch, but are not claimed as implemented until the scripts actually move away from the current bind-mount model.
- **gRPC completeness gap made explicit**: for CometBFT `v0.38.9`, the public gRPC surface centers on `BroadcastAPI` (`Ping`, `BroadcastTx`). The remaining work is protocol-parity work: vendored proto alignment with upstream, full response-shape mapping, and matching tests/demo/docs.

## Diff Specs ↔ Tasks — Alignment 2026-04-14

- **Unsafe endpoints removed from test scope**: `DialSeeds` and `DialPeers` require a node started with `--rpc.unsafe=true`. No public testnet node (including Cosmos Hub via Lava) exposes this flag. The REST client implementation (`DialSeedsAsync`, `DialPeersAsync`) is retained in the library. All live tests (integration, E2E) and the dedicated `unsafe-validation` CI job have been deleted. Unit tests via WireMock.Net (7.2.7) remain as the only automated coverage. Tasks 7.6.11, 7.8.10, 9.12, and V.14 updated accordingly.
- **CI test deduplication fixed**: the `build-and-test` job was calling `./scripts/test.sh --no-build`, which runs all three phases (unit + integration + E2E). Integration and E2E tests were therefore executed twice. The job now runs only the unit-test + coverage-gate commands inline; integration and E2E remain exclusively in their dedicated CI jobs. `scripts/test.sh` is unchanged as the local full-pipeline script. See task 9.15.
- **scripts/test.sh default endpoints**: endpoint env vars have built-in defaults (`${VAR:-<url>}`). No manual export needed for local runs against the public testnet.
- **Docker build hardened**: `.dockerignore` excludes macOS `obj/bin` artifacts to prevent exec-format errors on Linux. `-maxcpucount:1` added to the Dockerfile build step to prevent MSBuild parallel race conditions on fresh Linux builds.
- **cspell updated to v10.0.0**: the `v8.19.4` tag was unavailable in the pre-commit cache; updated via `pre-commit autoupdate`.
- **Git hooks installed**: `pre-commit install` succeeded after unsetting `core.hooksPath`. All hooks (format, detect-secrets, cspell, commit-msg, pre-push with coverage gate) are now active on the local repo.

## Phase 0 — Repo, Git Flow and Hooks (PREREQUISITE — BEFORE ANY CODE)

### 0.1 Repository creation
- [x] 0.1.1 Create the `~/Projects/CometBFT.Client/` directory
- [x] 0.1.2 `git init -b master` — **the main branch is `master`, not `main`**
- [x] 0.1.3 Verify: `git branch` must display `* master` (not `main`)
- [x] 0.1.4 Create `.gitignore` (.NET standard + NuGet artifacts + coverage/ + `.worktrees/`)
- [x] 0.1.5 Create the GitHub repository `Rinzler78/CometBFT.Client` (public) — set the default branch to `master` in Settings → Branches
- [x] 0.1.6 `git remote add origin https://github.com/Rinzler78/CometBFT.Client.git`
- [x] 0.1.7 Initial commit: `chore: initial repository setup`
- [x] 0.1.8 Push to `master`: `git push -u origin master`

### 0.2 Worktrees
- [x] 0.2.1 Create the `.worktrees/` directory at the repository root — **all git worktrees must be created in this directory**
- [x] 0.2.2 The `.worktrees/` directory is already in `.gitignore` (see 0.1.4)
- [x] 0.2.3 Naming convention: `git worktree add .worktrees/<branch-name> <branch-name>`
- [x] 0.2.4 Never create a worktree outside `.worktrees/`

### 0.3 Git Flow
- [x] 0.3.1 Create `.gitflow`: master/develop/feature/release/hotfix/bugfix, versiontag = v
- [x] 0.3.2 Run `git flow init -d` — creates the `develop` branch
- [x] 0.3.3 Push `develop` to origin
- [x] 0.3.4 Commit: `chore: configure git flow`

### 0.4 Git hooks
- [x] 0.4.1 Create `.pre-commit-config.yaml` (dotnet format + detect-secrets)
- [x] 0.4.2 Create `.git/hooks/commit-msg` — conventional commits
- [x] 0.4.3 Create `.git/hooks/pre-push` — block direct push to `master` and `develop`
- [x] 0.4.4 Make hooks executable
- [x] 0.4.5 `pre-commit install` to install pre-commit hooks
- [x] 0.4.6 Commit: `chore: add git hooks and pre-commit config`
- [x] 0.4.7 Extend `.git/hooks/pre-push` — run the repo test suite and verify **line** coverage `>= 90 %` globally and `>= 90 %` per file before any push:
  ```bash
  # In .git/hooks/pre-push (added after the master/develop protection block)
  echo "Running coverage gate before push..."
  ./scripts/test.sh
  if [ $? -ne 0 ]; then
    echo "ERROR: Coverage gate failed — fix coverage before pushing."
    exit 1
  fi
  ```
- [x] 0.4.8 Add a language detection hook — **English only** across the entire repository:
  > **Scope**: C# source files (`.cs`), XML documentation, Markdown files (`.md`), bash scripts (`.sh`), YAML (`.yml`, `.yaml`), commit messages.
  > **Rule**: all human-readable text (comments, XML doc `<summary>`, variable names, specs, README, CHANGELOG, commit messages) must be written in English. No other language is permitted.
  - Tool: `cspell` (Code Spell Checker — `streetsidesoftware/cspell-cli`)
  - Create `.cspell.json` at the root with:
    ```json
    {
      "version": "0.2",
      "language": "en",
      "dictionaries": ["en_US", "en-gb"],
      "ignorePaths": [
        "coverage/**",
        "artifacts/**",
        ".worktrees/**",
        "**/*.lock",
        "**/packages.lock.json"
      ],
      "words": [
        "CometBFT", "cometbft", "grpc", "gRPC", "protobuf", "proto",
        "Rinzler", "NuGet", "nuget", "dotnet", "csproj", "slnf",
        "Polly", "WireMock", "Coverlet", "NSubstitute", "xunit",
        "Spectre", "DocFX", "OpenAPI", "swagger",
        "async", "await", "nullable", "readonly", "init",
        "testnet", "mainnet", "lcd", "rpc", "abci",
        "Osmosis", "osmosis", "Cosmos", "cosmos",
        "dependabot", "gitflow", "editorconfig",
        "cspell", "warnaserror", "analyzers"
      ],
      "overrides": [
        {
          "filename": "**/*.cs",
          "words": ["Tx", "tx", "TxHash", "RawLog", "GasUsed", "GasWanted"]
        }
      ]
    }
    ```
  - Add the following entry to `.pre-commit-config.yaml`:
    ```yaml
    - repo: https://github.com/streetsidesoftware/cspell-cli
      rev: v8.19.4
      hooks:
        - id: cspell
          name: English-only language check
          args: [--no-progress, --no-summary, --show-context]
    ```
  - Verify that the hook blocks a commit containing a non-English word in a C# comment or a Markdown file
  - Every legitimate technical exception (acronym, protocol proper name, domain term) must be added explicitly in the `words` section of `.cspell.json` with a justification comment in the PR
  - Recurring false positives are managed in `.cspell.json` (never via `// cspell:disable` without an inline justification)
- [x] 0.4.9 Add a CI step in `.github/workflows/ci.yml` to run `cspell` across the entire repository — CI fails if a non-English word is detected in a production or documentation file

### 0.5 GitHub branch protection
- [x] 0.5.1 Create `.github/branch-protection.md`
- [x] 0.5.2 Configure rules on GitHub (Settings → Branches → Branch protection rules) ← via `gh api`
- [x] 0.5.3 Commit: `docs: add branch protection documentation`

### 0.6 Development feature branch
- [x] 0.6.1 Create the working branch: `feature/initial-library-creation`
- [x] 0.6.2 **Verify that all hooks are working** (dotnet format + detect-secrets pass)
- [x] 0.6.3 **All subsequent development (Phases 1–9) takes place on this branch or feature/** branches

> **Phase 0 complete. Development can begin.**

### 0.7 Development workflow conventions

- [x] 0.7.1 **PR merge strategy**: squash-merge into `develop`; merge commit (no squash) into `master`
- [x] 0.7.2 **Release branch lifecycle**:
  1. Create `release/vX.Y.Z` from `develop`
  2. Run full test suite (`./scripts/test.sh`) — must pass
  3. Run `./scripts/publish.sh --dry-run` — must produce one `.nupkg`
  4. Update `CHANGELOG.md` (move `[Unreleased]` → `[vX.Y.Z] - YYYY-MM-DD`)
  5. Bump `<Version>` in `Directory.Build.props`
  6. PR `release/vX.Y.Z` → `master`; require CI green + 1 approving review
  7. Merge to `master`; tag `vX.Y.Z`; CI `publish.yml` triggers
  8. Back-merge `master` → `develop`
- [x] 0.7.3 **SemVer bump rules**:
  - **Major**: breaking public API change OR protocol major version upgrade (e.g., CometBFT v0.38 → v1.x)
  - **Minor**: new endpoint/transport/DI extension added (backward-compatible)
  - **Patch**: bug fix, dependency update, documentation only
- [x] 0.7.4 **PR title** must follow conventional commit format: `feat(rest): add GetBlockResultsAsync`

---

## Phase 1 — Scaffold

### 1.1 Repo & solution
- [x] 1.1.1 Create `CometBFT.Client.sln` — contains all projects (src + tests + samples)
- [x] 1.1.2 Create `CometBFT.Client.src.slnf` — solution filter src/** + demos
- [x] 1.1.3 Create `CometBFT.Client.tests.slnf` — solution filter tests/**
- [x] 1.1.4 Create `global.json` (pin SDK .NET 10.0.100)
- [x] 1.1.5 Create `Directory.Build.props` — LangVersion, Nullable, TreatWarningsAsErrors, `<ProtocolVersion>` v0.38.9, net10.0
- [x] 1.1.5b Verify that `Directory.Build.props` contains for src projects:
  ```xml
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningLevel>9999</WarningLevel>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisMode>AllEnabledByDefault</AnalysisMode>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  ```

> **Note**: add the following to the `Directory.Build.props` default property group for src projects:
> ```xml
> <!-- Only Extensions is published as NuGet -->
> <IsPackable>false</IsPackable>
> <!-- Override in CometBFT.Client.Extensions.csproj: <IsPackable>true</IsPackable> -->
> ```

- [x] 1.1.5c Verify the following additional coding conventions are enforced in `Directory.Build.props` and documented in `CONTRIBUTING.md`:
  - `ILogger<T>` injected via DI on all client classes — `Console.WriteLine` is forbidden in library code
  - `ConfigureAwait(false)` on all `await` expressions inside library code (not required in test or sample projects)
  - All public client types implement `IAsyncDisposable`; `Dispose()` calls `DisposeAsync().AsTask().GetAwaiter().GetResult()` as sync fallback
  - Polly default policy values: 3 retries, exponential backoff (`TimeSpan.FromMilliseconds(100) * Math.Pow(2, retryAttempt)`), circuit breaker (5 failures within 30 s, 1 min open duration)
  - `CancellationToken` propagated to all internal async helpers — not just public surface
  - `decimal` type for all monetary/price/amount fields (never `double` or `float`)

### 1.2 Quality configuration
- [x] 1.2.1 Create `.editorconfig` (indent_size=4, charset=utf-8, end_of_line=lf)
- [x] 1.2.2 Create `.gitignore` (.NET standard)
- [x] 1.2.3 `Directory.Build.props` Tests section (Coverlet, threshold 90 %)
- [x] 1.2.3b Verify the coverage strategy in `Directory.Build.props` and/or validation scripts:
  ```xml
  <!-- Applied to all Tests/* projects -->
  <Threshold>90</Threshold>
  ```
  > The effective acceptance rule is: `>= 90 %` global line and `>= 90 %` per file line.

### 1.3 Source projects
- [x] 1.3.1 `src/CometBFT.Client.Core/`
- [x] 1.3.2 `src/CometBFT.Client.Rest/`
- [x] 1.3.3 `src/CometBFT.Client.Grpc/`
- [x] 1.3.4 `src/CometBFT.Client.WebSocket/`
- [x] 1.3.5 `src/CometBFT.Client.Extensions/`

> **Single NuGet package rule**: Only `src/CometBFT.Client.Extensions/` is packable — it produces the single published package `Rinzler78.CometBFT.Client`. All other src projects (`Core`, `Rest`, `Grpc`, `WebSocket`) set `<IsPackable>false</IsPackable>` and are bundled as internal project references. Users install one package only.

### 1.4 Test projects
- [x] 1.4.1 `tests/CometBFT.Client.Core.Tests/`
- [x] 1.4.2 `tests/CometBFT.Client.Rest.Tests/` (WireMock.Net)
- [x] 1.4.3 `tests/CometBFT.Client.Grpc.Tests/` (Grpc.AspNetCore)
- [x] 1.4.4 `tests/CometBFT.Client.WebSocket.Tests/` (NSubstitute)
- [x] 1.4.5 `tests/CometBFT.Client.Integration.Tests/`
- [x] 1.4.6 Create `tests/CometBFT.Client.E2E.Tests/` — end-to-end tests (start-to-finish demo against public testnet, `[Trait("Category","E2E")]`)

### 1.5 Ancillary projects
- [x] 1.5.1 Create `samples/CometBFT.Client.Demo.Rest/`
- [x] 1.5.2 Create `samples/CometBFT.Client.Demo.WebSocket/`
- [x] 1.5.3 Create `samples/CometBFT.Client.Demo.Grpc/`
- [x] 1.5.4 Create `docs/` (placeholder DocFX config)
- [x] 1.5.5 Add all projects to `.sln`, update `.src.slnf` and `.tests.slnf`
- [x] 1.5.6 Create minimal `README.md` stub (title + description + CI badge + Installation placeholder section) — **mandatory before any code commit**
- [x] 1.5.7 Create minimal `CHANGELOG.md` stub (Keep-a-Changelog format, version `[Unreleased]`) — **mandatory before any code commit**

---

## Phase 2 — Git Flow

### 2.1 Configuration
- [x] 2.1.1 Create `.gitflow`
- [x] 2.1.2 Run `git flow init -d` to initialize the branches

### 2.2 Pre-commit hooks
- [x] 2.2.1 Create `.pre-commit-config.yaml`
- [x] 2.2.2 Hook `commit-msg` — conventional commits
- [x] 2.2.3 Hook `pre-push` — block direct push to `master` and `develop`

### 2.3 Protection documentation
- [x] 2.3.1 Create `.github/branch-protection.md`

---

## Phase 3 — Scripts bash

### 3.1 build.sh
- [x] 3.1.1 Create `scripts/build.sh`

### 3.2 test.sh
- [x] 3.2.1 Create `scripts/test.sh`

### 3.3 publish.sh
- [x] 3.3.1 Create `scripts/publish.sh`

### 3.4 Docker scripts — `scripts/docker/`
- [x] 3.4.1 Create `scripts/docker/Dockerfile` (FROM mcr.microsoft.com/dotnet/sdk:10.0)
- [x] 3.4.2 Create `scripts/docker/docker-compose.yml`
- [x] 3.4.3 Create `scripts/docker/build.sh` — delegates to `./scripts/build.sh` inside the container
- [x] 3.4.4 Create `scripts/docker/test.sh` — delegates to `./scripts/test.sh` inside the container
- [x] 3.4.5 Create `scripts/docker/publish.sh` — `NUGET_API_KEY` via env, never as an argument
- [x] 3.4.6 `scripts/publish.sh` reads `NUGET_API_KEY` from the env if `--api-key` is not passed
- [x] 3.4.7 `chmod +x scripts/docker/*.sh`
- [x] 3.4.8 Document in README: local vs Docker usage, passing `NUGET_API_KEY`
- [x] 3.4.9 Harden `scripts/docker/build.sh` to run the build in a self-contained image without bind mounts, while preserving delegation to `./scripts/build.sh`
- [x] 3.4.10 Harden `scripts/docker/test.sh` to run tests in a self-contained image without bind mounts and cleanly retrieve coverage artifacts
- [x] 3.4.11 Harden `scripts/docker/publish.sh` to run `./scripts/publish.sh` in a self-contained image without bind mounts while continuing to inject `NUGET_API_KEY` via environment
- [x] 3.4.12 Align `scripts/docker/Dockerfile` and `scripts/docker/docker-compose.yml` to this self-contained mode without bind mounts

### 3.5 Demo scripts — local and Docker

> **Principle**: each local script builds + runs the targeted demo program. The Docker script delegates to the local script inside the container and forwards endpoint env vars.

Structure cible :
```
scripts/
├── demo-rest.sh          ← build + run Demo.Rest
├── demo-ws.sh            ← build + run Demo.WebSocket
├── demo-grpc.sh          ← build + run Demo.Grpc
└── docker/
    ├── demo-rest.sh      ← docker run ... ./scripts/demo-rest.sh "$@"
    ├── demo-ws.sh
    └── demo-grpc.sh
```

- [x] 3.5.1 Create `scripts/demo-rest.sh` (testnet default if env var absent):
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  COMETBFT_RPC_URL="${COMETBFT_RPC_URL:-https://cosmoshub.cometbftrpc.lava.build:443}"
  export COMETBFT_RPC_URL
  dotnet run --project samples/CometBFT.Client.Demo.Rest \
    --configuration Release "$@"
  ```

- [x] 3.5.2 Create `scripts/demo-ws.sh` (testnet default if env var absent):
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  COMETBFT_WS_URL="${COMETBFT_WS_URL:-wss://cosmoshub.cometbftrpc.lava.build:443/websocket}"
  export COMETBFT_WS_URL
  dotnet run --project samples/CometBFT.Client.Demo.WebSocket \
    --configuration Release "$@"
  ```

- [x] 3.5.3 Create `scripts/demo-grpc.sh` (testnet default if env var absent):
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  COMETBFT_GRPC_URL="${COMETBFT_GRPC_URL:-cosmoshub.grpc.lava.build}"
  export COMETBFT_GRPC_URL
  dotnet run --project samples/CometBFT.Client.Demo.Grpc \
    --configuration Release "$@"
  ```

- [x] 3.5.4 Create `scripts/docker/demo-rest.sh` — forward `COMETBFT_RPC_URL`:
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  docker run --rm \
    -v "$(pwd):/workspace" \
    -w /workspace \
    -e COMETBFT_RPC_URL \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    ./scripts/demo-rest.sh "$@"
  ```

- [x] 3.5.5 Create `scripts/docker/demo-ws.sh` — forward `COMETBFT_WS_URL`:
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  docker run --rm \
    -v "$(pwd):/workspace" \
    -w /workspace \
    -e COMETBFT_WS_URL \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    ./scripts/demo-ws.sh "$@"
  ```

- [x] 3.5.6 Create `scripts/docker/demo-grpc.sh` — forward `COMETBFT_GRPC_URL`:
  ```bash
  #!/usr/bin/env bash
  set -euo pipefail
  docker run --rm \
    -v "$(pwd):/workspace" \
    -w /workspace \
    -e COMETBFT_GRPC_URL \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    ./scripts/demo-grpc.sh "$@"
  ```

- [x] 3.5.7 `chmod +x scripts/demo-*.sh scripts/docker/demo-*.sh`
- [x] 3.5.8 Document in README: how to run each demo locally and in Docker
- [x] 3.5.9 **Smoke test zero-config**: run `./scripts/demo-rest.sh`, `./scripts/demo-ws.sh`, `./scripts/demo-grpc.sh` **without any env var or arg** — verify that each script starts and connects to the default testnet without a missing-argument error
- [x] 3.5.10 Harden `scripts/docker/demo-rest.sh`, `scripts/docker/demo-ws.sh` and `scripts/docker/demo-grpc.sh` to delegate to local scripts in a self-contained image without bind mounts

---

## Phase 4 — Domain Core

### 4.1 Immutable types
- [x] 4.1.1 `Block.cs`
- [x] 4.1.2 `BlockHeader.cs`
- [x] 4.1.3 `TxResult.cs`
- [x] 4.1.4 `Event.cs`, `Attribute.cs`
- [x] 4.1.5 `NodeInfo.cs`, `SyncInfo.cs`, `Validator.cs`
- [x] 4.1.6 `BroadcastTxResult.cs`
- [x] 4.1.7 `Vote.cs`
- [x] 4.1.8 Audit concepts exposed across multiple transports and record a matrix business concept → shared `Core.Domain` type → consumer transports
- [x] 4.1.9 Remove any remaining divergence where multiple transports expose duplicate or incompatible domain objects for the same business concept

### 4.2 Per-service interfaces
- [x] 4.2.1 `ICometBftRestClient.cs`
- [x] 4.2.2 `IHealthService`, `IStatusService`, `IBlockService`, `ITxService`, `IValidatorService`, `IAbciService`
- [x] 4.2.3 `ICometBftWebSocketClient.cs`
- [x] 4.2.4 `ICometBftGrpcClient.cs`
- [x] 4.2.5 Define and document cross-cutting capabilities shared across transports with compatible signatures when the protocol semantics are the same
- [x] 4.2.6 Align `ICometBftRestClient`, `ICometBftWebSocketClient` and `ICometBftGrpcClient` on the same `Core.Domain` objects for every shared business concept (block, header, tx result, validator set, broadcast result, etc.)
- [x] 4.2.7 Explicitly document interface gaps that remain transport-specific and justify why they cannot converge further

### 4.3 Options and exceptions
- [x] 4.3.1 `CometBftRestOptions`, `CometBftWebSocketOptions`, `CometBftGrpcOptions`
- [x] 4.3.2 `CometBftClientException`, `CometBftRestException`, `CometBftWebSocketException`, `CometBftGrpcException`

---

## Phase 5 — Clients (all official endpoints)

### 5.1 REST client
- [x] 5.1.1 `GetHealthAsync`, `GetStatusAsync`
- [x] 5.1.2 `GetBlockAsync`, `GetBlockByHashAsync`, `GetBlockResultsAsync`
- [x] 5.1.3 `GetValidatorsAsync`
- [x] 5.1.4 `GetTxAsync`, `SearchTxAsync`
- [x] 5.1.5 `BroadcastTxSyncAsync`, `BroadcastTxAsync`, `BroadcastTxCommitAsync` (POST JSON-RPC)
- [x] 5.1.6 `GetAbciInfoAsync`, `AbciQueryAsync`
- [x] 5.1.7 Polly: exponential retry (3 attempts) + circuit breaker + jitter
- [x] 5.1.8 Add missing public REST endpoints: `check_tx`, `net_info`, `blockchain`, `header`, `header_by_hash`, `commit`
- [x] 5.1.9 Add missing public REST endpoints: `genesis`, `genesis_chunked`, `dump_consensus_state`, `consensus_state`, `consensus_params`
- [x] 5.1.10 Add missing public REST endpoints: `unconfirmed_txs`, `num_unconfirmed_txs`, `block_search`, `broadcast_evidence`
- [x] 5.1.11 Add unit and integration tests corresponding to each added REST endpoint
- [x] 5.1.12 Audit the targeted CometBFT OpenAPI and record a complete matrix: public REST endpoint → .NET method → unit/integration/E2E tests → dashboard panel or demo usage
- [x] 5.1.13 Close any remaining REST delta revealed by this audit and align README, OpenSpec, and final validation on the complete matrix
- [x] 5.1.14 Implement the still-missing `Unsafe` REST endpoints: `dial_seeds`, `dial_peers`
- [x] 5.1.15 Add the types, argument validations, and mappings required for `dial_seeds` and `dial_peers`, including the `persistent`, `unconditional`, and `private` options

### 5.2 WebSocket client
- [x] 5.2.1 `CometBftWebSocketClient` with `Websocket.Client 5.0.0`
- [x] 5.2.2 `NewBlock`, `NewBlockHeader` subscription
- [x] 5.2.3 `Tx`, `Vote`, `ValidatorSetUpdates` subscription
- [x] 5.2.4 Automatic reconnection
- [x] 5.2.5 Audit all public WebSocket events, subscriptions, and calls of the targeted protocol and record the event/call → .NET API → tests → demo matrix
- [x] 5.2.6 Extend `ICometBftWebSocketClient` and `CometBftWebSocketClient` to cover any missing public call or subscription revealed by this audit
- [x] 5.2.7 Add or complete the domain mappings and exceptions required for any missing public WebSocket capability

### 5.3 gRPC client
- [x] 5.3.1 Proto `cometbft/rpc/grpc/grpc.proto` downloaded
- [x] 5.3.2 Proto compilation via `Grpc.Tools`
- [x] 5.3.3 `CometBftGrpcClient`: `PingAsync`, `BroadcastTxAsync`
- [x] 5.3.4 Polly on the gRPC channel
- [x] 5.3.5 Audit all public gRPC services and methods exposed by the targeted CometBFT release and record the proto source → expected .NET API matrix
- [x] 5.3.6 Align the local vendored proto to the exact upstream proto of `v0.38.9` (`proto/cometbft/rpc/grpc/types.proto`), including `ResponseBroadcastTx.tx_result`
- [x] 5.3.7 Extend `ICometBftGrpcClient` if needed to exactly reflect the audited public gRPC surface, with `CancellationToken` on every method
- [x] 5.3.8 Extend `CometBftGrpcClient` and its domain mappings to fully represent `ResponseBroadcastTx` and any other audited public gRPC response
- [x] 5.3.9 Add or complete the records/options/exceptions needed to cleanly represent `check_tx`, `tx_result`, and other exposed public gRPC shapes
- [x] 5.3.10 Add unit tests covering each exposed public gRPC method, including full response deserialization and typed error paths
- [x] 5.3.11 Add live integration tests covering each public gRPC method reachable on the validated endpoint
- [x] 5.3.12 Add E2E tests covering the public gRPC flows actually supported by the client with the expected complete responses

### 5.4 Encoding/decoding optimization (mandatory — .NET 10)
- [x] 5.4.1 All REST/WebSocket JSON schemas typed as immutable `record` in Core
- [x] 5.4.2 Zero `JsonElement` / `object` / `dynamic` / `Dictionary<string,object>` in domain records
- [x] 5.4.3 WebSocket `EventHandler<T>` expose typed records (`Block`, `TxResult`, `Vote`, `BlockHeader`, `IReadOnlyList<Validator>`)
- [x] 5.4.4 `CometBftJsonContext : JsonSerializerContext` with `[JsonSerializable]` for all REST types (top-level + generic envelopes)
- [x] 5.4.5 All `JsonSerializer.DeserializeAsync<T>` calls use `CometBftJsonContext.Default.Options`
- [x] 5.4.6 `HttpCompletionOption.ResponseHeadersRead` on all `HttpClient.GetAsync` calls
- [x] 5.4.7 `ArrayPool<byte>.Shared` for HTTP read buffers (payloads ≥ 4 KB) — closed as N/A, the client deserializes directly from the response stream without a manual buffering loop
- [x] 5.4.8 `Microsoft.IO.RecyclableMemoryStream` — closed as N/A, there is no `MemoryStream` allocation in the REST hot path
- [x] 5.4.9 Zero `JsonSerializer` calls using the default overload (reflection) — POST uses `CometBftJsonContext.Default.JsonRpcBroadcastRequest`
- [x] 5.4.10 Zero typed `JsonElement` / `object` / `dynamic` properties in domain records
- [x] 5.4.11 Zero `Newtonsoft.Json` imports in src projects
- [x] 5.4.12 `SocketsHttpHandler` with `PooledConnectionLifetime = 2 min` — deferred

---

## Phase 6 — DI Extensions
- [x] 6.1 `AddCometBftRest(this IServiceCollection, Action<CometBftRestOptions>)`
- [x] 6.2 `AddCometBftWebSocket(this IServiceCollection, Action<CometBftWebSocketOptions>)`
- [x] 6.3 `AddCometBftGrpc(this IServiceCollection, Action<CometBftGrpcOptions>)`

---

## Phase 7 — Tests ≥ 90 % — Unit, Integration, E2E

### 7.0 Testing conventions (apply across all test projects)

- [x] 7.0.1 Test method naming convention: `MethodName_WhenScenario_ShouldExpectedResult` (e.g., `GetBlockAsync_WhenNodeReturns200_ShouldReturnTypedBlock`)
- [x] 7.0.2 Mock strategy:
  - **Mock** (NSubstitute): external I/O boundaries — HTTP channel, gRPC channel, WebSocket transport
  - **Real**: all domain `record` types, `JsonSerializerContext`, pagination logic, option validation
  - **WireMock.Net**: HTTP layer integration (REST client tests)
  - **Grpc.AspNetCore test server**: in-process gRPC server (gRPC client tests)
  - **Never mock** domain logic or serialization paths — these must be tested with real types
- [x] 7.0.3 One test project per src assembly: `Core.Tests`, `Rest.Tests`, `Grpc.Tests`, `WebSocket.Tests`, `Integration.Tests`, `E2E.Tests`
- [x] 7.0.4 Test projects set `<IsPackable>false</IsPackable>` and `<GenerateDocumentationFile>false</GenerateDocumentationFile>`

### 7.1 Core unit tests
- [x] 7.1.1 Options tests (constructors, default values)

### 7.2 REST unit tests (WireMock.Net)
- [x] 7.2.1 WireMock fixture for: health, status, block, block (height), validators, broadcast_tx_sync, abci_info, RPC error
- [x] 7.2.2 Success tests (200 OK + correct deserialization)
- [x] 7.2.3 Error tests (JSON-RPC error → CometBftRestException)
- [x] 7.2.4 Polly retry tests (WireMock simulating 2 errors then success)
- [x] 7.2.5 DI registration tests
- [x] 7.2.6 Extend the REST unit suite to exhaustively cover every public endpoint in the validated OpenAPI matrix
- [x] 7.2.7 Add REST unit tests explicitly covering `dial_seeds` and `dial_peers`, including list encoding and boolean options

### 7.3 WebSocket unit tests (NSubstitute)
- [x] 7.3.1 Constructor + connection tests (null options, invalid URL)
- [x] 7.3.2 Subscribe/unsubscribe without connection → CometBftWebSocketException
- [x] 7.3.3 Subscribe/unsubscribe events tests (NewBlock, Tx, Vote, BlockHeader)
- [x] 7.3.4 DisposeAsync idempotent tests
- [x] 7.3.5 Extend the WebSocket unit suite to exhaustively cover all public events, subscriptions, and calls exposed after the protocol audit

### 7.4 gRPC unit tests (NSubstitute)
- [x] 7.4.1 `PingAsync` → true/false/exception
- [x] 7.4.2 `BroadcastTxAsync` → result / null / RpcException → CometBftGrpcException
- [x] 7.4.3 `DisposeAsync` idempotent, `ObjectDisposedException` after dispose
- [x] 7.4.4 Extend the unit suite to cover all audited public gRPC methods and all useful fields of their responses

### 7.5 Extensions unit tests
- [x] 7.5.1 Verify DI registrations (AddCometBftRest/WebSocket/Grpc)

### 7.6 Live integration tests
- [x] 7.6.1 Centralize default testnet endpoints in a dedicated helper/config for integration tests
- [x] 7.6.2 Consistent skip pattern for `COMETBFT_RPC_URL`, `COMETBFT_WS_URL`, `COMETBFT_GRPC_URL`
- [x] 7.6.3 GetHealth, GetStatus, GetBlock
- [x] 7.6.4 GetValidators, GetAbciInfo
- [x] 7.6.5 WebSocket integration: connection, subscription, receipt of at least one typed event, clean disconnection
- [x] 7.6.6 gRPC integration: resolution via DI, `PingAsync`, and validation of the nominal path or expected error
- [x] 7.6.7 Run the full integration suite against the documented testnet and record the actual result
- [x] 7.6.8 Extend live REST integrations to exhaustively cover the validated public endpoint matrix
- [x] 7.6.9 Extend live WebSocket integrations to exhaustively cover the public events, subscriptions, and calls exposed by `ICometBftWebSocketClient`
- [x] 7.6.10 Extend live gRPC integrations to cover all public methods exposed by `ICometBftGrpcClient` and validate the complete shape of gRPC responses
- [x] 7.6.11 ~~Add a REST validation strategy for `Unsafe` endpoints (`dial_seeds`, `dial_peers`) against a controlled node where unsafe RPC is enabled, separate from public endpoint validations~~ **Deleted (2026-04-14)**: no public node with `--rpc.unsafe=true` is available; live integration tests removed. Unit coverage via WireMock.Net (7.2.7) is the only remaining automated test path for these endpoints.

### 7.7 Global and per-file coverage — mandatory gate

> **Rule**: 90 % minimum **global (line)** AND 90 % minimum **per file (line)**.
> The pre-push hook blocks any push if the gate fails.

- [x] 7.7.1 Fix `./scripts/test.sh` so that it runs tests without MSBuild parameter errors
- [x] 7.7.2 Produce a consolidated machine-readable coverage output for all test projects
- [x] 7.7.3 Add an automated validation that fails if **global line** coverage < 90 %
- [x] 7.7.4 Add an automated validation that fails if any **source file** is < 90 % line coverage
- [x] 7.7.5 Wire this validation into `./scripts/test.sh`
- [x] 7.7.6 Wire this validation into `.git/hooks/pre-push`
- [x] 7.7.7 Wire this validation into `.github/workflows/ci.yml`
- [x] 7.7.8 Generate a usable local report for developer diagnostics; report upload is not required

### 7.8 E2E tests (end-to-end — against public testnet)

> **Trait**: `[Trait("Category","E2E")]` — skipped if endpoint env vars are absent.
> E2E tests execute a full flow (DI client init → real calls → deserialization → business assertions).

- [x] 7.8.1 Create `tests/CometBFT.Client.E2E.Tests/` if absent
- [x] 7.8.2 Full REST flow: `AddCometBftRest` → `GetHealthAsync` → `GetStatusAsync` → `GetBlockAsync` → `GetValidatorsAsync` — verify end-to-end deserialization
- [x] 7.8.3 Full WebSocket flow: connection → `NewBlock` subscription → receipt of ≥ 1 typed `Block` event → clean disconnection
- [x] 7.8.4 Full gRPC flow: `AddCometBftGrpc` → `PingAsync` → `BroadcastTxAsync` (empty tx, expected error) — verify exception handling
- [x] 7.8.5 Automatic skip if `COMETBFT_RPC_URL` / `COMETBFT_WS_URL` / `COMETBFT_GRPC_URL` are absent
- [x] 7.8.6 CI E2E gate: separate step in `ci.yml` run with testnet env vars
- [x] 7.8.7 Extend REST E2E scenarios to reflect full coverage of the public endpoints actually exposed by `ICometBftRestClient`
- [x] 7.8.10 ~~Add a dedicated REST E2E flow for `Unsafe` endpoints on a controlled environment when those routes are enabled~~ **Deleted (2026-04-14)**: E2E test removed — no public node with `--rpc.unsafe=true`.
- [x] 7.8.8 Extend WebSocket E2E scenarios to reflect full coverage of the public events, subscriptions, and calls exposed by `ICometBftWebSocketClient`
- [x] 7.8.9 Extend gRPC E2E scenarios to cover all audited public gRPC methods and the complete responses actually mapped

---

## Phase 8 — Documentation and Demos

### 8.1 Documentation
- [x] 8.1.1 XML doc on all `public` types and members (enforced by TreatWarningsAsErrors)
- [x] 8.1.2 `README.md`: badges, installation, quickstart, cometbft link + protocol version
- [x] 8.1.3 `CHANGELOG.md` (Keep-a-Changelog format)
- [x] 8.1.4 Configure DocFX in `docs/` (docfx.json)

### 8.2 REST demo (`samples/CometBFT.Client.Demo.Rest/`)
- [x] 8.2.1 Create net10.0 console project
- [x] 8.2.2 Dependencies: `Spectre.Console`, `Microsoft.Extensions.Hosting`
- [x] 8.2.3 Config: `COMETBFT_RPC_URL` env var or `--rpc-url` CLI arg
- [x] 8.2.3b Verify that resolution respects the CLI arg > env var > testnet default priority:
  ```csharp
  var rpcUrl = args.GetOption("--rpc-url")
      ?? Environment.GetEnvironmentVariable("COMETBFT_RPC_URL")
      ?? "https://cosmoshub.cometbftrpc.lava.build:443";
  ```
  > No exception, no exit if env var is absent.
- [x] 8.2.4 Register `AddCometBftRest` via DI
- [x] 8.2.5 Refresh loop every 10 s: GetHealth, GetStatus, GetBlock, GetValidators, GetAbciInfo
- [x] 8.2.6 Spectre.Console `Live` layout: Header, Health/Status, Latest Block, Validators, ABCI Info, Log
- [x] 8.2.7 Each panel displays call timestamp and latency in ms
- [x] 8.2.8 Add `GetBlockResultsAsync` to the refresh cycle and REST demo display
- [x] 8.2.9 Extend the REST demo to reflect the complete matrix of public REST endpoints considered mandatory for the library's operational visibility
- [x] 8.2.10 Extend the REST demo to make all methods of `ICometBftRestClient` accessible, including `Unsafe` capabilities behind an explicit mode or warning

### 8.3 WebSocket demo (`samples/CometBFT.Client.Demo.WebSocket/`)
- [x] 8.3.1 Create net10.0 console project
- [x] 8.3.2 Dependencies: `Spectre.Console`, `Microsoft.Extensions.Hosting`
- [x] 8.3.3 Config: `COMETBFT_WS_URL` env var or `--ws-url` CLI arg
- [x] 8.3.3b Verify that resolution respects the CLI arg > env var > testnet default priority:
  ```csharp
  var wsUrl = args.GetOption("--ws-url")
      ?? Environment.GetEnvironmentVariable("COMETBFT_WS_URL")
      ?? "wss://cosmoshub.cometbftrpc.lava.build:443/websocket";
  ```
- [x] 8.3.4 Register `AddCometBftWebSocket` via DI
- [x] 8.3.5 NewBlock → "Live Blocks" panel (scrolling 20 entries, event-driven)
- [x] 8.3.6 Tx → "Live Transactions" panel (scrolling 20 entries)
- [x] 8.3.7 Vote → log line (validator address, height, round)
- [x] 8.3.8 Automatic reconnection with WARN log
- [x] 8.3.9 Spectre.Console layout: Header, Live Blocks, Live Transactions, Log
- [x] 8.3.10 Subscribe to `NewBlockHeader` and expose its state in the WebSocket demo
- [x] 8.3.11 Subscribe to `ValidatorSetUpdates` and expose the updates in the WebSocket demo
- [x] 8.3.12 Extend the WebSocket demo to exhaustively reflect all public events, subscriptions, and calls exposed by `ICometBftWebSocketClient`

### 8.4 gRPC demo (`samples/CometBFT.Client.Demo.Grpc/`)
- [x] 8.4.1 Create net10.0 console project
- [x] 8.4.2 Dependencies: `Spectre.Console`, `Microsoft.Extensions.Hosting`
- [x] 8.4.3 Config: `COMETBFT_GRPC_URL` env var or `--grpc-url` CLI arg
- [x] 8.4.3b Verify that resolution respects the CLI arg > env var > testnet default priority:
  ```csharp
  var grpcUrl = args.GetOption("--grpc-url")
      ?? Environment.GetEnvironmentVariable("COMETBFT_GRPC_URL")
      ?? "cosmoshub.grpc.lava.build";
  ```
- [x] 8.4.4 Register `AddCometBftGrpc` via DI
- [x] 8.4.5 Fallback polling `PingAsync` every 10 s (streaming unavailable in v0.38)
- [x] 8.4.6 BroadcastAPI panel: Ping latency + timestamp
- [x] 8.4.7 Spectre.Console layout: Header, BroadcastAPI, Log
- [x] 8.4.8 Add endpoint/protocol information in the gRPC demo header
- [x] 8.4.9 Add a `Live Blocks` or `Streaming Events` panel consistent with the effective streaming/polling mode
- [x] 8.4.10 Extend the gRPC demo to expose all public gRPC methods actually supported by the client with their useful responses, not just the minimal ping
- [x] 8.4.11 Add in the gRPC demo an explicit display of the significant gRPC fields actually mapped (`check_tx`, `tx_result` and equivalents), or a verifiable diagnostic equivalent if not observable live

---

## Phase 9 — CI/CD

- [x] 9.1 Create `.github/workflows/ci.yml` (build + lint + test + coverage)
  - [x] 9.1b Add CI step "Package freshness":
    ```yaml
    - name: Check outdated packages
      run: |
        dotnet list package --outdated --include-transitive > outdated.txt
        if grep -q "^   >" outdated.txt; then
          echo "❌ Outdated direct dependencies detected:" && grep "^   >" outdated.txt && exit 1
        fi
    ```
- [x] 9.2 Create `.github/workflows/publish.yml` (pack + push on release tag)
- [x] 9.3 Add `.github/dependabot.yml`:
  ```yaml
  version: 2
  updates:
    - package-ecosystem: "nuget"
      directory: "/"
      schedule:
        interval: "weekly"
      open-pull-requests-limit: 10
  ```
- [x] 9.4 Enable `RestoreLockedMode` in `Directory.Build.props` (src and test projects) + commit `packages.lock.json` after `dotnet restore`
- [x] 9.5 Add a separate CI step for integration tests against a validated public endpoint
- [x] 9.6 Add a separate CI step for E2E tests against a validated public endpoint
- [x] 9.7 Fail CI if global line coverage < 90 % or if any source file line coverage < 90 %
- [x] 9.8 Verify that CI does not require coverage report upload to be compliant
- [x] 9.9 Add to CI a dedicated validation path for self-contained Docker wrappers once the bind-mount migration is implemented
- [x] 9.10 Extend gRPC CI to verify coverage of all audited public gRPC methods and schema parity with the targeted upstream proto
- [x] 9.11 Extend CI to explicitly surface exhaustive validation of the public REST, WebSocket, and gRPC surfaces defined by the audit matrices
- [x] 9.12 ~~Add a separate REST validation path for `Unsafe` endpoints against a controlled test environment, independent of default public endpoints~~ **Deleted (2026-04-14)**: the `unsafe-validation` CI job was removed — no public node with `--rpc.unsafe=true`.
- [x] 9.13 Verify `dotnet pack` produces exactly **one** `.nupkg` file — `Rinzler78.CometBFT.Client.*.nupkg`. All other src projects must have `<IsPackable>false</IsPackable>` set in `Directory.Build.props` or their individual `.csproj`.
- [x] 9.14 Required NuGet package metadata in `CometBFT.Client.Extensions.csproj`:
- [x] 9.15 **Eliminate CI test duplication**: the `build-and-test` job must run only unit tests + coverage gate; integration and E2E tests belong exclusively in their dedicated CI jobs. Replace the `./scripts/test.sh` call with the inline unit-test + coverage-gate commands filtered by `Category!=Integration&Category!=E2E`.
  ```xml
  <PackageId>Rinzler78.CometBFT.Client</PackageId>
  <Authors>Rinzler78</Authors>
  <Description>Async .NET 10 client library for CometBFT — REST, WebSocket and gRPC transports with full source-generated JSON serialization and DI extensions.</Description>
  <PackageTags>cometbft;blockchain;cosmos;grpc;rest;websocket;dotnet</PackageTags>
  <PackageProjectUrl>https://github.com/Rinzler78/CometBFT.Client</PackageProjectUrl>
  <RepositoryUrl>https://github.com/Rinzler78/CometBFT.Client</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <!-- SourceLink -->
  ```
- [x] 9.16 **GitHub Release Governance** — configure tag protection, branch protection, and publish workflow:
  - [x] 9.16.1 Create `.github/branch-protection.md` documenting required GitHub repository settings: `master` (require PR + ≥ 1 approval + all CI green + disallow force-push), `develop` (require PR + CI green), `release/*` (require PR targeting `master`)
  - [x] 9.16.2 Configure GitHub tag protection rule: only tags matching `v*` may be pushed; restricted to maintainer/admin role
  - [x] 9.16.3 Update `.github/workflows/publish.yml` trigger: `on: push: tags: ['v*']` only; add step to validate tag points at a `master` commit
  - [x] 9.16.4 Add CHANGELOG enforcement step in `publish.yml`: extract `## [<version>]` section; fail with descriptive message if missing
  - [x] 9.16.5 Add GitHub Release creation step in `publish.yml`: `gh release create $TAG --notes-file <changelog-section>` with `--prerelease` flag if tag contains `-alpha` or `-rc`
  - [x] 9.16.6 Add `.nupkg` and `.snupkg` upload as release assets in `publish.yml`
  - [x] 9.16.7 Validate pre-release tag path: tag `v*-alpha.*` or `v*-rc.*` pushes to nuget.org pre-release and creates GitHub pre-release

---

## Phase 10 — CometBFT Naming (applied)

> Applied in commits `1cf06f1`, `02f1df5`, `5dd4b4e` before publication.

### 10.1 C# identifiers (namespaces + public API)
- [x] 10.1.1 `ICometBftRestClient` — primary REST client interface (+ implementation + usages)
- [x] 10.1.2 `ICometBftWebSocketClient` — primary WebSocket client interface
- [x] 10.1.3 `ICometBftGrpcClient` — primary gRPC client interface
- [x] 10.1.4 `CometBftRestClient` — REST client implementation
- [x] 10.1.5 `CometBftWebSocketClient` — WebSocket client implementation
- [x] 10.1.6 `CometBftGrpcClient` — gRPC client implementation
- [x] 10.1.7 `CometBftRest/WebSocket/GrpcOptions` — per-transport options classes
- [x] 10.1.8 `CometBftClientException` — base exception (+ per-transport subtypes)
- [x] 10.1.9 `CometBftJsonContext` — AOT JSON serialization context
- [x] 10.1.10 `AddCometBftRest/WebSocket/Grpc` — DI extensions (+ `AddCometBftClient`; `AddCometBftSdkGrpc` removed later as a layer violation)
- [x] 10.1.11 `dotnet build CometBFT.Client.sln --warnaserror` → 0 errors

### 10.2 Scripts, CI/CD, documentation
- [x] 10.2.1 `scripts/*.sh` and `scripts/docker/*.sh` — paths and names aligned
- [x] 10.2.2 `.github/workflows/ci.yml` and `publish.yml` — solution and project names
- [x] 10.2.3 `README.md`, `CHANGELOG.md`, `src/*/README.md`, `samples/*/README.md` — package names and examples
- [x] 10.2.4 `tools/CometBFT.Client.CoverageGate/Program.cs` — `excludedPrefixes/Suffixes`

### 10.3 Naming validation
- [x] 10.3.1 `grep -r "CometBFT\.Client" src/ tests/ --include="*.cs" | grep -v LegacyProto` → identifiers aligned
- [x] 10.3.2 `git remote -v` → `https://github.com/Rinzler78/CometBFT.Client.git`
- [x] 10.3.3 `./scripts/test.sh` → green + coverage ≥ 90 % (actual: 97 %)
- [x] 10.3.4 REST + WS + gRPC demos → start without error

---

## Final validation

- [x] V.1 `dotnet build` passes without warnings; full validation re-run in Docker with live Integration/E2E suites passing against the `Lava` endpoint set
- [x] V.2 `dotnet format --verify-no-changes` passes
- [x] V.3 `./scripts/test.sh` — coverage gate functional, global line coverage ≥ 90 % and per file ≥ 90 % (actual: 97 %)
- [x] V.4 `./scripts/publish.sh --dry-run` — package generated without error
- [x] V.5 All public CometBFT endpoints covered (vs `/rpc/openapi/openapi.yaml`)
- [x] V.6 `dotnet list package --outdated` — zero outdated direct packages
- [x] V.7 `dotnet build --warnaserror` — zero warnings across all src projects
- [x] V.8 REST, WebSocket, and gRPC integration tests run or correctly skipped according to env vars
- [x] V.9 REST, WebSocket, and gRPC E2E tests run or correctly skipped according to env vars
- [x] V.10 `scripts/docker/*.sh` wrappers work in a self-contained mode without bind mounts and remain aligned with local scripts
- [x] V.11 The gRPC client covers all public gRPC methods of the targeted CometBFT release, with proto parity, complete responses, tests, demos, and documentation aligned
- [x] V.12 The REST client covers all public endpoints of the targeted CometBFT OpenAPI, with tests, demos, and documentation aligned
- [x] V.13 The WebSocket client covers all public events, subscriptions, and calls of the targeted protocol, with tests, demos, and documentation aligned
- [x] V.14 ~~The REST client also covers the `Unsafe` endpoints of the targeted OpenAPI when the node enables them, with dedicated tests, explicit demo, and prerequisite documentation~~ **Revised (2026-04-14)**: the REST client exposes `DialSeedsAsync` and `DialPeersAsync`; unit tests (WireMock.Net) cover the implementation. Live integration, E2E tests, and the CI validation job have been removed — no public node with `--rpc.unsafe=true` is available. Acceptance is limited to unit-level coverage until a private testnet is provisioned.
