# CometBFT Client

## Purpose

Standalone .NET 10 client library for CometBFT providing REST (RPC), WebSocket, and gRPC transports with full coverage of all public endpoints defined in the latest stable CometBFT release, dependency injection support, Polly resilience, and XML documentation. All serialization paths are optimized to minimize encoding/decoding latency.

**Protocol source** : https://github.com/cometbft/cometbft — latest stable release
**Package** : `Rinzler78.CometBFT.Client` on nuget.org

---

## Known Public Nodes

These endpoints are used as defaults in demos and real integration tests (via env vars). Tests targeting public nodes are preferred in CI; mainnet endpoints are for local/manual use only.

### Cosmos Hub — Mainnet (validated public RPC)
| Transport | Provider | URL |
|---|---|---|
| REST (RPC) | Lava | https://cos1.lava.build:443 |
| WebSocket | Lava | wss://cos1.lava.build:443/websocket |
| gRPC | Lava | `cos1.lava.build:9090` |
| REST (RPC) | Polkachu | https://cosmos-rpc.polkachu.com |
| gRPC | Polkachu | `cosmos-grpc.polkachu.com:14990` |

> **Integration tests use validated Cosmos Hub endpoints by default.**
> Set `COMETBFT_RPC_URL`, `COMETBFT_WS_URL`, and `COMETBFT_GRPC_URL` to override.

---

## ADDED Requirements

### Requirement: Immutable Domain Types

The library SHALL provide immutable `record` types for all CometBFT domain concepts with Nullable reference types enabled.

#### Scenario: Block record construction
- **WHEN** a `Block` record is constructed with Header, Data, and LastCommit
- **THEN** the record is immutable and all properties are accessible

#### Scenario: TxResult record with events
- **WHEN** a `TxResult` record is constructed with Height, Index, Tx, and Result
- **THEN** all properties are accessible and the record is immutable

#### Scenario: NodeInfo record round-trip
- **WHEN** a `NodeInfo` record is constructed from an RPC response
- **THEN** the record supports `with` expressions and all protocol/network fields are accessible

---

### Requirement: Low-Latency Encoding and Decoding

All serialization and deserialization code SHALL be optimized to minimize CPU overhead and heap allocations. The library SHALL target .NET 10 and exploit available zero-copy and source-generation capabilities.

#### Mandatory techniques

| Area | Rule |
|---|---|
| JSON (REST/RPC) | Use `System.Text.Json` with **source-generated `JsonSerializerContext`** (`[JsonSerializable]`) — no runtime reflection on hot paths |
| JSON streaming | Deserialize directly from `Stream` via `JsonSerializer.DeserializeAsync<T>(stream, ctx)` — never buffer the full response body to a `string` first |
| HTTP response | Use `HttpCompletionOption.ResponseHeadersRead` + `GetStreamAsync` on every GET endpoint to avoid double-buffering |
| Buffers | Use `ArrayPool<byte>.Shared` for temporary byte buffers; use `Microsoft.IO.RecyclableMemoryStream` for payloads > 4 KB |
| gRPC | Parse protobuf messages directly from the gRPC response stream (`Parser.ParseFrom`) — no intermediate JSON transcoding on the gRPC path |
| WebSocket frames | Process incoming frames without allocating a new `byte[]` per message; accumulate via `ArrayPool<byte>.Shared` |
| Hot-path strings | Avoid `string` allocations in message parsing; prefer `ReadOnlySpan<byte>` / `Utf8JsonReader` where the framework permits |
| Newtonsoft.Json | **Forbidden on hot paths.** May only be used for one-off tooling or edge cases explicitly justified in code comments |
| **Typed JSON schemas** | **Every JSON shape — top-level and nested — SHALL be represented as an immutable `record`.** `JsonElement`, `JsonDocument`, `Dictionary<string, object>`, `dynamic`, `object`, raw JSON `string`, `JObject`, `JToken` are **forbidden** as domain type properties or method return values. Sub-objects (block header, commit sig, validator, event attributes, ABCI info…) each require their own named `record`. The `[JsonSerializable]` context must declare every such type. |
| `decimal` for numeric fields | All amounts, fees, and monetary values use `decimal` — never `double` or `float` (IEEE-754 rounding forbidden) |

**Additional coding conventions** (enforced via analyzers and code review):

| Convention | Rule |
|---|---|
| Logging | `ILogger<T>` injected via DI — `Console.WriteLine` forbidden in library code |
| Async | `ConfigureAwait(false)` on all `await` in library code |
| Disposal | All public client types implement `IAsyncDisposable` |
| Polly values | 3 retries, exponential backoff (100 ms × 2ⁿ), circuit breaker: 5 failures/30 s, 1 min open |
| CancellationToken | Propagated to all internal async helpers, not only public methods |

#### Scenario: Every nested JSON object is a typed record
- **WHEN** a CometBFT RPC response contains nested objects (e.g. `block.header`, `block.last_commit.signatures[]`, `tx_result.events[].attributes[]`, `abci_info.response`)
- **THEN** each nested shape maps to a named immutable `record` declared in `CometBFT.Client.Core`
- **AND** no property of any domain type is typed `JsonElement`, `object`, `dynamic`, or `Dictionary<string, object>`
- **AND** no public API method returns a raw JSON `string` representing a domain concept

#### Scenario: JSON deserialized from stream without intermediate string
- **WHEN** `GetBlockAsync` is called
- **THEN** the HTTP response body is deserialized via `JsonSerializer.DeserializeAsync` reading directly from the response `Stream`
- **AND** no intermediate `string` or `byte[]` copy of the full body is created

#### Scenario: Source-generated serializer context covers all domain types including nested
- **WHEN** the `CometBFT.Client.Core` assembly is compiled
- **THEN** a `[JsonSerializable]`-annotated `JsonSerializerContext` exists declaring every public domain `record` — including all nested types — used in REST and WebSocket responses
- **AND** no call to `JsonSerializer.Serialize/Deserialize` uses the default reflection-based overload

#### Scenario: WebSocket frames processed without per-message allocation
- **WHEN** a WebSocket event (NewBlock, Tx, Vote) is received
- **THEN** the incoming frame bytes are accumulated via `ArrayPool<byte>.Shared` and the deserialized record is produced without allocating a new `byte[]` per frame

#### Scenario: gRPC path has no JSON transcoding
- **WHEN** a gRPC method is called
- **THEN** the response is parsed with `Google.Protobuf` binary codec only
- **AND** no JSON serialization step is introduced between the wire bytes and the domain record

---

### Requirement: Complete REST API Coverage (All CometBFT RPC Endpoints)

The REST client SHALL implement all public CometBFT RPC HTTP endpoints as defined in the latest stable release of https://github.com/cometbft/cometbft (`rpc/openapi/openapi.yaml`). All methods SHALL be async with `CancellationToken` support and pagination where applicable.

#### Endpoint groups covered

| Group | Endpoints |
|---|---|
| Info | `/health`, `/status`, `/net_info`, `/blockchain`, `/block`, `/block_by_hash`, `/block_results`, `/commit`, `/validators`, `/genesis`, `/genesis_chunked`, `/dump_consensus_state`, `/consensus_state`, `/consensus_params`, `/unconfirmed_txs`, `/num_unconfirmed_txs` |
| Transaction | `/broadcast_tx_async`, `/broadcast_tx_sync`, `/broadcast_tx_commit`, `/check_tx`, `/tx`, `/tx_search` |
| ABCI | `/abci_info`, `/abci_query` |
| Evidence | `/broadcast_evidence` |

#### Scenario: GetStatusAsync returns typed NodeInfo
- **WHEN** `GetStatusAsync(CancellationToken)` is called
- **THEN** a `ResultStatus` record with `NodeInfo`, `SyncInfo`, and `ValidatorInfo` is returned

#### Scenario: GetBlockAsync returns typed Block
- **WHEN** `GetBlockAsync(height, CancellationToken)` is called
- **THEN** a `Block` record with `Header`, `Data`, and `LastCommit` is returned

#### Scenario: GetBlockResultsAsync returns typed TxResults
- **WHEN** `GetBlockResultsAsync(height, CancellationToken)` is called
- **THEN** a `ResultBlockResults` record with `TxsResults` and `FinalizeBlockEvents` is returned

#### Scenario: BroadcastTxAsync accepts pre-signed transaction bytes
- **WHEN** `BroadcastTxAsync(txBytes, BroadcastMode, CancellationToken)` is called with a pre-signed `TxRaw` (produced by `ITxSigner` from `Rinzler78.Cosmos.Client`)
- **THEN** a `ResultBroadcastTx` with non-empty `Hash` is returned
- **AND** the CometBFT client does not perform any signing — it only broadcasts the bytes as-is; signing is the responsibility of the caller (e.g. `Rinzler78.Cosmos.Client.ITxSigner`)

#### Scenario: TxSearchAsync returns paginated results
- **WHEN** `TxSearchAsync(query, prove, pageRequest, CancellationToken)` is called
- **THEN** a `ResultTxSearch` with `Txs` list and `TotalCount` is returned

#### Scenario: REST client throws CometBftRestException on 4xx
- **WHEN** the server returns 400 Bad Request
- **THEN** a `CometBftRestException` is thrown with the HTTP status code and error details

#### Scenario: REST client retries on transient failure
- **WHEN** the server returns 503 twice then 200
- **THEN** the client retries automatically via Polly and returns the successful response

---

### Requirement: Complete WebSocket Client Coverage (All CometBFT Event Types)

The WebSocket client SHALL subscribe to all CometBFT event types defined in the latest stable release and expose them as typed async streams. All methods SHALL be async with `CancellationToken` support.

#### Event types covered

| Event | JSONRPC subscription query |
|---|---|
| `NewBlock` | `tm.event='NewBlock'` |
| `NewBlockHeader` | `tm.event='NewBlockHeader'` |
| `Tx` | `tm.event='Tx'` |
| `Vote` | `tm.event='Vote'` |
| `ValidatorSetUpdates` | `tm.event='ValidatorSetUpdates'` |

#### Scenario: NewBlock subscription delivers typed events
- **WHEN** `SubscribeNewBlockAsync(CancellationToken)` is called
- **THEN** a `NewBlockEvent` record is received for each new block finalized

#### Scenario: NewBlockHeader subscription delivers typed events
- **WHEN** `SubscribeNewBlockHeaderAsync(CancellationToken)` is called
- **THEN** a `NewBlockHeaderEvent` record is received with `Header` only (no full block)

#### Scenario: Tx subscription delivers typed events
- **WHEN** `SubscribeTxAsync(CancellationToken)` is called
- **THEN** a `TxEvent` record is received for each transaction processed

#### Scenario: WebSocket client throws CometBftWebSocketException on connection failure
- **WHEN** the WebSocket endpoint is unreachable
- **THEN** a `CometBftWebSocketException` is thrown

#### Scenario: WebSocket client reconnects on transient disconnect
- **WHEN** the connection drops mid-stream
- **THEN** the client attempts reconnection via Polly before delivering a `CometBftWebSocketException`

---

### Requirement: Complete gRPC Client Coverage (CometBFT + Cosmos SDK Tx)

The gRPC client SHALL be compiled from the proto files of the latest stable CometBFT release (`/proto/cometbft/`) and expose all available gRPC services. An additional `ICometBftSdkGrpcClient` SHALL target `cosmos.tx.v1beta1` from the Cosmos SDK. All methods SHALL be async with `CancellationToken` support.

**Wire protocol note** : The proto package `tendermint.rpc.grpc` and the enum value `GrpcProtocol.TendermintLegacy` are intentionally preserved for backward-compatibility with legacy CometBFT nodes — these are not renamed.

#### Scenario: PingAsync returns without error
- **WHEN** `PingAsync(CancellationToken)` is called via gRPC
- **THEN** the call completes without throwing

#### Scenario: BroadcastTxAsync returns typed response
- **WHEN** `BroadcastTxAsync(txBytes, CancellationToken)` is called via gRPC
- **THEN** a `ResponseBroadcastTx` record is returned

#### Scenario: Cosmos SDK gRPC GetTxAsync returns typed TxResponse
- **WHEN** `GetTxAsync(txHash, CancellationToken)` is called via `ICometBftSdkGrpcClient`
- **THEN** a typed `TxResponse` record is returned

#### Scenario: gRPC client throws CometBftGrpcException on unavailable
- **WHEN** the gRPC server is unreachable after Polly retries
- **THEN** a `CometBftGrpcException` is thrown

---

### Requirement: Dependency Injection Registration

The library SHALL provide `IServiceCollection` extension methods for registering REST, WebSocket, and gRPC clients with options.

#### Scenario: AddCometBftRest registers ICometBftRestClient
- **WHEN** `services.AddCometBftRest(opts => opts.BaseUrl = "http://localhost:26657")` is called
- **THEN** `services.GetRequiredService<ICometBftRestClient>()` resolves without error

#### Scenario: AddCometBftWebSocket registers ICometBftWebSocketClient
- **WHEN** `services.AddCometBftWebSocket(opts => opts.BaseUrl = "ws://localhost:26657/websocket")` is called
- **THEN** `services.GetRequiredService<ICometBftWebSocketClient>()` resolves without error

#### Scenario: AddCometBftGrpc registers ICometBftGrpcClient
- **WHEN** `services.AddCometBftGrpc(opts => opts.BaseUrl = "http://localhost:9090")` is called
- **THEN** `services.GetRequiredService<ICometBftGrpcClient>()` resolves without error

#### Scenario: AddCometBftSdkGrpc registers ICometBftSdkGrpcClient
- **WHEN** `services.AddCometBftSdkGrpc(opts => opts.BaseUrl = "http://localhost:9090")` is called
- **THEN** `services.GetRequiredService<ICometBftSdkGrpcClient>()` resolves without error

---

### Requirement: Bash Scripts Wrapping dotnet CLI

The repository SHALL provide `build.sh`, `test.sh` and `publish.sh` scripts that wrap the corresponding `dotnet` commands with consistent defaults. For each local script, a Docker counterpart (`docker-build.sh`, `docker-test.sh`, `docker-publish.sh`) SHALL execute the **identical** local script inside an official .NET 10 SDK container — the Docker scripts contain no logic of their own.

```
scripts/
├── build.sh             ← local execution
├── test.sh
├── publish.sh
└── docker/
    ├── Dockerfile       ← FROM mcr.microsoft.com/dotnet/sdk:10.0
    ├── docker-compose.yml
    ├── build.sh         ← docker build + docker run (no bind mount)
    ├── test.sh          ← docker build + docker run; artifacts via docker cp
    └── publish.sh       ← docker build + docker run -e NUGET_API_KEY (no bind mount)
```

**Container conventions** : The `Dockerfile` at `scripts/docker/Dockerfile` copies the full repository sources into the image (`COPY . /workspace`) so the container is fully self-contained and runs on any Docker context (local daemon, remote daemon, CI, Docker-in-Docker) without bind mounts or volume mounts. Each Docker script builds the image first (`docker build`), then runs it (`docker run --rm`). Coverage artifacts and NuGet packages produced inside the container are extracted to the host via `docker cp`. NuGet API key is passed via `-e NUGET_API_KEY` (never as a CLI argument, never in the image).

**Default values (mandatory)** : Every env var and CLI arg SHALL have a default value. Scripts MUST NOT exit solely because an endpoint env var is unset — they fall back to the public mainnet default. Only `NUGET_API_KEY` (secret) is exempt from this rule.

| Env var | Default (validated Cosmos Hub public endpoint) |
|---|---|
| `COMETBFT_RPC_URL` | `https://cos1.lava.build` |
| `COMETBFT_WS_URL` | `wss://cos1.lava.build/websocket` |
| `COMETBFT_GRPC_URL` | `cos1.lava.build:9090` |
| `CONFIGURATION` | `Release` |

#### Scenario: build.sh builds in Release by default
- **WHEN** `./scripts/build.sh` is run without arguments
- **THEN** `dotnet build <sln> --configuration Release` is executed and exits 0

#### Scenario: test.sh enforces 90 % coverage gate
- **WHEN** `./scripts/test.sh` is run
- **THEN** `dotnet test` runs with Coverlet collecting coverage and fails if any metric (line/branch/method) is below 90 %

#### Scenario: publish.sh packs and pushes
- **WHEN** `./scripts/publish.sh --api-key <key>` is run
- **THEN** `dotnet pack` then `dotnet nuget push` are executed with `--skip-duplicate`

#### Scenario: scripts/docker/build.sh runs build.sh inside the SDK container
- **WHEN** `./scripts/docker/build.sh` is run (with optional extra args)
- **THEN** the image is built with `docker build -t cometbft-client-builder -f scripts/docker/Dockerfile .` and then `docker run --rm cometbft-client-builder ./scripts/build.sh "$@"` is executed — no bind mount
- **AND** the exit code of the container matches the exit code of `build.sh`
- **AND** no build logic is duplicated inside `scripts/docker/build.sh`

#### Scenario: scripts/docker/test.sh runs test.sh inside the SDK container
- **WHEN** `./scripts/docker/test.sh` is run
- **THEN** the image is built, a container is created (`docker create`), started (`docker start -a`), and after exit the coverage artifacts are copied to the host via `docker cp <container>:/workspace/coverage ./coverage/`
- **AND** the container is removed after artifact extraction (`docker rm`)

#### Scenario: scripts/docker/publish.sh passes NUGET_API_KEY into the container
- **WHEN** `NUGET_API_KEY=<key> ./scripts/docker/publish.sh` is run
- **THEN** the image is built, the container receives `NUGET_API_KEY` via `-e NUGET_API_KEY` (no bind mount), and calls `./scripts/publish.sh` which reads it from the environment
- **AND** the API key is never written to disk or embedded in any script

---

### Requirement: Git Flow and Branch Protection Hooks

The repository SHALL enforce Git Flow branching and protect master and develop from direct pushes via git hooks.

#### Scenario: Direct push to master is blocked
- **WHEN** a developer attempts `git push origin master` directly
- **THEN** the pre-push hook exits with code 1

#### Scenario: Direct push to develop is blocked
- **WHEN** a developer attempts `git push origin develop` directly
- **THEN** the pre-push hook exits with code 1

#### Scenario: Invalid commit message is rejected
- **WHEN** a commit message does not match conventional commits format
- **THEN** the commit-msg hook exits with code 1

---

### Requirement: GitHub Release Governance — Tags, SemVer, and Publish Workflow

The repository SHALL enforce a strict release lifecycle: tags are the single source of truth for versioning, GitHub branch protection rules mirror the local git hooks, and the `publish.yml` workflow is the only path to NuGet publication.

#### Tag naming convention

| Tag pattern | Meaning | NuGet feed | GitHub Release |
|---|---|---|---|
| `v<MAJOR>.<MINOR>.<PATCH>` | Stable release (e.g. `v1.2.0`) | `nuget.org` stable | Published, not pre-release |
| `v<x.y.z>-alpha.<n>` | Alpha pre-release (e.g. `v1.2.0-alpha.1`) | `nuget.org` pre-release | Published, marked pre-release |
| `v<x.y.z>-rc.<n>` | Release candidate (e.g. `v1.2.0-rc.1`) | `nuget.org` pre-release | Published, marked pre-release |

Tags MUST be created **only on `master`** for both stable and pre-release publications. Tags on `develop`, `release/*`, or feature branches are forbidden — `publish.yml` rejects any tag not reachable from `master`.

#### SemVer rules (conventional commits driven)

| Commit prefix | Minimum version bump |
|---|---|
| `BREAKING CHANGE` in footer or `!` suffix | **MAJOR** |
| `feat:` | **MINOR** |
| `fix:`, `perf:`, `refactor:`, `docs:`, `chore:` | **PATCH** |

#### GitHub branch protection rules (required — configured in `.github/branch-protection.md`)

| Branch | Required checks before merge | Additional rules |
|---|---|---|
| `master` | All CI check-runs green (`CI / English-only language check (cspell)`, `CI / Build & Test (.NET 10)`, `CI / Integration Tests`, `CI / E2E Tests`) | Require PR + ≥ 1 approving review; disallow direct push; disallow force-push; restrict tag creation to maintainers |
| `develop` | All CI check-runs green | Require PR; disallow direct push; disallow force-push |
| `release/*` | All CI check-runs green | Require PR targeting `master`; delete branch after merge |

Tags matching `v*` SHALL be protected: only repository maintainers (those with `maintain` or `admin` role) may create them.

#### CI/CD workflow structure

**`ci.yml`** — triggered on `push` to `feature/**`/`bugfix/**` and on `pull_request` targeting `develop`, `master`, or `release/**`:
- `language-check` job: `cspell` English-only check across source, scripts, and docs
- `build-and-test` job: `dotnet build` + `dotnet format --verify-no-changes` + unit tests with Coverlet coverage gate (≥ 90 %)
- `integration-tests` job: runs integration tests against public CometBFT endpoints (needs `build-and-test`)
- `e2e-tests` job: runs E2E tests against public CometBFT endpoints (needs `integration-tests`)

**`publish.yml`** — triggered **only** on `push: tags: ['v*']` (never on branch push):
1. Validate tag points at a commit reachable from `master` (`git merge-base --is-ancestor HEAD origin/master`); fail otherwise
2. Extract CHANGELOG section matching the tag version (exact heading match `## [<version>]`); fail if section is missing
3. `dotnet build --configuration Release`
4. Run full test suite including coverage gate; fail if any threshold is not met
5. `dotnet pack --configuration Release --no-build` with `PackageVersion` derived from the tag (e.g. `v1.2.0` → `1.2.0`, `v1.2.0-rc.1` → `1.2.0-rc.1`); NuGet stable vs pre-release is determined by the packed version string
6. `dotnet nuget push` with `--skip-duplicate`
7. Create GitHub Release via `gh release create $TAG --notes-file <extracted-changelog-section>` with `--prerelease` flag if tag contains `-alpha` or `-rc`
8. Upload `.nupkg` and `.snupkg` as release assets

#### CHANGELOG enforcement (pre-publish gate)

Before `dotnet nuget push`, `publish.yml` SHALL extract the CHANGELOG.md section whose heading matches `## [<version>]` (e.g. `## [1.2.0]`). If no matching section is found, the workflow exits non-zero with the message: `CHANGELOG.md has no entry for version <version> — add a [<version>] section before tagging`.

#### Scenario: Stable tag on master triggers NuGet stable publish and GitHub Release
- **WHEN** a maintainer pushes tag `v1.2.0` pointing at a `master` commit
- **THEN** `publish.yml` runs, validates the tag is on `master`, finds `## [1.2.0]` in CHANGELOG.md, packs and pushes to nuget.org stable feed, and creates a GitHub Release named `v1.2.0` (not pre-release) with the extracted CHANGELOG section as description
- **AND** `.nupkg` and `.snupkg` are attached to the GitHub Release as downloadable assets

#### Scenario: Pre-release tag triggers nuget.org pre-release publish and GitHub pre-release
- **WHEN** a maintainer pushes tag `v1.2.0-rc.1`
- **THEN** `publish.yml` pushes to nuget.org with the pre-release version identifier
- **AND** the GitHub Release is created with the `--prerelease` flag

#### Scenario: Tag on non-master branch is rejected by publish.yml
- **WHEN** a tag matching `v*` is pushed but does not point at a `master` commit
- **THEN** `publish.yml` fails at the branch validation step with a descriptive error message
- **AND** no NuGet package is pushed and no GitHub Release is created

#### Scenario: Missing CHANGELOG section blocks publish
- **WHEN** `publish.yml` runs for tag `v1.3.0` but CHANGELOG.md has no `## [1.3.0]` section
- **THEN** the workflow exits non-zero with the message `CHANGELOG.md has no entry for version 1.3.0`
- **AND** no package is pushed

#### Scenario: Direct tag creation by non-maintainer is rejected by GitHub
- **WHEN** a collaborator without `maintain` or `admin` role attempts to push a `v*` tag to the remote
- **THEN** GitHub's tag protection rule rejects the push
- **AND** no publish workflow is triggered

#### Scenario: publish.yml is never triggered by a branch push
- **WHEN** code is pushed to `develop`, `master`, or any feature branch (not a tag)
- **THEN** `publish.yml` does NOT run
- **AND** only `ci.yml` runs

---

### Requirement: Test Coverage ≥ 90 % — Global, Per File, Per Branch, Per Method

All library code SHALL be covered by automated tests achieving **≥ 90 % simultaneously on line, branch, and method metrics**, enforced at **three levels**:

| Level | Enforcement |
|---|---|
| **Global** | All assemblies combined ≥ 90 % on all metrics |
| **Per assembly** | Each src assembly independently ≥ 90 % (ThresholdStat=Minimum) |
| **Per file** | Each source file ≥ 90 % visible in ReportGenerator HTML report |

A high-coverage file or assembly MUST NOT compensate for a low-coverage one. The global aggregate passes only when every individual file and assembly also passes.

**Coverage is enforced at two points:**
1. **Pre-push hook** — runs `dotnet test` with Coverlet thresholds; blocks push if any threshold fails
2. **CI pipeline** — runs the same gate; fails the build with an explicit message naming the failing assembly/file/metric

**Coverlet configuration (Directory.Build.props — Test projects):**
```xml
<Threshold>90</Threshold>
<ThresholdType>line,branch,method</ThresholdType>
<ThresholdStat>Minimum</ThresholdStat>  <!-- worst assembly, not average -->
```

#### Scenario: Coverage gate enforced globally
- **WHEN** tests run across all src assemblies
- **THEN** the combined line coverage is ≥ 90 %, the combined branch coverage is ≥ 90 %, and the combined method coverage is ≥ 90 %

#### Scenario: Coverage gate enforced per assembly
- **WHEN** tests run across all src assemblies
- **THEN** each assembly independently achieves ≥ 90 % line, ≥ 90 % branch, ≥ 90 % method
- **AND** if any single assembly falls below 90 % on any metric, `./scripts/test.sh` exits non-zero
- **AND** a high-coverage assembly does NOT compensate for a low-coverage one

#### Scenario: Coverage gate enforced per file
- **WHEN** ReportGenerator produces the HTML report
- **THEN** every source file shows ≥ 90 % line coverage in the per-file breakdown
- **AND** files below 90 % are flagged in CI output

#### Scenario: Pre-push hook blocks push on coverage failure
- **WHEN** a developer runs `git push`
- **THEN** the pre-push hook executes `dotnet test` with Coverlet thresholds before the push is sent
- **AND** if any threshold fails (global, per-assembly, or per-file), the hook exits non-zero and the push is rejected with an explicit error message
- **AND** the push succeeds only when all coverage thresholds pass

#### Scenario: Coverage report generated per assembly and per file
- **WHEN** `./scripts/test.sh` completes
- **THEN** ReportGenerator produces an HTML report at `coverage/report/` showing breakdown per assembly, per class, and per file
- **AND** the report is uploaded as a CI artifact

#### Scenario: Coverage gate blocks CI on failure
- **WHEN** a code change reduces coverage below 90 % on any metric at any level
- **THEN** CI fails with an explicit message naming the failing assembly, file, and metric

---

### Requirement: Integration Tests — Real Endpoints, Skippable

The library SHALL provide integration tests that exercise the real HTTP/gRPC/WebSocket stack against public CometBFT endpoints. These tests are automatically skipped when the required environment variables are not set, and run in CI against validated public nodes.

#### Scenario: Integration test skipped when env var absent
- **WHEN** the required endpoint env var is not set
- **THEN** all integration tests are skipped (not failed) using `Xunit.SkippableFact`

#### Scenario: Integration test runs against real endpoint
- **WHEN** the endpoint env var is set to a valid URL
- **THEN** the client initializes via DI, calls at least one real endpoint, and returns a valid deserialized response without throwing

#### Scenario: Integration tests included in coverage report
- **WHEN** integration tests run (env vars set)
- **THEN** their coverage is merged into the global coverage report alongside unit test coverage

---

### Requirement: E2E Tests — Full Flow, Skippable

The library SHALL provide end-to-end tests that exercise a complete user flow from DI registration through real network calls to deserialized domain objects. E2E tests are tagged `[Trait("Category","E2E")]` and skipped when endpoint env vars are absent.

#### Scenario: E2E test executes full client lifecycle
- **WHEN** an E2E test runs with endpoint env vars set
- **THEN** it creates a `ServiceCollection`, registers the client via `Add*` extension, resolves the client from DI, calls ≥ 3 distinct endpoints across at least 2 transports (REST + WebSocket or gRPC), and asserts the returned domain records are non-null and structurally valid

#### Scenario: E2E test skipped when env var absent
- **WHEN** the required endpoint env var is not set (`COMETBFT_RPC_URL` / `COMETBFT_WS_URL` / `COMETBFT_GRPC_URL`)
- **THEN** the E2E test is skipped (not failed)

#### Scenario: CI runs E2E as a separate step
- **WHEN** the CI pipeline runs
- **THEN** E2E tests execute in a dedicated step after unit and integration tests, using validated public node env vars
- **AND** E2E failures are reported separately from unit test failures

---

### Requirement: Zero Errors, Zero Warnings — Build Quality Gate

The build SHALL produce **zero errors and zero warnings**. `TreatWarningsAsErrors` SHALL be set to `true` for all src projects. Warning suppression (`#pragma warning disable`) is forbidden without an inline justification comment. Nullable reference types SHALL be enabled on all projects.

**Directory.Build.props mandatory settings** :
```xml
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<WarningLevel>9999</WarningLevel>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisMode>AllEnabledByDefault</AnalysisMode>
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

#### Scenario: Build produces zero warnings
- **WHEN** `./scripts/build.sh` is run on a clean checkout
- **THEN** `dotnet build` exits 0 with zero warnings and zero errors
- **AND** any new public member without an XML `<summary>` causes CS1591 to be treated as an error

#### Scenario: Warning suppression requires justification
- **WHEN** a `#pragma warning disable` directive is added
- **THEN** it MUST be accompanied by an inline comment explaining why the suppression is justified

#### Scenario: Nullable violations are errors
- **WHEN** a nullable dereference warning is introduced (CS8602, CS8603, CS8604…)
- **THEN** the build fails — the developer must fix the null-safety issue, not suppress it

---

### Requirement: Package Freshness — Zero Outdated Dependencies

All NuGet packages (direct dependencies) SHALL be pinned to their latest stable versions at time of release. The CI pipeline SHALL fail if any direct dependency has a newer stable version available. Transitive dependencies SHALL be locked via `packages.lock.json`.

#### Scenario: CI fails on outdated direct dependencies
- **WHEN** `dotnet list package --outdated` is run in CI
- **THEN** the output contains zero outdated direct dependencies
- **AND** the step exits non-zero if any direct package has a newer stable version

#### Scenario: packages.lock.json prevents transitive drift
- **WHEN** `dotnet restore --locked-mode` is run
- **THEN** restore succeeds using the committed lock file without resolving new versions

#### Scenario: Dependabot or Renovate auto-PRs package updates
- **WHEN** a new stable version of a direct dependency is released
- **THEN** an automated PR is opened within 24 hours proposing the version bump

---

### Requirement: Protocol Version Traceability

The library SHALL document the CometBFT protocol version in `Directory.Build.props`, `README.md`, and `CHANGELOG.md`.

#### Scenario: ProtocolVersion is set in Directory.Build.props
- **WHEN** `Directory.Build.props` is read
- **THEN** `<ProtocolVersion>` is present with the targeted CometBFT release tag (e.g., `v0.38.x`)

#### Scenario: CHANGELOG references protocol version
- **WHEN** a new library version is released
- **THEN** `CHANGELOG.md` contains a link to the CometBFT release and the targeted version

#### Scenario: README links to official repo
- **WHEN** `README.md` is read
- **THEN** it contains a link to https://github.com/cometbft/cometbft and the protocol version

---

### Requirement: English-Only Language Policy

All human-readable text in the repository SHALL be written in English. This applies without exception to:

| Scope | Examples |
|---|---|
| Source code | Variable names, method names, class names |
| Comments | Inline `//`, block `/* */`, `#` in bash/yaml |
| XML documentation | `<summary>`, `<param>`, `<returns>`, `<remarks>` |
| Specifications | All `.md` files under `openspec/` |
| Documentation | `README.md`, `CHANGELOG.md`, `docs/**` |
| Scripts | Bash comments and echo messages |
| Commit messages | Title and body |
| CI/CD | YAML comments and step names |

**Enforcement** : a `cspell` pre-commit hook and a CI step detect non-English words before any commit or merge. Technical terms, protocol names, and domain acronyms are whitelisted in `.cspell.json`. Suppression via `// cspell:disable` is forbidden without an inline justification comment.

#### Scenario: Non-English word in C# comment blocks pre-commit hook
- **WHEN** a developer attempts to commit a `.cs` file containing a comment with a non-English word not listed in `.cspell.json`
- **THEN** the `cspell` pre-commit hook exits non-zero and the commit is rejected
- **AND** the error output identifies the file, line number, and offending word

#### Scenario: Non-English word in Markdown blocks pre-commit hook
- **WHEN** a developer attempts to commit a `.md` file containing non-English prose
- **THEN** the `cspell` pre-commit hook exits non-zero and the commit is rejected

#### Scenario: Technical term whitelisted in cspell.json passes the hook
- **WHEN** a technical term (e.g., `CometBFT`, `gRPC`, `NuGet`, `tendermint`) is present in `.cspell.json` `words`
- **THEN** the hook accepts it without error

#### Scenario: CI enforces English-only on every PR
- **WHEN** a pull request is opened against `develop` or `master`
- **THEN** the CI runs `cspell` on all changed files and fails the check if any non-whitelisted non-English word is found

---

### Requirement: Documentation as Commit Gate

The library SHALL have a `README.md` and `CHANGELOG.md` present from the very first code commit. No public type or member may be committed without the corresponding XML documentation comment. Build enforces this via `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.

#### Scenario: README stub exists at first commit
- **WHEN** the first code commit is made
- **THEN** a `README.md` with at minimum a title, a one-line description, a CI badge placeholder, and an Installation section placeholder is present in the repository root

#### Scenario: CHANGELOG stub exists at first commit
- **WHEN** the first code commit is made
- **THEN** a `CHANGELOG.md` following Keep-a-Changelog format with an `[Unreleased]` section is present in the repository root

#### Scenario: Public API without XML doc fails build
- **WHEN** a public type or member is added without an XML documentation comment
- **THEN** the build fails with a documentation warning promoted to error
- **AND** the commit is rejected by the pre-commit hook (dotnet build --warnaserror)

#### Scenario: README updated when public API changes
- **WHEN** a new public interface, client method, or DI extension is added
- **THEN** `README.md` is updated to reflect the new capability before the commit is merged

---

### Requirement: NuGet Dependency Chain — Base Layer, Interfaces, and Domain Models

`Rinzler78.CometBFT.Client` is the **root of the client dependency chain**. It has no upstream NuGet dependency on any other `Rinzler78.*` package. It exposes the base public interfaces and domain model types that higher-level libraries (`Rinzler78.Cosmos.Client`, `Rinzler78.Osmosis.Client`) consume directly or extend — without redefining them.

**Dependency chain position:**
```
Rinzler78.CometBFT.Client   ← this package (no upstream)
    ↑
Rinzler78.Cosmos.Client     (depends on this package, extends interfaces, reuses domain models)
    ↑
Rinzler78.Osmosis.Client    (depends on Cosmos.Client, transitively on this package)
```

**Domain models exposed to downstream libraries:**
- `Block`, `BlockHeader`, `TxResult`, `Event`, `Attribute` — consumed by `Cosmos.Client.Core`
- `NodeInfo`, `SyncInfo`, `Validator` — consumed by `Cosmos.Client.Core` where applicable

**Interface inheritance chain:**
```
ICometBftSdkGrpcClient  ← defined here
    ↑ extends
ICosmosGrpcClient       (defined in Cosmos.Client)
    ↑ extends
IOsmosisGrpcClient      (defined in Osmosis.Client)
```

#### Scenario: No upstream Rinzler78 package reference
- **WHEN** `CometBFT.Client.Extensions.csproj` is inspected
- **THEN** it contains no `<PackageReference>` to any other `Rinzler78.*` package
- **AND** `dotnet list package` on the solution shows no Rinzler78 transitive dependency

#### Scenario: Public interfaces and models are consumable by downstream libraries
- **WHEN** `Rinzler78.Cosmos.Client` is compiled with a reference to `Rinzler78.CometBFT.Client`
- **THEN** `ICometBftRestClient`, `ICometBftWebSocketClient`, `ICometBftGrpcClient`, `ICometBftSdkGrpcClient`, and all domain records (`Block`, `TxResult`, `Event`, `Attribute`…) resolve without error
- **AND** downstream libraries extend interfaces and compose domain types without redefining them

---

### Requirement: Single NuGet Package — `Rinzler78.CometBFT.Client`

The repository SHALL produce exactly **one** NuGet package: `Rinzler78.CometBFT.Client`. Internal src projects (`Core`, `Rest`, `WebSocket`, `Grpc`) are organizational units only — they set `<IsPackable>false</IsPackable>` and are never published independently. Only `CometBFT.Client.Extensions` is packable and carries the full required metadata (id, description, tags, SourceLink, symbols).

#### Scenario: Only one .nupkg is produced
- **WHEN** `dotnet pack` runs on the solution
- **THEN** exactly one `.nupkg` file is produced: `Rinzler78.CometBFT.Client.X.Y.Z.nupkg`
- **AND** no `Core`, `Rest`, `WebSocket`, or `Grpc` `.nupkg` files are generated

#### Scenario: Package metadata is complete
- **WHEN** the package is inspected with `dotnet nuget` or NuGet Package Explorer
- **THEN** it contains: `PackageId`, `Authors`, `Description`, `Tags`, `ProjectUrl`, `RepositoryUrl`, `LicenseExpression`, embedded symbols (`.snupkg`), and SourceLink metadata

---

### Requirement: Demo Console Sample (`samples/CometBFT.Client.Sample/`)

The repository SHALL provide a minimal console application that exercises all three transports
sequentially to serve as a quick integration reference.

**Config** : `COMETBFT_RPC_URL`, `COMETBFT_WS_URL`, `COMETBFT_GRPC_URL` env vars
or `--rpc-url`, `--ws-url`, `--grpc-url` CLI args. Falls back to Cosmos Hub mainnet defaults.

#### Scenario: Console sample exercises REST, WebSocket and gRPC in sequence
- **WHEN** the sample is run with valid endpoint env vars
- **THEN** it calls at least one REST endpoint, subscribes to one WebSocket event stream
  for a bounded duration, calls at least one gRPC method, and exits 0 without error

---

> **Note — transport-specific Spectre.Console demos removed (v0.2.0)**
>
> `samples/CometBFT.Client.Demo.Rest/`, `samples/CometBFT.Client.Demo.WebSocket/`, and
> `samples/CometBFT.Client.Demo.Grpc/` were consolidated into the unified Avalonia dashboard
> below. The scripts `demo-rest.sh`, `demo-ws.sh`, and `demo-grpc.sh` no longer exist;
> use `./scripts/demo.sh` instead.

---

### Requirement: Demo Unified Dashboard (`samples/CometBFT.Client.Demo.Dashboard/`)

The repository SHALL provide a unified, real-time Avalonia 12 desktop dashboard that aggregates WebSocket events, REST polling, and Cosmos SDK gRPC enrichment into a single persistent window. The dashboard connects to all three transports simultaneously and updates live as new blocks, transactions, and events arrive.

**Stack** : Avalonia 12.0, `Microsoft.Extensions.Hosting`, `CommunityToolkit.Mvvm`, `Avalonia.Controls.DataGrid`, `Avalonia.Themes.Fluent`
**Config** : `COMETBFT_RPC_URL`, `COMETBFT_WS_URL`, `COMETBFT_GRPC_URL` env vars or `--rpc-url`, `--ws-url`, `--grpc-url` CLI args. Falls back to validated Cosmos Hub public endpoints.

#### Design System (Mintscan-inspired)

| Token | Value | Usage |
|---|---|---|
| Background | `#060b18` | Window background |
| Card surface | `#0c1426` | All card backgrounds |
| Card border | `#162039` | All card borders |
| Cyan accent | `#29b6f6` | Primary accent, validator bars |
| Mint green | `#69f0ae` | Block heights, LIVE indicator |
| Warm orange | `#ffb74d` | Transaction accent, pending badge |
| Lavender | `#b39ddb` | Event log accent |
| Hash / address | `#40c4ff` | Monospace hash and address fields |
| Text primary | `#e3eaf5` | Values and labels |
| Text muted | `#455a64` | Column headers, secondary text |

Window minimum: 1600×960. All cards: `CornerRadius="12"`, `BorderThickness="1"`, `BorderBrush="#162039"`, card header band `Background="#0a1220"`.

#### Layout

```
┌───────────────────────────────────────────────────────────────────────┐
│ HEADER (gradient): ⬡  CometBFT  | NODE pill | VERSION pill | SYNCED  │
│                                                              ● Connected│
├───────────────────────────────────────────────────────────────────────┤
│ KPI ROW:  #LATEST BLOCK  |  BLOCK TXS  |  MEMPOOL  |  PEERS          │
├───────────────┬──────────────────────────────────┬────────────────────┤
│ Node Info     │  Recent Blocks  │  Recent Txs     │  Event Log        │
│ (status card) │  (left half)    │  (right half)   │  (typed entries)  │
│               │                 │                 │                   │
│ Validators    │                 │                 │                   │
│ (rank + bar)  │                 │                 │                   │
└───────────────┴─────────────────┴─────────────────┴────────────────────┘
```

The center area is a `Grid` with `ColumnDefinitions="*,10,*"` — Recent Blocks and Recent Transactions occupy equal width side by side (not stacked).

#### Header bar

- Gradient background `#060d1e` → `#0a1830`, height 58 px, bottom border `#0e1a30`
- Left: hex icon `⬡` in `#0d2040` rounded badge, "CometBFT" title (15 px bold), ChainId sub-label
- Center: NODE pill (Moniker), VERSION pill (NodeVersion), SYNCED pill (`SyncStatusText` + green dot)
- Right: connection pill (`ConnectionStatus` + green dot), `Background="#0a1e10"`, `BorderBrush="#1a3a20"`

#### KPI stats row

Four headline metrics separated by 1 px dividers. Each metric: label (9 px, `#37474f`), large colored number (26 px bold), sub-label (9 px, `#455a64`).

| Metric | Binding | Color |
|---|---|---|
| LATEST BLOCK | `LatestHeight` `StringFormat='#{0:N0}'` Consolas monospace | `#69f0ae` |
| BLOCK TXS | `LatestBlockTxCount` | `#ffb74d` |
| MEMPOOL | `PendingTxCount` | `#29b6f6` |
| PEERS | `PeerCount` | `#b39ddb` |

Sub-label for LATEST BLOCK: `LatestBlockTime`.

#### Node Info card (left column, top)

- Header band: ChainId pill (`#0d2040` / `#1a3a6b` border) + Synced badge (`#0a1e10` / `#1a3a20` border, green dot + `SyncStatusText`)
- Body: Moniker (15 px bold `#e3eaf5`) as large title, horizontal separator, then key-value rows for Node ID (10 px monospace cyan `#40c4ff`, truncated), Version, Peers (colored number `#b39ddb` + "nodes" label)
- `SyncStatusText` computed property: `IsSyncing ? "Syncing…" : "Synced"`, decorated with `[NotifyPropertyChangedFor(nameof(SyncStatusText))]`

#### Validators card (left column, fills remaining height)

- Header band: "Validators" title (cyan left-accent strip 3 px) + count badge
- Manual column headers row: `#` | ADDRESS | POWER %
- Custom `ListBox` with `x:DataType="vm:ValidatorRow"` (compiled bindings):
  - Rank badge: `#0d1e38` background, `#546e7a` text, 22 px width
  - Address: 11 px monospace `#90a4ae`, truncated
  - Power bar: `ProgressBar` 3 px height, `Foreground="#29b6f6"`, `Background="#0e1a30"`, bound to `VotingPowerPct` (0–100), `Maximum="100"`
  - Percentage label: 11 px `#29b6f6`, right-aligned
- `ValidatorRow(int Rank, string Address, long VotingPower, long ProposerPriority, double VotingPowerPct)`
- `DashboardBackgroundService.RefreshValidatorsAsync` computes: `totalPower = Sum(VotingPower)`, `VotingPowerPct = Round(v.VotingPower / totalPower * 100, 1)`, rank = sort index + 1

#### Recent Blocks card (center-left column)

- Header band: "Recent Blocks" (green left-accent strip) + LIVE badge (green dot + "LIVE")
- Manual column headers: HEIGHT | TIME | TXS | PROPOSER
- `ListBox` with `x:DataType="vm:BlockRow"`:
  - Row: `Border` with `BorderThickness="3,0,0,0"`, `BorderBrush="#1a3a24"`, hover state via `Border.Styles` `Border:pointerover` selector changing `BorderBrush` to `#69f0ae` and `Background` to `#0a1220`
  - Height: 12 px bold monospace `#69f0ae`
  - Time: 11 px `#546e7a`
  - Tx count badge: `#0e1e10` / `#1e3e1e` border, `#81c784` text
  - Proposer: 10 px cyan monospace, truncated
- Collection: `ObservableCollection<BlockRow>`, max 50 entries (insert at 0, trim tail)

#### Recent Transactions card (center-right column)

- Header band: "Transactions" (orange left-accent strip) + `PendingTxCount` pending badge
- Manual column headers: HASH | HEIGHT | STATUS
- `ListBox` with `x:DataType="vm:TxRow"`:
  - Row: two-line `StackPanel` inside `Border` with left-accent border (`#2a1e08`, hover `#ffb74d`)
  - Line 1: Hash (11 px cyan monospace, truncated) + Height (11 px `#546e7a` monospace) + Status badge
  - Line 2: Log (10 px `#37474f`, truncated, `TextTrimming="CharacterEllipsis"`)
  - Status badge: `#0b1f0f` / `#1a4228` border, `#4caf50` text bound to `StatusText`
- `TxRow` computed properties: `StatusText => Code == 0 ? "OK" : "ERR"`, `IsSuccess => Code == 0`
- Collection: max 50 entries

#### Event Log card (right column)

- Header band: "Event Log" (lavender left-accent strip) + entry count badge
- `ListBox` with `x:DataType="vm:EventLogRow"`:
  - Row: `Grid ColumnDefinitions="10,*"` — category dot (`Ellipse` 6 px, `#455a64`) + stacked content (timestamp 9 px monospace `#37474f` / description 10 px `#78909c`, wrapping)
- `EventLogRow(string Timestamp, string Category, string Description)` — `Category` values: `"block"`, `"tx"`, `"vote"`, `"validator"`, `"header"`, `"error"`
- `AppendEventLog(string category, string message)` — inserts at index 0, caps at 100 entries

#### Background service

`DashboardBackgroundService` implements `BackgroundService` and:
- Subscribes to WS events: `NewBlock`, `NewBlockHeader`, `Tx`, `Vote`, `ValidatorSetUpdates`
- On `NewBlock`: calls `ICometBftSdkGrpcClient.GetLatestBlockAsync()` to enrich the block row, then `ICometBftRestClient.GetNumUnconfirmedTxsAsync()` for mempool count
- On startup: calls `ICometBftSdkGrpcClient.GetStatusAsync()` (chain meta), `GetLatestValidatorsAsync()` (validators), `ICometBftRestClient.GetNetInfoAsync()` (peers)
- Periodic timer every 30 s: refreshes node info and peer count
- All UI mutations via `Dispatcher.UIThread.InvokeAsync()`; all async library calls use `ConfigureAwait(false)`

#### Scenario: Dashboard starts and connects to all three transports
- **WHEN** the dashboard starts with valid endpoint env vars
- **THEN** `ConnectionStatus` transitions from "Connecting…" to "Connected" within 5 seconds
- **AND** all KPI metrics populate from the initial load calls before the first block event arrives

#### Scenario: KPI row updates on every new block
- **WHEN** a `NewBlock` WebSocket event is received
- **THEN** `LatestHeight`, `LatestBlockTime`, and `LatestBlockTxCount` update within one UI frame
- **AND** `PendingTxCount` refreshes via REST `GetNumUnconfirmedTxsAsync()`

#### Scenario: Validators display with rank and power bars
- **WHEN** `GetLatestValidatorsAsync()` returns N validators
- **THEN** each `ValidatorRow` has `Rank` 1…N (sorted descending by `VotingPower`) and `VotingPowerPct = VotingPower / totalPower × 100` rounded to one decimal
- **AND** the `ProgressBar` in each row accurately reflects the validator's share

#### Scenario: Blocks list caps at 50 entries
- **WHEN** more than 50 `NewBlock` events have been received
- **THEN** `Blocks` contains exactly 50 entries — the 50 most recent — with the latest at index 0

#### Scenario: Transactions list caps at 50 entries
- **WHEN** more than 50 `TxExecuted` events have been received
- **THEN** `Transactions` contains exactly 50 entries — the 50 most recent

#### Scenario: SyncStatusText reflects node sync state
- **WHEN** `GetStatusAsync()` returns `IsSyncing = false`
- **THEN** the Synced pill in the header shows "Synced" with a green dot
- **WHEN** `GetStatusAsync()` returns `IsSyncing = true`
- **THEN** the pill shows "Syncing…"

#### Scenario: Event log preserves category metadata
- **WHEN** `AppendEventLog("block", "Block #30750376 — 3 txs")` is called
- **THEN** the inserted `EventLogRow` has `Category = "block"` and a UTC timestamp
- **AND** it appears at index 0 in `EventLog`

#### Scenario: Dashboard shuts down cleanly on window close
- **WHEN** the Avalonia window is closed
- **THEN** `host.StopAsync()` is awaited, all WebSocket event handlers are unsubscribed, and the process exits with code 0
