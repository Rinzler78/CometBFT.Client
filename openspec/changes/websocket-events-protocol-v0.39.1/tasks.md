# WebSocket Events Completeness — Tasks

## Phase 0 — Branch and OpenSpec

### 0.1 Solution structure (verify or create)
- [x] 0.1.1 Verify `CometBFT.Client.sln` exists and contains **all** projects (src + tests + samples) — create if absent
- [x] 0.1.2 Verify `CometBFT.Client.src.slnf` exists as a solution filter including **only** source projects (`src/**`) — create if absent
- [x] 0.1.3 Verify `CometBFT.Client.src-tests.slnf` exists as a solution filter including source projects (`src/**`) and test projects (`tests/**`) — excludes samples — create if absent

### 0.2 Branch and OpenSpec files
- [x] Create `release/v2.1.0` from `develop`
- [x] `openspec/changes/websocket-events-protocol-v0.39.1/proposal.md` — exists
- [x] `openspec/changes/websocket-events-protocol-v0.39.1/tasks.md` — exists
- [x] `openspec/changes/websocket-events-protocol-v0.39.1/specs/spec-diff.md` — exists

## Phase 1 — Protocol Version Bump
- [x] `Directory.Build.props` — updated `<ProtocolVersion>` from `v0.38.9` to `v0.39.1`
- [x] `CHANGELOG.md` — added `v2.1.0` section

## Phase 2 — New Domain Types
- [x] `Domain/NewBlockEventsData.cs` — `sealed record NewBlockEventsData(BlockHeader Header, long Height, IReadOnlyList<CometBftEvent> Events)`
- [x] `Domain/CompleteProposalData.cs` — `sealed record CompleteProposalData(long Height, int Round, string BlockId)`
- [x] `Domain/ValidatorSetUpdatesData.cs` — `sealed record ValidatorSetUpdatesData(IReadOnlyList<Validator> ValidatorUpdates)`
- [x] `Domain/NewEvidenceData.cs` — `sealed record NewEvidenceData(long Height, string EvidenceType, string Validator)`
- [x] No `[JsonSerializable]` changes needed — domain types are output-only, not deserialized directly

## Phase 3 — Interface Updates
- [x] `Interfaces/ICometBftWebSocketClient.cs` — added 5 `IObservable<T>` streams + 4 Subscribe methods
- [x] Shim `ICometBftWebSocketClient<TTx>` inherits automatically — verified
- [x] Shim `ICometBftWebSocketClient` inherits automatically — verified

## Phase 4 — Implementation
- [x] `WebSocket/Json/WsWireTypes.cs` — added 13 new `[JsonDerivedType]` entries + wire classes
- [x] `WebSocket/Internal/WebSocketMessageParser.cs` — added 4 parser methods
- [x] `WebSocket/CometBftWebSocketClient.cs` — 5 Subject<T> backing fields, 5 IObservable properties, 4 Subscribe methods + SubscribeConsensusInternalAsync, new switch cases, DisposeAsync cleanup
- [x] `CometBFT.Client.WebSocket.csproj` — added `System.Reactive 6.1.0`

## Phase 5 — Build Verification
- [x] `dotnet build CometBFT.Client.sln` — 0 errors, 0 warnings

## Phase 6 — Tests
- [x] `Core.Tests/Domain/NewBlockEventsDataTests.cs`
- [x] `Core.Tests/Domain/CompleteProposalDataTests.cs`
- [x] `Core.Tests/Domain/ValidatorSetUpdatesDataTests.cs`
- [x] `Core.Tests/Domain/NewEvidenceDataTests.cs`
- [x] `WebSocket.Tests/NewBlockEventsParserTests.cs`
- [x] `WebSocket.Tests/CompleteProposalParserTests.cs`
- [x] `WebSocket.Tests/ValidatorSetUpdatesDataParserTests.cs`
- [x] `WebSocket.Tests/NewEvidenceParserTests.cs`
- [x] `WebSocket.Tests/NewStreamDispatchTests.cs` — dispatch + ConsensusInternalStream (9 Theory cases)
- [x] `WebSocket.Tests/CometBftWebSocketClientConnectedTests.cs` — 4 new Subscribe ACK tests
- [x] `dotnet test` — 712 tests passed, 0 failed

## Phase 7 — Docs and Samples
- [x] `src/CometBFT.Client.WebSocket/README.md` — updated subscriptions table + Observable Streams section
- [x] `src/CometBFT.Client.Core/README.md` — updated version + types + interface description
- [x] `README.md` (root) — updated v0.38.9 → v0.39.1
- [x] `samples/CometBFT.Client.Sample/README.md` — added NewBlockEvents pattern
- [x] `samples/CometBFT.Client.Sample/Program.cs` — added NewBlockEventsStream subscription + comment guide
- [x] `samples/CometBFT.Client.Demo.Dashboard/Services/DashboardBackgroundService.cs` — added NewBlockEvents subscription

## Phase 8 — Release
- [ ] PR `release/v2.1.0` → `develop`
- [ ] Merge `develop` → `master`
- [ ] Tag `v2.1.0`
