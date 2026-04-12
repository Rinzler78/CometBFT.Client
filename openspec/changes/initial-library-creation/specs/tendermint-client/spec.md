# Tendermint Client

## Purpose

Standalone .NET 10 client library for CometBFT/Tendermint providing REST, WebSocket and gRPC transports with full coverage of all public endpoints, dependency injection support, Polly resilience, and XML documentation. All serialization paths are optimized to minimize encoding/decoding latency.

**Protocol source** : https://github.com/cometbft/cometbft — latest stable release
**Package** : `Rinzler78.CometBFT.Client` on nuget.org

---

## Known Public Nodes

These endpoints are used as defaults in demos and real integration tests (via env vars). The default set MUST be based on validated public endpoints rather than historical assumptions.

### Cosmos Hub — Mainnet (`cosmoshub-4`)
| Transport | Provider | URL |
|---|---|---|
| REST (LCD) | Polkachu | https://cosmos-api.polkachu.com |
| REST (LCD) | Cosmos Directory | https://rest.cosmos.directory/cosmoshub |
| gRPC | Lava | `cosmoshub.grpc.lava.build` |
| RPC (REST/WS) | Lava | `https://cosmoshub.tendermintrpc.lava.build:443` |

### Reality Notes

- Earlier Theta testnet defaults proved unreliable from the validation environment and were removed as effective defaults.
- `https://cosmos-rpc.polkachu.com` validated successfully for RPC but rejected WebSocket upgrades with HTTP `400`, so it was not retained as the cross-transport default.
- `wss://rpc-cosmoshub.ecostake.com/websocket` validated successfully and served as an intermediate fallback during reconciliation.
- `Lava` was retained as the final default RPC/WebSocket/gRPC provider because its public Cosmos Hub endpoints were listed in the ecosystem registry and validated successfully in live checks.

> **Integration tests and demos use validated public Cosmos Hub defaults.** Set `TENDERMINT_RPC_URL`, `TENDERMINT_WS_URL`, `TENDERMINT_GRPC_URL` to override.

---

## ADDED Requirements

### Requirement: Immutable Domain Types

The library SHALL provide immutable `record` types for all Tendermint domain concepts with Nullable reference types enabled.

#### Scenario: Block record construction
- **WHEN** a `Block` record is constructed with Height, Hash, Time, Proposer and Txs
- **THEN** all properties are accessible and the record is immutable (init-only setters)

#### Scenario: Block equality by value
- **WHEN** two `Block` records are constructed with the same values
- **THEN** they compare equal using `==`

#### Scenario: TxResult record with Events
- **WHEN** a `TxResult` is constructed with a non-empty `Events` collection
- **THEN** `Events` is non-null and each `Event` contains its `Attributes`

#### Scenario: Transports share the same domain objects
- **WHEN** the same Tendermint/CometBFT concept is exposed through REST, WebSocket, and gRPC
- **THEN** the public APIs use the same `CometBFT.Client.Core.Domain` record type instead of transport-specific duplicates
- **AND** cross-transport consumers can compare, persist, and process those objects without transport-dependent adapters

#### Scenario: Cross-transport interfaces stay domain-aligned
- **WHEN** a capability exists on multiple transports
- **THEN** the corresponding public interfaces expose compatible signatures and shared domain return types whenever the underlying protocol semantics match
- **AND** any unavoidable transport-specific difference is documented explicitly in XML docs, README, and OpenSpec

---

### Requirement: Low-Latency Encoding and Decoding

All serialization and deserialization code SHALL be optimized to minimize CPU overhead and heap allocations. The library SHALL target .NET 10 and exploit available zero-copy and source-generation capabilities.

#### Mandatory techniques

| Area | Rule |
|---|---|
| JSON (REST) | Use `System.Text.Json` with **source-generated `JsonSerializerContext`** (`[JsonSerializable]`) — no runtime reflection on hot paths |
| JSON streaming | Deserialize directly from `Stream` via `JsonSerializer.DeserializeAsync<T>(stream, ctx)` — never buffer the full response body to a `string` first |
| HTTP response | Use `HttpCompletionOption.ResponseHeadersRead` + `GetStreamAsync` on every GET endpoint to avoid double-buffering |
| Buffers | Prefer direct stream deserialization and avoid unnecessary buffering. `ArrayPool<byte>.Shared` or `Microsoft.IO.RecyclableMemoryStream` are only required when a manual buffering stage actually exists |
| gRPC | Parse protobuf messages directly from the gRPC response stream (`Parser.ParseFrom`) — no intermediate JSON transcoding on the gRPC path |
| Hot-path strings | Avoid `string` allocations in message parsing; prefer `ReadOnlySpan<byte>` / `Utf8JsonReader` where the framework permits |
| Newtonsoft.Json | **Forbidden on hot paths.** May only be used for one-off tooling or edge cases explicitly justified in code comments |
| **Typed JSON schemas** | **Every JSON shape — top-level and nested — SHALL be represented as an immutable `record`.** `JsonElement`, `JsonDocument`, `Dictionary<string, object>`, `dynamic`, `object`, raw JSON `string`, `JObject`, `JToken` are **forbidden** as domain type properties or method return values. Sub-objects (block header, events, event attributes, validator info, ABCI metadata…) each require their own named `record`. The `[JsonSerializable]` context must declare every such type. |

#### Scenario: Every nested JSON object is a typed record
- **WHEN** a CometBFT JSON response contains nested objects (e.g. `block_id`, `header`, `last_commit`, `result.events[].attributes[]`)
- **THEN** each nested shape maps to a named immutable `record` declared in `CometBFT.Client.Core`
- **AND** no property of any domain type is typed `JsonElement`, `object`, `dynamic`, or `Dictionary<string, object>`
- **AND** no public API method returns a raw JSON `string` representing a domain concept

#### Scenario: WebSocket events carry typed domain records (not raw JSON strings)
- **WHEN** a `NewBlock`, `Tx`, `Vote`, or `ValidatorSetUpdates` event is received over WebSocket
- **THEN** the corresponding `EventHandler<T>` exposes a fully-typed domain `record` (e.g. `EventHandler<Block>`, `EventHandler<TxResult>`, `EventHandler<Vote>`)
- **AND** the raw JSON string is never surfaced to the consumer

#### Scenario: JSON deserialized from stream without intermediate string
- **WHEN** `GetBlockAsync` is called
- **THEN** the HTTP response body is deserialized via `JsonSerializer.DeserializeAsync` reading directly from the response `Stream`
- **AND** no intermediate `string` or `byte[]` copy of the full body is created

#### Scenario: Source-generated serializer context covers all domain types including nested
- **WHEN** the `CometBFT.Client.Core` assembly is compiled
- **THEN** a `[JsonSerializable]`-annotated `JsonSerializerContext` exists declaring every public domain `record` — including all nested types — used in REST and WebSocket responses
- **AND** no call to `JsonSerializer.Serialize/Deserialize` uses the default reflection-based overload

#### Scenario: ArrayPool buffer reuse in HTTP pipeline
- **WHEN** a REST endpoint returns a payload ≥ 4 KB
- **THEN** the client rents a buffer from `ArrayPool<byte>.Shared` for the read operation and returns it after deserialization
- **AND** no `new byte[]` allocation occurs inside the response-reading loop

#### Scenario: gRPC path has no JSON transcoding
- **WHEN** a gRPC method is called
- **THEN** the response is parsed with `Google.Protobuf` binary codec only
- **AND** no JSON serialization step is introduced between the wire bytes and the domain record

---

### Requirement: Complete REST API Coverage

The REST client SHALL implement all public CometBFT RPC HTTP endpoints as defined in the latest stable release of https://github.com/cometbft/cometbft (`/rpc/openapi/openapi.yaml`), including endpoints tagged `Unsafe` when those routes are explicitly enabled on the target node. All methods SHALL be async with `CancellationToken` support.

#### Scenario: GetHealthAsync returns healthy status
- **WHEN** `GetHealthAsync(CancellationToken)` is called against a running node
- **THEN** the method returns without throwing and the response is valid

#### Scenario: GetBlockAsync with height returns block
- **WHEN** `GetBlockAsync(height: 1, CancellationToken)` is called
- **THEN** a `Block` record with matching Height is returned

#### Scenario: GetBlockAsync without height returns latest
- **WHEN** `GetBlockAsync(height: null, CancellationToken)` is called
- **THEN** the latest block is returned

#### Scenario: BroadcastTxSyncAsync returns tx hash
- **WHEN** `BroadcastTxSyncAsync(txBase64, CancellationToken)` is called with a valid transaction
- **THEN** a `BroadcastTxResult` with a non-empty Hash is returned

#### Scenario: REST client throws TendermintRestException on 5xx
- **WHEN** the server returns a 5xx error after all Polly retries are exhausted
- **THEN** a `TendermintRestException` is thrown with the HTTP status code

#### Scenario: REST client retries on transient failure
- **WHEN** the server returns 503 twice then 200
- **THEN** the client retries automatically (Polly) and returns the successful response

---

### Requirement: Complete WebSocket API Coverage

The WebSocket client SHALL subscribe to all public CometBFT event types (NewBlock, NewBlockHeader, Tx, Vote, ValidatorSetUpdates) as defined in the latest stable release of https://github.com/cometbft/cometbft. All event handlers SHALL be async-compatible.

#### Scenario: NewBlock event received after subscription
- **WHEN** the client subscribes to NewBlock events
- **THEN** each new block produced by the node raises the `NewBlockReceived` event with a valid `Block`

#### Scenario: Tx event received after subscription
- **WHEN** the client subscribes to Tx events
- **THEN** each committed transaction raises `TxExecuted` with a valid `TxResult` including Events and Attributes

#### Scenario: Client reconnects automatically after disconnect
- **WHEN** the WebSocket connection is dropped unexpectedly
- **THEN** the client attempts reconnection without requiring manual intervention

#### Scenario: WebSocket client throws TendermintWebSocketException on fatal error
- **WHEN** the server rejects the connection (auth failure, invalid endpoint)
- **THEN** a `TendermintWebSocketException` is thrown

---

### Requirement: gRPC Client

The gRPC client SHALL be compiled from the proto files of the latest stable CometBFT release (`/proto/cometbft/`) and expose all available gRPC services for that targeted release. All methods SHALL be async with `CancellationToken` support.

#### Scenario: Ping via gRPC succeeds
- **WHEN** `PingAsync(CancellationToken)` is called on a running gRPC endpoint
- **THEN** the method returns without throwing

#### Scenario: BroadcastTx via gRPC returns response
- **WHEN** `BroadcastTxAsync(txBytes, CancellationToken)` is called with a valid transaction
- **THEN** a `ResponseCheckTx` or `ResponseDeliverTx` is returned

#### Scenario: gRPC client throws TendermintGrpcException on unavailable
- **WHEN** the gRPC server is unreachable after Polly retries
- **THEN** a `TendermintGrpcException` is thrown

#### Scenario: Every available gRPC service is exposed consistently
- **WHEN** the targeted CometBFT release exposes multiple public gRPC services
- **THEN** `ITendermintGrpcClient` exposes async methods covering the complete supported service surface
- **AND** the concrete client, tests, and demos are updated together so the OpenSpec does not overstate partial coverage

---

### Requirement: Dependency Injection Registration

The library SHALL provide `IServiceCollection` extension methods for registering each transport client with options.

#### Scenario: AddTendermintRest registers ITendermintRestClient
- **WHEN** `services.AddTendermintRest(opts => opts.BaseUrl = "http://localhost:26657")` is called
- **THEN** `services.GetRequiredService<ITendermintRestClient>()` resolves without error

#### Scenario: AddTendermintWebSocket registers ITendermintWebSocketClient
- **WHEN** `services.AddTendermintWebSocket(opts => opts.BaseUrl = "ws://localhost:26657")` is called
- **THEN** `services.GetRequiredService<ITendermintWebSocketClient>()` resolves without error

#### Scenario: AddTendermintGrpc registers ITendermintGrpcClient
- **WHEN** `services.AddTendermintGrpc(opts => opts.BaseUrl = "http://localhost:9090")` is called
- **THEN** `services.GetRequiredService<ITendermintGrpcClient>()` resolves without error

---

### Requirement: Bash Scripts Wrapping dotnet CLI

The repository SHALL provide `build.sh`, `test.sh` and `publish.sh` scripts that wrap the corresponding `dotnet` commands with consistent defaults and accept the same parameters. For each local script, a Docker counterpart (`docker-build.sh`, `docker-test.sh`, `docker-publish.sh`) SHALL execute the **identical** local script inside an official .NET 10 SDK container — the Docker scripts contain no logic of their own.

```
scripts/
├── build.sh             ← exécution locale
├── test.sh
├── publish.sh
└── docker/
    ├── Dockerfile       ← FROM mcr.microsoft.com/dotnet/sdk:10.0
    ├── docker-compose.yml
    ├── build.sh         ← docker run ... ./scripts/build.sh "$@"
    ├── test.sh          ← docker run ... ./scripts/test.sh "$@"
    └── publish.sh       ← docker run ... -e NUGET_API_KEY ./scripts/publish.sh "$@"
```

**Container conventions** : image `mcr.microsoft.com/dotnet/sdk:10.0`, mount repo root as `/workspace`, working directory `/workspace`, `--rm` flag. NuGet API key passed via `NUGET_API_KEY` environment variable (never as a positional argument to the Docker script).

**Default values (mandatory)** : Every env var and CLI arg SHALL have a default value. Scripts MUST NOT exit solely because an endpoint env var is unset — they fall back to the validated public default set. Only `NUGET_API_KEY` (secret) is exempt from this rule.

| Env var | Default (validated public Cosmos Hub set) |
|---|---|
| `TENDERMINT_RPC_URL` | `https://cosmoshub.tendermintrpc.lava.build:443` |
| `TENDERMINT_WS_URL` | `wss://cosmoshub.tendermintrpc.lava.build:443/websocket` |
| `TENDERMINT_GRPC_URL` | `cosmoshub.grpc.lava.build` |
| `CONFIGURATION` | `Release` |

#### Scenario: build.sh builds in Release by default
- **WHEN** `./scripts/build.sh` is run without arguments
- **THEN** `dotnet build <sln> --configuration Release` is executed and exits 0

#### Scenario: test.sh runs with coverage gate
- **WHEN** `./scripts/test.sh` is run without arguments
- **THEN** `dotnet test` runs with `--collect "XPlat Code Coverage"` and fails if coverage < 90 %

#### Scenario: publish.sh packs and pushes
- **WHEN** `./scripts/publish.sh --api-key <key>` is run
- **THEN** `dotnet pack` then `dotnet nuget push` are executed with `--skip-duplicate`

#### Scenario: scripts/docker/build.sh runs build.sh inside the SDK container
- **WHEN** `./scripts/docker/build.sh` is run (with optional extra args)
- **THEN** `docker run --rm -v "$(pwd):/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 ./scripts/build.sh "$@"` is executed
- **AND** the exit code of the container matches the exit code of `build.sh`
- **AND** no build logic is duplicated inside `scripts/docker/build.sh`

#### Scenario: scripts/docker/test.sh runs test.sh inside the SDK container
- **WHEN** `./scripts/docker/test.sh` is run
- **THEN** `test.sh` is invoked inside the container and coverage artifacts are written to `coverage/` on the host via the volume mount
- **AND** the script exits non-zero if the coverage gate fails

#### Scenario: scripts/docker/publish.sh passes NUGET_API_KEY into the container
- **WHEN** `NUGET_API_KEY=<key> ./scripts/docker/publish.sh` is run
- **THEN** the container receives `NUGET_API_KEY` via `-e NUGET_API_KEY` and calls `./scripts/publish.sh` which reads it
- **AND** the API key is never written to disk or embedded in any script

#### Scenario: scripts/demo-rest.sh builds and runs the REST demo
- **WHEN** `./scripts/demo-rest.sh` is run without any env var set
- **THEN** `TENDERMINT_RPC_URL` defaults to `https://cosmoshub.tendermintrpc.lava.build:443`
- **AND** `dotnet run --project samples/CometBFT.Client.Demo.Rest --configuration Release` is executed against the validated public Cosmos Hub endpoint
- **WHEN** `TENDERMINT_RPC_URL=<custom-url> ./scripts/demo-rest.sh` is run
- **THEN** the custom URL is used instead of the default

#### Scenario: scripts/demo-ws.sh builds and runs the WebSocket demo
- **WHEN** `./scripts/demo-ws.sh` is run without any env var set
- **THEN** `TENDERMINT_WS_URL` defaults to `wss://cosmoshub.tendermintrpc.lava.build:443/websocket`
- **AND** the live event dashboard subscribes to NewBlock, NewBlockHeader, Tx, Vote, and ValidatorSetUpdates events on the validated public Cosmos Hub endpoint

#### Scenario: scripts/demo-grpc.sh builds and runs the gRPC demo
- **WHEN** `TENDERMINT_GRPC_URL=<url> ./scripts/demo-grpc.sh` is run
- **THEN** `dotnet run --project samples/CometBFT.Client.Demo.Grpc --configuration Release` is executed

#### Scenario: scripts/docker/demo-*.sh run demos inside the SDK container
- **WHEN** `TENDERMINT_RPC_URL=<url> ./scripts/docker/demo-rest.sh` is run
- **THEN** the container receives the env var(s) via `-e` flags and delegates to `./scripts/demo-rest.sh`
- **AND** no demo logic is duplicated inside the Docker script

---

### Requirement: Git Flow and Branch Protection Hooks

The repository SHALL enforce Git Flow branching (master/develop/feature/release/hotfix) and protect master and develop from direct pushes via git hooks.

#### Scenario: Direct push to master is blocked
- **WHEN** a developer attempts `git push origin master` directly
- **THEN** the pre-push hook exits with code 1 and prints an error message

#### Scenario: Direct push to develop is blocked
- **WHEN** a developer attempts `git push origin develop` directly
- **THEN** the pre-push hook exits with code 1 and prints an error message

#### Scenario: Invalid commit message is rejected
- **WHEN** a commit message does not match `^(feat|fix|docs|style|refactor|test|chore|ci)(\(.+\))?: .{1,100}`
- **THEN** the commit-msg hook exits with code 1

---

### Requirement: Test Coverage ≥ 90 % — Global and Per File

All library code SHALL be covered by automated tests achieving **≥ 90 % global line coverage** and **≥ 90 % line coverage for every individual source file**.

| Level | Enforcement |
|---|---|
| **Global** | All source assemblies combined ≥ 90 % line coverage |
| **Per file** | Each source file independently ≥ 90 % line coverage |

A high-coverage file MUST NOT compensate for a low-coverage one. The global aggregate passes only when every individual file also passes.

**Coverage is enforced at two points:**
1. **Pre-push hook** — runs the repository test command and coverage validation; blocks push if the global or per-file gate fails
2. **CI pipeline** — runs the same gate; fails the build with an explicit message naming the failing file(s) and the global percentage when below threshold

Implementation note: the repository MAY use Coverlet, `dotnet-coverage`, or a custom validation script, but the acceptance rule is the same: global line coverage >= 90 % and per-file line coverage >= 90 %.

#### Scenario: Coverage gate enforced globally
- **WHEN** tests run across all src assemblies
- **THEN** the combined line coverage is ≥ 90 %
- **AND** `./scripts/test.sh` exits non-zero when the global line coverage drops below 90 %

#### Scenario: Coverage gate enforced per file
- **WHEN** coverage validation runs after the test suite
- **THEN** every source file shows ≥ 90 % line coverage
- **AND** files below 90 % are flagged in command output and CI logs

#### Scenario: Pre-push hook blocks push on coverage failure
- **WHEN** a developer runs `git push`
- **THEN** the pre-push hook executes the repository test and coverage validation flow before the push is sent
- **AND** if the global or per-file threshold fails, the hook exits non-zero and the push is rejected with an explicit error message
- **AND** the push succeeds only when all coverage thresholds pass

#### Scenario: Coverage report generated for local diagnosis
- **WHEN** `./scripts/test.sh` completes
- **THEN** the repository produces machine-readable coverage output and MAY also generate a local HTML report for diagnosis
- **AND** uploading the coverage report as a CI artifact is optional and not required for acceptance

#### Scenario: Coverage gate blocks CI on failure
- **WHEN** a code change reduces coverage below 90 % globally or below 90 % for any file
- **THEN** CI fails with an explicit message naming the failing file(s) and/or the global percentage

---

### Requirement: Integration Tests — Real Endpoints, Skippable

The library SHALL provide integration tests that exercise the real REST, WebSocket, and gRPC transports against validated public CometBFT endpoints. CI SHALL run them against an explicit validated endpoint set.

#### Scenario: Integration test can be disabled when env var absent
- **WHEN** the required endpoint env var is not set
- **THEN** integration tests depending on that variable are skipped rather than hard-failing local developer workflows

#### Scenario: Integration test runs against real endpoint
- **WHEN** the endpoint env var is set to a valid URL
- **THEN** the client initializes via DI, calls at least one real endpoint, and returns a valid deserialized response without throwing

#### Scenario: WebSocket integration test receives a typed event
- **WHEN** `TENDERMINT_WS_URL` is set to a valid WebSocket endpoint
- **THEN** the test connects, subscribes to at least one public event type, receives at least one typed event, and disconnects cleanly

#### Scenario: gRPC integration test reaches the broadcast API
- **WHEN** `TENDERMINT_GRPC_URL` is set to a valid gRPC endpoint
- **THEN** the test resolves the gRPC client from DI, calls at least one real RPC, and validates success or the expected typed error path

#### Scenario: Integration tests included in coverage report
- **WHEN** integration tests run (env vars set)
- **THEN** their coverage is merged into the global coverage report alongside unit test coverage

---

### Requirement: E2E Tests — Full Flow

The library SHALL provide end-to-end tests that exercise a complete user flow from DI registration through real network calls to deserialized domain objects. E2E tests are tagged `[Trait("Category","E2E")]`, and CI SHALL run them against an explicit validated endpoint set.

#### Scenario: E2E test executes full client lifecycle
- **WHEN** an E2E test runs with endpoint env vars set
- **THEN** it creates a `ServiceCollection`, registers the client via `Add*` extension, resolves the client from DI, calls ≥ 3 distinct endpoints, and asserts the returned domain records are non-null and structurally valid

#### Scenario: E2E test can be disabled when env var absent locally
- **WHEN** the required endpoint env var is not set
- **THEN** the E2E test is skipped rather than failing solely because the developer did not opt into live-network execution

#### Scenario: CI runs E2E as a separate step
- **WHEN** the CI pipeline runs
- **THEN** E2E tests execute in a dedicated step after unit and integration tests, using validated public endpoint env vars
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
- **AND** suppression of CS1591 without a justification comment fails the code review

#### Scenario: Nullable violations are errors
- **WHEN** a nullable dereference warning is introduced (CS8602, CS8603, CS8604…)
- **THEN** the build fails — the developer must fix the null-safety issue, not suppress it

---

### Requirement: Package Freshness — Zéro Dépendance Obsolète

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

The library SHALL document the CometBFT protocol version it targets in `Directory.Build.props` (`<ProtocolVersion>`), in `README.md`, and in `CHANGELOG.md`.

#### Scenario: ProtocolVersion is set in Directory.Build.props
- **WHEN** `Directory.Build.props` is read
- **THEN** a `<ProtocolVersion>` property is present with the targeted CometBFT release tag (e.g., `v0.38.x`)

#### Scenario: CHANGELOG references protocol version
- **WHEN** a new library version is released
- **THEN** `CHANGELOG.md` contains a line referencing the CometBFT release URL and version targeted

#### Scenario: README links to official repo and version
- **WHEN** `README.md` is read
- **THEN** it contains a link to https://github.com/cometbft/cometbft and the protocol version badge

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

### Requirement: Demo REST (`samples/CometBFT.Client.Demo.Rest/`)

The repository SHALL provide a REST demo that connects to a real CometBFT node via HTTP and makes the full supported REST capability set observable to the user. Frequently-used endpoints may be shown in a live Spectre.Console dashboard refreshed every 10 seconds; less-common endpoints may be exposed through explicit demo actions, menus, or diagnostics, but they SHALL still be represented in the demo surface.

**Config** : `TENDERMINT_RPC_URL` env var or `--rpc-url` CLI arg, with a validated public Cosmos Hub default when neither is supplied.

#### Scenario: REST demo starts and renders dashboard
- **WHEN** the REST demo is run with a valid `TENDERMINT_RPC_URL`
- **THEN** a Spectre.Console live dashboard renders with panels: Header, Health/Status, Latest Block, Validators, ABCI Info, Log
- **AND** all panels are populated on startup and refreshed every 10 seconds

#### Scenario: REST demo calls dashboard endpoints
- **WHEN** the refresh cycle runs
- **THEN** the following are called and displayed: `GetHealthAsync`, `GetStatusAsync`, `GetBlockAsync(null)`, `GetBlockResultsAsync(null)`, `GetValidatorsAsync()`, `GetAbciInfoAsync()`
- **AND** each panel shows the response summary with call timestamp and latency in ms

#### Scenario: REST demo exposes the full supported REST surface
- **WHEN** the demo is reviewed against the supported REST API matrix
- **THEN** every REST method exposed by `ITendermintRestClient` is either rendered directly in the dashboard or reachable through an explicit demo interaction path
- **AND** methods backed by `Unsafe` endpoints are clearly marked as requiring a node with unsafe RPC enabled

#### Scenario: REST demo handles transient errors gracefully
- **WHEN** a REST call fails (timeout, 5xx)
- **THEN** the panel displays the error message with timestamp and the next refresh retries automatically

---

### Requirement: Demo WebSocket (`samples/CometBFT.Client.Demo.WebSocket/`)

The repository SHALL provide a WebSocket demo that subscribes to all available CometBFT event types and displays incoming events in real time in a live Spectre.Console dashboard — no polling, purely event-driven.

**Config** : `TENDERMINT_WS_URL` env var or `--ws-url` CLI arg, with a validated public Cosmos Hub default when neither is supplied.

#### Scenario: WebSocket demo subscribes to all event types and renders live
- **WHEN** the WebSocket demo starts
- **THEN** it subscribes to NewBlock, NewBlockHeader, Tx, Vote, and ValidatorSetUpdates events
- **AND** the dashboard panels update immediately upon each received event (no fixed refresh interval)

#### Scenario: NewBlock event updates Live Blocks panel
- **WHEN** a NewBlock event is received
- **THEN** the Live Blocks panel prepends the new block (height, time, proposer, #txs) and keeps the last 20 entries

#### Scenario: Tx event updates Live Transactions panel
- **WHEN** a Tx event is received
- **THEN** the Live Transactions panel prepends the transaction (hash, height, code, gas used, gas wanted) and keeps the last 20 entries

#### Scenario: Vote event is logged
- **WHEN** a Vote event is received
- **THEN** a log line is appended with validator address, block height and round

#### Scenario: WebSocket demo reconnects automatically
- **WHEN** the WebSocket connection drops
- **THEN** the demo reconnects and resubscribes without requiring manual intervention
- **AND** a WARN log line is emitted indicating the reconnection

---

### Requirement: Demo gRPC (`samples/CometBFT.Client.Demo.Grpc/`)

The repository SHALL provide a gRPC demo that connects to a real CometBFT gRPC endpoint, polls all available gRPC services every 10 seconds, and — when server-side streaming RPCs are available in the targeted CometBFT release — subscribes to them for real-time event delivery instead of polling.

**Config** : `TENDERMINT_GRPC_URL` env var or `--grpc-url` CLI arg, with a validated public Cosmos Hub default when neither is supplied.
**Streaming** : If `cometbft.services.block.v1.BlockService` (or equivalent streaming service) is present in the targeted proto release, use server-side streaming for new block events; fall back to 10 s polling otherwise.

#### Scenario: gRPC demo starts and renders dashboard
- **WHEN** the gRPC demo starts
- **THEN** a Spectre.Console live dashboard renders with panels: Header, BroadcastAPI, Streaming Events (if available), Log

#### Scenario: gRPC polling refreshes every 10 seconds
- **WHEN** no streaming RPC is available
- **THEN** `PingAsync` is called every 10 seconds and the panel shows latency + last call timestamp

#### Scenario: gRPC streaming delivers real-time block events
- **WHEN** the targeted CometBFT proto release includes a streaming `BlockService`
- **THEN** the demo subscribes to the stream and updates the Live Blocks panel immediately upon each received event (no 10 s delay)
- **AND** a log line notes "gRPC streaming active — polling disabled"

#### Scenario: gRPC demo falls back to polling when streaming unavailable
- **WHEN** no streaming service is present in the proto
- **THEN** the demo logs "gRPC streaming not available — polling every 10 s" and polls instead

#### Scenario: Demo layout — gRPC dashboard
- **WHEN** the gRPC demo is running
- **THEN** the dashboard shows: Header (chain endpoint, protocol version), BroadcastAPI (Ping latency), Live Blocks (streaming or polled), Log

---

### Review Findings — 2026-04-10

> Sources: Blind Hunter · Edge Case Hunter · Acceptance Auditor

#### Decision Needed

- [x] [Review][Decision] **D1 — BroadcastTx* via GET ou POST ?** — **Décision : POST JSON-RPC.** `BroadcastTxSyncAsync`, `BroadcastTxAsync`, `BroadcastTxCommitAsync` passent la transaction encodée en base64 dans le body d'une requête POST JSON-RPC (`method`, `params`). Le query-string GET est abandonné pour éviter les limites d'URL (~8 KB) et l'exposition dans les logs serveur. [`TendermintRestClient.cs:147-171`]

- [x] [Review][Decision] **D2 — `VoteReceived` : string brute ou type `Vote` ?** — **Décision : introduire un record `Vote`.** `ITendermintWebSocketClient.VoteReceived` expose `EventHandler<Vote>`. Le record `Vote` est déclaré dans `CometBFT.Client.Core` et enregistré dans `TendermintJsonContext`. [`ITendermintWebSocketClient.cs:29`]

- [x] [Review][Decision] **D3 — Renommer `BroadcastTxAsyncAsync` → `BroadcastTxAsync` ?** — **Décision : renommer.** `BroadcastTxAsyncAsync` est renommé `BroadcastTxAsync` dans `ITxService` et `TendermintRestClient`. Breaking change accepté (bibliothèque non encore publiée). [`ITxService.cs`, `TendermintRestClient.cs`]

- [x] [Review][Decision] **D4 — Préfixe `0x` sur les paramètres `hash`** — **Décision : normalisation interne.** `GetBlockByHashAsync(string hash)` et `GetTxAsync(string hash)` acceptent un `string` et ajoutent le préfixe `0x` s'il est absent (`if (!hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase))`). Le contrat public documente que les deux formats (`{hex}` et `0x{hex}`) sont acceptés. [`TendermintRestClient.cs:77, 120`]

#### Patches

- [x] [Review][Patch] **P1 — WebSocket: `ParseNewBlock` hash toujours vide** — Corrigé : chemin `result.data.value.block_id.hash` utilisé. [`TendermintWebSocketClient.cs`]

- [x] [Review][Patch] **P2 — WebSocket: `ParseTxResult` hash = bytes tx + Events vide** — Corrigé : hash lu depuis `result.events["tx.hash"][0]`, events depuis `result.data.value.TxResult.result.events`. [`TendermintWebSocketClient.cs`]

- [x] [Review][Patch] **P3 — WebSocket: exception non-JSON tue le pipeline Rx définitivement** — Corrigé : `catch (Exception)` large dans `OnMessageReceived` protège le pipeline. [`TendermintWebSocketClient.cs`]

- [x] [Review][Patch] **P4 — WebSocket: race TOCTOU sur `_client` + fuite sur échec `StartOrFail`** — Corrigé : `SemaphoreSlim _connectLock` + cleanup `newClient?.Dispose()` en cas d'échec. [`TendermintWebSocketClient.cs`]

- [x] [Review][Patch] **P5 — WebSocket: `DisconnectAsync` ne remet pas `_client` à null** — Corrigé : `_client = null` assigné dans `DisconnectAsync`. [`TendermintWebSocketClient.cs`]

- [x] [Review][Patch] **P6 — WebSocket: abonnements non re-soumis après reconnexion automatique** — Corrigé : `OnReconnected` rejoue tous les `_activeSubscriptions`. [`TendermintWebSocketClient.cs`]

- [x] [Review][Patch] **P7 — REST: `GetHealthAsync` avale `OperationCanceledException`** — Corrigé : `catch (OperationCanceledException) { throw; }` avant le catch `HttpRequestException`. [`TendermintRestClient.cs`]

- [x] [Review][Patch] **P8 — REST: `GetRpcResultAsync` ne vérifie pas le statut HTTP** — Corrigé : `response.EnsureSuccessStatusCode()` présent dans `GetRpcResultAsync`. [`TendermintRestClient.cs`]

- [x] [Review][Patch] **P9 — REST: déréférencements `!` non protégés dans tout le parsing** — Corrigé : `raw.Header!` remplacé par guard `if (raw.Header is null) throw new TendermintRestException(...)` dans `MapBlock`. [`TendermintRestClient.cs`]

- [x] [Review][Patch] **P10 — Polly: `BrokenCircuitException` non exclue du retry + pas de jitter** — Jitter ajouté. `BrokenCircuitException` non incluse dans `HandleTransientHttpError()` → propagée immédiatement sans retry. [`ServiceCollectionExtensions.cs`]

- [x] [Review][Patch] **P11 — Polly: `HttpClient.Timeout` absorbe les retries** — Corrigé : `HttpClient.Timeout = Timeout.InfiniteTimeSpan` + `Policy.TimeoutAsync<HttpResponseMessage>(options.Timeout)` ajouté comme premier handler (par tentative). [`ServiceCollectionExtensions.cs`]

- [x] [Review][Patch] **P12 — `build.sh` : `$1` dupliqué + `$@` non quoté + `CONFIGURATION` morte** — Corrigé : `"$@"` quoté, pas de variable morte, pas de duplication. [`scripts/build.sh`]

- [x] [Review][Patch] **P13 — `test.sh` : `$@` non quoté** — Corrigé : `"$@"` quoté. [`scripts/test.sh`]

- [x] [Review][Patch] **P14 — REST: `AbciQueryAsync` double-quote le paramètre `path`** — Corrigé : paramètre passé sans guillemets supplémentaires. [`TendermintRestClient.cs`]

- [x] [Review][Patch] **P15 — README : commentaire gRPC stale ("stub; add proto files")** — Corrigé : README décrit le gRPC avec les bindings proto générés. [`README.md`]

#### Deferred

- [x] [Review][Defer] `TendermintGrpcClient` null-check options dans ctor public — pre-existing defensive gap, faible risque. [`TendermintGrpcClient.cs:24`] — deferred, pre-existing
- [x] [Review][Defer] Options enregistrées comme singleton mutable — refactor `IOptions<T>` hors scope. [`ServiceCollectionExtensions.cs:34`] — deferred, pre-existing
- [x] [Review][Defer] `AbciQueryAsync` data null omis silencieusement — edge case rare, documentable. [`TendermintRestClient.cs:146`] — deferred, pre-existing
- [x] [Review][Defer] `ParseBlock` retourne height=0 sur parse failure — masque bugs serveur, faible impact. [`TendermintRestClient.cs:178`] — deferred, pre-existing
- [x] [Review][Defer] `long.Parse` sans `InvariantCulture` — Tendermint envoie toujours des décimaux invariants. [`TendermintRestClient.cs:55-75`] — deferred, low risk
- [x] [Review][Defer] `SendSubscribeAsync` `Task.Run` inutile — cosmétique, non bloquant. [`TendermintWebSocketClient.cs:141`] — deferred, pre-existing
- [x] [Review][Defer] `PollyRetryTests` lent (backoff production 2s+4s) + `LogEntries` race — nécessite DI pour injecter policy de test. [`PollyRetryTests.cs`] — deferred, test infra
- [x] [Review][Defer] `commit-msg` hook manque `build`, `perf`, `revert` — faible impact, extensible. [`.git/hooks/commit-msg`] — deferred, low priority
- [x] [Review][Defer] `pre-push` hook — edge cases `--mirror`/delete push — scénarios rares. [`.git/hooks/pre-push`] — deferred, low priority
