# Change: Initial Creation — CometBFT.Client

## Why

There is no standalone, production-ready .NET client library for CometBFT/Tendermint. The client code embedded in OsmoBot-CSharp lacks dependency injection, uniform async patterns, Polly resilience, XML documentation, and test coverage. Extracting it as an independent NuGet package (`Rinzler78.CometBFT.Client`) makes it reusable, independently versioned, and aligned with modern .NET practices.

## Status

As of 2026-04-13, this change is **complete**. All `tasks.md` items are checked.
The repository is fully validated end-to-end with real CometBFT endpoints.

Subsequent improvements delivered on `develop` after the initial completion:

- **WebSocketMessageParser extraction** (`8826ee4`): parsing logic decoupled from `CometBftWebSocketClient` into a dedicated `WebSocketMessageParser` with unit tests.
- **SOLID/clean-code remediation phases 1–9** (`319d110`): single-responsibility split, dependency inversion via internal interfaces, dead-code removal, and naming consistency across all source projects.
- **Cosmos SDK gRPC client** (`344c572`): added `ICometBftSdkGrpcClient` / `CometBftSdkGrpcClient` targeting the Cosmos SDK `cosmos.tx.v1beta1` gRPC surface; demo-grpc extended with full block-polling and Cosmos TX panels; `AddCometBftSdkGrpc` DI extension added.
- **Coverage at 97 %** (`0769c8e`): URL scheme fix, clean shutdown, and test suite expansion raised global line coverage from 90 % to 97 %.

## Reality Gaps Closed During Reconciliation

- The original change text lagged behind the repository: scaffold, source projects, baseline scripts, CI files, and a large part of the implementation already existed and had to be reclassified from planned work to verified existing work.
- The effective acceptance target for coverage was stronger than the original wording suggested. The repository now enforces `>= 90 %` global line coverage and `>= 90 %` per source file line coverage through `scripts/test.sh`, `.git/hooks/pre-push`, and CI.
- The REST client did not yet cover the full public CometBFT RPC surface required by `/rpc/openapi/openapi.yaml`. The missing endpoint groups were implemented and covered by tests.
- The original change understated the E2E and CI delta. A dedicated E2E test project and separate CI jobs for integration and E2E live tests were added.
- The demos and the spec were out of sync. The REST demo now includes block results, the WebSocket demo exposes `NewBlockHeader` and `ValidatorSetUpdates`, and the gRPC demo exposes the effective polling/live state more explicitly.
- The initial default endpoint assumptions were not reliable. Theta testnet endpoints were unreachable from the validation environment, `cosmos-rpc.polkachu.com` worked for RPC but rejected WebSocket upgrades with HTTP `400`, and the default endpoint set had to be rebuilt from live validation.
- The final default transport set was aligned on validated Cosmos Hub public endpoints, with `Lava` retained for the default RPC/WebSocket/gRPC path because the endpoints were listed in the ecosystem registry and validated successfully in real checks.
- The publish flow was tightened to read `NUGET_API_KEY` from the environment in both local and Docker paths instead of relying on command-line-only secret passing.

## What Changes

- **Scaffold**: New repo `CometBFT.Client`, solution, `Directory.Build.props`, `.editorconfig`, `global.json`
- **Git Flow**: `.gitflow` config, pre-commit hooks (format + secret detection + conventional commits), pre-push branch protection
- **Scripts**: `build.sh`, `test.sh` (coverage gate ≥ 90 % global line + ≥ 90 % per source file line), `publish.sh` (pack + NuGet push)
- **Domain Core** (`CometBFT.Client.Core`): Immutable `record` types — `Block`, `BlockHeader`, `TxResult`, `Event`, `Attribute`, `NodeInfo`, `SyncInfo`, `Validator`; segregated interfaces per service
- **REST client** (`CometBFT.Client.Rest`): All public CometBFT RPC HTTP endpoints; `HttpClient` + Polly retry + circuit breaker; `CometBftRestOptions`; `CometBftRestException`
- **WebSocket client** (`CometBFT.Client.WebSocket`): All event types (NewBlock, NewBlockHeader, Tx, Vote, ValidatorSetUpdates); `ICometBftWebSocketClient`; `CometBftWebSocketOptions`; `CometBftWebSocketException`
- **gRPC client** (`CometBFT.Client.Grpc`): Proto from CometBFT `v0.38.9`; `ICometBftGrpcClient` (`PingAsync`, `BroadcastTxAsync`); `ICometBftSdkGrpcClient` targeting Cosmos SDK `cosmos.tx.v1beta1`; `CometBftGrpcOptions`; `CometBftGrpcException`
- **DI Extensions** (`CometBFT.Client.Extensions`): `AddCometBftRest()`, `AddCometBftWebSocket()`, `AddCometBftGrpc()`, `AddCometBftSdkGrpc()` on `IServiceCollection`
- **Tests ≥ 90 %**: Unit tests with WireMock.Net (REST), NSubstitute (gRPC/WebSocket); integration tests and E2E tests against real public CometBFT endpoints, with CI pinned to a validated Cosmos Hub endpoint set; effective coverage 97 %
- **Documentation**: XML doc on all public members (`TreatWarningsAsErrors`), README with badges + quickstart, CHANGELOG tracking protocol version, DocFX API reference, samples per transport
- **CI/CD**: `ci.yml` (build + lint + unit/integration/E2E + coverage gate), `publish.yml` (pack + push on release tag)

## Rename: Tendermint → CometBFT

Completed as part of this change before publication. All `Tendermint`-prefixed public
identifiers were renamed to `CometBft` (PascalCase). The protocol wire name
`tendermint.rpc.grpc` and the `GrpcProtocol.TendermintLegacy` enum value are intentionally
preserved for backward-compatibility.

| Element | Before | After |
|---------|--------|-------|
| Public interfaces | `ITendermintRestClient`, `ITendermintWebSocketClient`, `ITendermintGrpcClient` | `ICometBftRestClient`, `ICometBftWebSocketClient`, `ICometBftGrpcClient` |
| Client classes | `TendermintRestClient`, `TendermintWebSocketClient`, `TendermintGrpcClient` | `CometBftRestClient`, `CometBftWebSocketClient`, `CometBftGrpcClient` |
| Options | `TendermintRest/WebSocket/GrpcOptions` | `CometBftRest/WebSocket/GrpcOptions` |
| Exceptions | `TendermintClientException` + sub-types | `CometBftClientException` + sub-types |
| JSON context | `TendermintJsonContext` | `CometBftJsonContext` |
| DI extensions | `AddTendermintRest/WebSocket/Grpc` | `AddCometBftRest/WebSocket/Grpc/SdkGrpc` |

Unchanged (protocol wire): `GrpcProtocol.TendermintLegacy`, namespace `CometBFT.Client.Grpc.LegacyProto`, proto package `tendermint.rpc.grpc`.

## Impact

- **New repo**: `github.com/Rinzler78/CometBFT.Client`
- **New package family**: `CometBFT.Client.Core`, `CometBFT.Client.Rest`, `CometBFT.Client.WebSocket`, `CometBFT.Client.Grpc`, `CometBFT.Client.Extensions` (published independently; README may additionally document an umbrella package strategy later)
- **Protocol source**: https://github.com/cometbft/cometbft — latest stable release; version tracked in `<ProtocolVersion>` in `Directory.Build.props` and in `CHANGELOG.md`
- **Downstream**: `Rinzler78.Cosmos.Client` will depend on this package; OsmoBot-CSharp will replace project references with this NuGet package

## Dependencies

- No dependency on `Rinzler78.NetExtension.Standard`; any stale reference to this package must be removed from docs, specs, tasks, and project files
- No other OsmoBot-CSharp package dependency (Tendermint is the base layer)

## Risks & Mitigation

- **Risk**: gRPC services not yet stable in CometBFT public releases
  - **Mitigation**: Ship `CometBFT.Client.Grpc` as preview assembly; REST and WebSocket are stable and shipped independently
- **Risk**: Public endpoint behavior diverges from the initial spec assumptions
  - **Mitigation**: Effective defaults were selected only after live RPC, WebSocket handshake, and gRPC validation, and the OpenSpec now records the resulting deltas explicitly

## Exit Criteria Before Archive

- `tasks.md` has no remaining unchecked implementation items except explicitly manual GitHub configuration steps and intentionally tracked future follow-up expectations
- `scripts/test.sh` enforces `>= 90 %` global line coverage and `>= 90 %` per source file line coverage
- `.git/hooks/pre-push` and `.github/workflows/ci.yml` invoke the same coverage gate
- Missing transport coverage, tests, and demo gaps called out in `tasks.md` are implemented or intentionally descoped in a follow-up change
