# Extensibility v2 — Tasks

## Phase 0 — Branch and OpenSpec
- [x] Create `release/v2.0.0` from `develop`
- [x] Create `openspec/changes/extensibility-v2/proposal.md`
- [x] Create `openspec/changes/extensibility-v2/tasks.md`
- [x] Create `openspec/changes/extensibility-v2/specs/spec-diff.md`

## Phase 1 — Abstract bases: BlockBase and TxResultBase
- [x] `Domain/Block.cs` — add `abstract record BlockBase`, `Block` inherits, remove `sealed`
- [x] `Domain/Block.Generic.cs` — `Block<TTx>` inherits `BlockBase`, remove `sealed`
- [x] `Domain/TxResult.cs` — add `abstract record TxResultBase`, `TxResult` inherits, remove `sealed`
- [x] `Domain/TxResult.Generic.cs` — `TxResult<TTx>` inherits `TxResultBase`, remove `sealed`

## Phase 2 — Remove sealed from applicative types
- [x] `Domain/Validator.cs`
- [x] `Domain/BroadcastTxResult.cs`
- [x] `Domain/BlockHeader.cs`
- [x] `Domain/ConsensusParamsInfo.cs`
- [x] `Domain/NodeInfo.cs` (NodeInfo only; ProtocolVersion stays sealed)
- [x] `Domain/SyncInfo.cs`
- [x] `Domain/UnconfirmedTxsInfo.cs`
- [x] `Domain/BlockchainInfo.cs`

## Phase 3 — Generic service interfaces + shims
- [x] `Interfaces/IBlockService.cs` — `IBlockService<TBlock>` + shim
- [x] `Interfaces/ITxService.cs` — `ITxService<TTxResult>` + shim
- [x] `Interfaces/IValidatorService.cs` — `IValidatorService<TValidator>` + shim
- [x] `Interfaces/ICometBftRestClient.cs` — 3-param generic + shim
- [x] `Interfaces/ICometBftWebSocketClient.cs` — 4-param generic + 2 shims

## Phase 4 — Verify compilation
- [x] `dotnet build` on all projects — 0 errors, 0 warnings

## Phase 5 — DI Extensions generic overloads
- [x] `Extensions/ServiceCollectionExtensions.cs` — `AddCometBftRest<TInterface,TClient>`, `AddCometBftWebSocket<TTx,TInterface,TClient>`

## Phase 6 — Tests
- [x] `Core.Tests/Domain/BlockTests.cs` — inheritance assertions
- [x] `Core.Tests/Domain/BlockGenericTests.cs` — inheritance assertions
- [x] `Core.Tests/Domain/TxResultTests.cs` — inheritance assertions
- [x] `Core.Tests/Domain/TxResultGenericTests.cs` — inheritance assertions
- [x] `Rest.Tests/ServiceCollectionExtensionsTests.cs` — generic DI overload tests
- [x] `dotnet test` — 645 tests, 0 failures

## Phase 7-8 — Samples, docs, CHANGELOG
- [x] `samples/CometBFT.Client.Sample/Program.cs` — extension guide comment
- [x] `CHANGELOG.md` — v2.0.0 section
- [x] `openspec/changes/extensibility-v2/specs/spec-diff.md` — design rules delta
- [x] `src/CometBFT.Client.Core/README.md` — Extension Guide section + updated Contents
- [x] `openspec/changes/initial-library-creation/specs/cometbft-client/spec.md` — Domain types + DI registration sections updated to v2

## Phase 9 — Release
- [x] PR `release/v2.0.0` → `develop`
- [x] Merge `develop` → `master`
- [x] Tag `v2.0.0` — NuGet published, GitHub Release created
