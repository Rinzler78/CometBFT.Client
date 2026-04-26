# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **WebSocket — `Disconnected` / `Reconnected` events** (`ICometBftWebSocketClient`,
  `CometBftWebSocketClient`): two new lifecycle events on the WebSocket interface.
  `Disconnected` fires when the underlying TCP connection drops (a reconnection attempt is
  already in progress); `Reconnected` fires after the connection is restored and all active
  subscriptions have been replayed. Neither fires on the initial connection.
- **Demo Dashboard — disconnect/reconnect status feedback** (`DashboardBackgroundService`):
  `OnDisconnected` handler sets the UI badge to `"Reconnecting…"` (`isConnected: false`) and
  appends an event-log entry; `OnWsReconnected` restores it to `"Reconnected"` (`isConnected: true`)
  with a corresponding log entry. The UI now reflects the full connection lifecycle instead of
  remaining stuck on `"Connected"`.

### Fixed
- **Demo Dashboard — handler leak** (`DashboardBackgroundService`): event handler registrations
  moved inside the `try` block so the `finally` clause always deregisters them, even if an
  exception is raised before `ConnectAsync`.
- **Demo Dashboard — degraded status**: connection status now reflects actual subscribe success
  count — `"Connected"` (7/7) or `"Degraded (n/7 topics)"` when the relay rejects some topics
  due to the `max_subscriptions_per_client = 5` rate limit.
- **Demo Dashboard — time format**: all timestamps now use the local timezone with a full date
  (`yyyy-MM-dd HH:mm:ss`). Previously block times were displayed in UTC without a date
  (`HH:mm:ss`), which made events ambiguous after a reconnection that crosses a day boundary.
  Affected: `BlockRow.Time`, `LatestBlockTime`, `EventLogRow.Timestamp`, and the header
  description in `OnNewBlockHeader`.
- **Demo Dashboard — fatal error visibility**: unhandled exceptions in `ExecuteAsync` are now
  surfaced in the UI event log (`AppendEventLog("fatal", …)`) instead of being silently swallowed
  and showing `"Disconnected"` identically to a clean shutdown.
- **Demo Dashboard — cancellation**: `OnValidatorSetUpdated` now passes `stoppingToken` to
  `RefreshValidatorsAsync` instead of `CancellationToken.None`.
- **Demo Dashboard — parallel refresh**: periodic refresh loop now runs `RefreshNodeInfoAsync`
  and `RefreshNetInfoAsync` concurrently via `Task.WhenAll`.
- **Demo Dashboard — Abbreviate**: removed private `Safe()` helper; consolidated on
  `MainWindowViewModel.Abbreviate()` (now `internal static`) as the single truncation helper.
- **OpenSpec — `NewBlockEventsData.Events` type**: corrected from `IReadOnlyList<AbciEventEntry>`
  to `IReadOnlyList<CometBftEvent>` in `proposal.md` and `spec-diff.md`; added two-level
  structure note (`CometBftEvent.Type` + `CometBftEvent.Attributes`).

### Added
- **API docs** (`ICometBftWebSocketClient`): XML `<remarks>` documenting the concurrent
  burst-subscribe pattern (`Task.WhenAll`), `ErrorOccurred` pre-wiring requirement, relay
  rate-limit behaviour (`max_subscriptions_per_client = 5`), and `*Stream` observable
  lifecycle (pre-initialized at construction, safe to subscribe before `ConnectAsync`).
- **Sample** (`CometBFT.Client.Sample`): updated to demonstrate the `Task.WhenAll`
  burst-subscribe pattern; serial `await` replaced to avoid the 30–45 s relay ACK stall.
- **Tests** (`CometBFT.Client.Demo.Dashboard.Tests`): new test assembly covering
  `MainWindowViewModel` (8 tests), `Resilient` helper (4 tests), and WebSocket
  subscribe rate-limit behaviour (3 tests — burst of 7 with limit 5 → exactly 2 rejections
  via `ErrorOccurred`).
- **Pre-commit hook** `dotnet-vulnerable`: detects vulnerable NuGet packages (including
  transitive dependencies via `--include-transitive`) on every `.csproj` /
  `packages.lock.json` change. Mirrors the `NU1902` gate in CI restore and prevents
  vulnerable packages from reaching the remote.

### Changed
- **CI — E2E resilience**: `continue-on-error: true` removed from the `e2e-tests` job.
  `Rest_Flow` and `Grpc_Flow` E2E tests now catch network-layer exceptions
  (`HttpRequestException`, `OperationCanceledException`) and call `Assert.Skip` so
  testnet instability is reported as a skip, not a false failure. Implementation bugs
  (`JsonException`, assertion failures) still fail the job.

### Dependencies
- `WireMock.Net` 2.3.0 → 2.4.0 (test dependency): resolves `OpenTelemetry.Api` and
  `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.14.0 moderate-severity CVEs
  (advisory IDs: g94r-2vxg-569j, mr8r-92fq-pj8p, q834-8qmm-v933).

## [2.1.0] - 2026-04-22

### Added
- **WebSocket Events Completeness** — protocol target bumped from v0.38.9 to v0.39.1.
- 4 new domain types (sealed records): `NewBlockEventsData`, `CompleteProposalData`,
  `ValidatorSetUpdatesData`, `NewEvidenceData`.
- 5 new `IObservable<T>` streams on `ICometBftWebSocketClient`:
  - `NewBlockEventsStream` (🔴 critical — committed block + all ABCI events for DeFi indexing)
  - `CompleteProposalStream` (consensus complete-proposal step)
  - `ValidatorSetUpdatesStream` (validator set changed)
  - `NewEvidenceStream` (new evidence submitted)
  - `ConsensusInternalStream` (merged stream: TimeoutPropose, TimeoutWait, Lock, Unlock, Relock,
    PolkaAny, PolkaNil, PolkaAgain, MissingProposalBlock)
- 4 new `Subscribe*Async` methods: `SubscribeNewBlockEventsAsync`, `SubscribeCompleteProposalAsync`,
  `SubscribeNewEvidenceAsync`, `SubscribeConsensusInternalAsync`.
- All existing subscriptions, events, and interfaces are preserved — fully backward compatible.

## [2.0.0] - 2026-04-22

### Breaking Changes
- `IBlockService` now inherits `IBlockService<Block>` (shim). Existing code that depends on
  `IBlockService` directly is unaffected, but implementations must now satisfy the generic
  contract (i.e., return covariant types where the shim allows).
- `ITxService` now inherits `ITxService<TxResult>` (shim).
- `IValidatorService` now inherits `IValidatorService<Validator>` (shim).
- `ICometBftRestClient` now inherits `ICometBftRestClient<Block, TxResult, Validator>` (shim).
- `ICometBftWebSocketClient<TTx>` now inherits
  `ICometBftWebSocketClient<TTx, Block<TTx>, TxResult<TTx>, Validator>` (shim).
- Existing implementations (`CometBftRestClient`, `CometBftWebSocketClient<TTx>`) are
  unmodified and satisfy all shims without any code change.

### Added
- `abstract record BlockBase(long Height, string Hash, DateTimeOffset Time, string Proposer)` —
  shared base for `Block` and `Block<TTx>`, enabling consumer inheritance without redefinition.
- `abstract record TxResultBase(Hash, Height, Index, Code, Data, Log, Info, GasWanted, GasUsed, Events, Codespace)` —
  shared base for `TxResult` and `TxResult<TTx>`.
- `IBlockService<TBlock> where TBlock : BlockBase` — generic service interface.
- `ITxService<TTxResult> where TTxResult : TxResultBase` — generic service interface.
- `IValidatorService<TValidator> where TValidator : Validator` — generic service interface.
- `ICometBftRestClient<TBlock, TTxResult, TValidator>` — 3-parameter aggregate interface.
- `ICometBftWebSocketClient<TTx, TBlock, TTxResult, TValidator>` — 4-parameter aggregate interface with
  `where TBlock : Block<TTx>` and `where TTxResult : TxResult<TTx>` constraints.
- `AddCometBftRest<TInterface, TClient>()` — generic DI overload; runs the same Polly pipeline
  for any `TClient : TInterface : ICometBftRestClient`.
- `AddCometBftWebSocket<TTx, TInterface, TClient>()` — generic DI overload for typed WebSocket clients.

### Changed
- `Block`, `Block<TTx>` — removed `sealed`; both inherit `BlockBase`.
- `TxResult`, `TxResult<TTx>` — removed `sealed`; both inherit `TxResultBase`.
- `Validator`, `BroadcastTxResult`, `BlockHeader`, `ConsensusParamsInfo`, `NodeInfo`,
  `SyncInfo`, `UnconfirmedTxsInfo`, `BlockchainInfo` — removed `sealed` to allow
  application-layer enrichment (e.g. Cosmos adds `AppHash`, `ChainId`, `RawLog`).
- `AddCometBftRest()` and `AddCometBftWebSocket<TTx>()` now delegate to their generic
  overloads — no behavioral change.
- Protocol-pure types (`Vote`, `AbciEventEntry`, `AbciQueryResponse`, `AbciProofOps`,
  `AbciProofOp`, `GenesisChunk`, `ProtocolVersion`, `NetworkInfo`, `NetworkPeer`,
  `CometBftEvent`, `RawTxCodec`) remain `sealed`.

## [1.0.0] - 2026-04-20

> **Breaking-change notice**
> This release removes public API members (`ICometBftSdkGrpcClient`,
> `CometBftSdkGrpcClient`, `CometBftSdkGrpcOptions`, and `AddCometBftSdkGrpc`).
> This is the first major release because the package now enforces the CometBFT-native
> transport boundary strictly.

### Breaking
- Removed the Cosmos SDK gRPC layer from `Rinzler78.CometBFT.Client`.
- Consumers must migrate `cosmos.base.tendermint.v1beta1` usage to
  `Rinzler78.Cosmos.Client`.
- Consumers using `AddCometBftClient()` now receive CometBFT-native transports only:
  REST, WebSocket, and the native gRPC BroadcastAPI.

### Removed
- `ICometBftSdkGrpcClient`, `CometBftSdkGrpcClient`, `CometBftSdkGrpcOptions` —
  `cosmos.base.tendermint.v1beta1.Service` is a Cosmos SDK addition and does not
  belong in the CometBFT layer. These types move to `Rinzler78.Cosmos.Client`.
- `AddCometBftSdkGrpc()` DI extension method — removed with the types above.
- `cosmos/base/tendermint/v1beta1/query.proto` and its generated C# stubs —
  removed from `CometBFT.Client.Grpc`.
- `--grpc-url` CLI flag and `COMETBFT_GRPC_URL` env var from the demo dashboard —
  the dashboard now uses REST only (WebSocket + REST are sufficient).

### Changed
- `AddCometBftClient()` now registers REST + gRPC BroadcastAPI + WebSocket only
  (previously also registered SDK gRPC).
- `DashboardBackgroundService` replaces the three `ICometBftSdkGrpcClient` calls
  with their `ICometBftRestClient` equivalents:
  `GetLatestBlockAsync()` → `GetBlockAsync()`,
  `GetStatusAsync()` → `GetStatusAsync()`,
  `GetLatestValidatorsAsync()` → `GetValidatorsAsync()`.
- `CometBftClientOptions` doc comment updated to reflect REST + gRPC BroadcastAPI + WebSocket scope.
- `DemoDefaults.GrpcUrl` comment updated: no longer mentions `ICometBftSdkGrpcClient`.

### Added
- Unified real-time Avalonia 12 dashboard (`samples/CometBFT.Client.Demo.Dashboard/`)
  combining WebSocket events and REST polling in a single window
  with a Mintscan-inspired dark design system (deep-navy palette, KPI stats row,
  validators with voting-power `ProgressBar`, blocks and transactions side by side,
  event log with category-typed entries)
- `ValidatorRow.Rank` and `ValidatorRow.VotingPowerPct` computed from total voting power
- `TxRow.StatusText` (`OK` / `ERR`) and `TxRow.IsSuccess` computed properties
- `EventLogRow.Category` string (`block`, `tx`, `vote`, `validator`, `header`, `error`)
- `MainWindowViewModel.SyncStatusText` computed property via `[NotifyPropertyChangedFor]`
- `./scripts/demo.sh` — single launcher script for the unified dashboard demo
- OpenSpec requirement added for Demo Unified Dashboard in `openspec/`

### Removed
- `samples/CometBFT.Client.Demo.Rest/`, `samples/CometBFT.Client.Demo.WebSocket/`,
  and `samples/CometBFT.Client.Demo.Grpc/` — consolidated into the unified Dashboard

---

## [0.2.0] - 2026-04-19

Protocol: [CometBFT v0.38.9](https://github.com/cometbft/cometbft/releases/tag/v0.38.9)

### Added
- `ITxCodec<TTx>` interface and generic wrappers `Block<TTx>`, `TxResult<TTx>` — decodes
  raw transaction bytes into a caller-supplied type on the WebSocket hot path
- `AddCometBftWebSocket<TTx>(configure, codec)` DI extension for the generic WebSocket client
- `ICometBftSdkGrpcClient` — four additional `cosmos.base.tendermint.v1beta1` methods:
  `GetSyncingAsync()`, `GetBlockByHeightAsync(long)`, `GetValidatorSetByHeightAsync(long)`,
  `ABCIQueryAsync(string, byte[], long, bool)`
- `AddCometBftClient(Action<CometBftClientOptions>?)` — unified DI registration method that
  registers REST, WebSocket, gRPC, and SDK gRPC clients in a single call
- `.env.example` template with documented environment variable names
- Thread-safety fixes in `CometBftWebSocketClient` and DI isolation guards

### Fixed
- `NotNullAttribute` added on nullable-forgiven parameters throughout `Core` and `WebSocket`
- `CometBftWebSocketClient` state machine hardened against concurrent `ConnectAsync` calls
- `DecodeRawAsync` error context improved to include raw bytes length on failure
- `IServiceCollection` isolation: `AddCometBft*` no longer mutates a shared options instance

---

## [0.1.0] - 2026-04-18

Protocol: [CometBFT v0.38.9](https://github.com/cometbft/cometbft/releases/tag/v0.38.9)

### Added
- Initial implementation of `Rinzler78.CometBFT.Client` targeting CometBFT protocol v0.38.9
- `CometBFT.Client.Core` — immutable `record` domain types (`Block`, `BlockHeader`,
  `TxResult`, `Event`, `Attribute`, `NodeInfo`, `SyncInfo`, `Validator`,
  `BroadcastTxResult`); segregated service interfaces; options classes; typed exceptions
- `CometBFT.Client.Rest` — full REST/JSON-RPC 2.0 client with Polly-based resilience
  - Endpoints covered: health, status, block, block by hash, block results, validators,
    tx search, tx by hash, broadcast async/sync/commit, abci info, abci query,
    net_info, blockchain, header, header_by_hash, commit, genesis, genesis_chunked,
    dump_consensus_state, consensus_state, consensus_params, unconfirmed_txs,
    num_unconfirmed_txs, block_search, broadcast_evidence, check_tx, dial_seeds, dial_peers
- `CometBFT.Client.WebSocket` — real-time event subscription client with auto-reconnect;
  events: `NewBlockReceived`, `TxExecuted`, `VoteReceived`, `ValidatorSetUpdated`,
  `NewBlockHeaderReceived`, `ErrorOccurred`
- `CometBFT.Client.Grpc` — CometBFT `BroadcastAPI` gRPC client (`ICometBftGrpcClient`:
  `PingAsync`, `BroadcastTxAsync`); `CometBftSdkGrpcClient` targeting Cosmos SDK
  `cosmos.base.tendermint.v1beta1.Service` (`ICometBftSdkGrpcClient`: `GetStatusAsync`,
  `GetLatestBlockAsync`, `GetLatestValidatorsAsync`)
- `CometBFT.Client.Extensions` — `IServiceCollection` extension methods:
  `AddCometBftRest`, `AddCometBftWebSocket`, `AddCometBftGrpc`, `AddCometBftSdkGrpc`
- Unit tests with WireMock.Net (REST), NSubstitute (gRPC, WebSocket)
- Integration tests and E2E tests against real Cosmos Hub public endpoints
  (skip-on-missing-env-var pattern); global line coverage ≥ 90 %
- GitHub Actions CI (`ci.yml`: build + format + unit/integration/E2E + coverage gate)
  and tag-triggered publish workflow (`publish.yml`)
- Git Flow configuration, commit-msg hook, pre-commit hooks (`dotnet format`, `detect-secrets`,
  `cspell` English-only check), pre-push branch-protection and coverage gate
- `CONTRIBUTING.md` with full development workflow, coding conventions, and test standards

### Changed
- All `Tendermint`-prefixed public identifiers renamed to `CometBft` prefix
  (`ITendermintRestClient` → `ICometBftRestClient`, etc.);
  wire-level names (`tendermint.rpc.grpc`, `GrpcProtocol.TendermintLegacy`) preserved for
  backward-compatibility
- `WebSocketMessageParser` extracted from `CometBftWebSocketClient` into a dedicated
  internal class with its own unit-test suite
- SOLID/clean-code remediation applied across all source projects: single-responsibility
  split, dependency inversion via internal interfaces, dead-code removal, naming consistency
