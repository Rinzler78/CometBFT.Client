# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial implementation targeting CometBFT protocol version v0.38.9
- `CometBFT.Client.Core` — domain types (`Block`, `BlockHeader`, `TxResult`, `Event`, `Attribute`,
  `NodeInfo`, `SyncInfo`, `Validator`, `BroadcastTxResult`), service interfaces, options, and exceptions
- `CometBFT.Client.Rest` — full REST/JSON-RPC 2.0 client with Polly-based resilience
  - Endpoints: health, status, block, block by hash, block results, validators,
    tx search, tx by hash, broadcast async/sync/commit, abci info, abci query,
    net_info, blockchain, header, header_by_hash, commit, genesis, genesis_chunked,
    dump_consensus_state, consensus_state, consensus_params, unconfirmed_txs,
    num_unconfirmed_txs, block_search, broadcast_evidence, check_tx, dial_seeds, dial_peers
- `CometBFT.Client.WebSocket` — real-time event subscription client using `Websocket.Client`
  with auto-reconnect; events: `NewBlockReceived`, `TxExecuted`, `VoteReceived`,
  `ValidatorSetUpdated`, `NewBlockHeaderReceived`
- `CometBFT.Client.Grpc` — gRPC client with `PingAsync` and `BroadcastTxAsync`
  (`GrpcProtocol.TendermintLegacy`); `CometBftSdkGrpcClient` targeting Cosmos SDK
  `cosmos.tx.v1beta1` gRPC surface (`GrpcProtocol.CosmosSdk`)
- `CometBFT.Client.Extensions` — `IServiceCollection` extension methods
  (`AddCometBftRest`, `AddCometBftWebSocket`, `AddCometBftGrpc`, `AddCometBftSdkGrpc`)
- Unit tests with WireMock.Net for REST, NSubstitute for gRPC and WebSocket
- Integration tests and E2E tests against real Cosmos Hub public endpoints
  (skip-on-missing-env-var pattern); global line coverage ≥ 90 %
- GitHub Actions CI (build + format check + unit/integration/E2E + coverage gate) and publish workflows
- Git flow configuration, commit-msg hook, branch protection documentation
- pre-commit configuration with `dotnet format` and `detect-secrets`

### Changed
- All public `Tendermint`-prefixed identifiers renamed to `CometBft` prefix
  (`ITendermintRestClient` → `ICometBftRestClient`, `TendermintGrpcClient` → `CometBftGrpcClient`, etc.)
- `WebSocketMessageParser` extracted from `CometBftWebSocketClient` into a dedicated internal class
- SOLID/clean-code remediation applied across all source projects: single-responsibility split,
  dependency inversion via internal interfaces, dead-code removal, naming consistency
