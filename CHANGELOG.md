# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
