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
    tx search, tx by hash, broadcast async/sync/commit, abci info, abci query
- `CometBFT.Client.WebSocket` — real-time event subscription client using `Websocket.Client`
  with auto-reconnect; events: `NewBlockReceived`, `TxExecuted`, `VoteReceived`, `ValidatorSetUpdated`
- `CometBFT.Client.Grpc` — gRPC channel stub with `PingAsync` and `BroadcastTxAsync`
- `CometBFT.Client.Extensions` — `IServiceCollection` extension methods
  (`AddTendermintRest`, `AddTendermintWebSocket`, `AddTendermintGrpc`)
- Unit tests with WireMock.Net for REST, NSubstitute for gRPC and WebSocket
- Integration tests with skip-on-missing-env-var pattern
- GitHub Actions CI (build + format check + tests) and publish workflows
- Git flow configuration, commit-msg hook, branch protection documentation
- pre-commit configuration with `dotnet format` and `detect-secrets`
