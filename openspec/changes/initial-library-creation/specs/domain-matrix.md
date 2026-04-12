# Domain Matrix — CometBFT.Client

**Audit date**: 2026-04-12  
**Protocol target**: CometBFT v0.38.9

This document maps each business concept to its shared `Core.Domain` type and
shows which transport(s) consume it. It also documents any remaining transport-specific
types and justifies why they remain private.

---

## Verdict

**No domain duplication detected.** All shared business concepts are represented by a
single sealed record in `src/CometBFT.Client.Core/Domain/` and consumed by all
transports through the shared interface contracts. Transport-specific internal models
are justified and documented below.

---

## Concept → Core type → Transport matrix

| Business concept | Core type | REST | WebSocket | gRPC |
|-----------------|-----------|------|-----------|------|
| Block | `Block` | `IBlockService.GetBlockAsync`, `SearchBlocksAsync` | `NewBlockReceived` event | — |
| Block header | `BlockHeader` | `IBlockService.GetHeaderAsync`, `GetHeaderByHashAsync` | `NewBlockHeaderReceived` event | — |
| Blockchain range | `BlockchainInfo` | `IBlockService.GetBlockchainAsync` | — | — |
| Transaction result | `TxResult` | `ITxService.GetTxAsync`, `SearchTxAsync`, `IBlockService.GetBlockResultsAsync` | `TxExecuted` event | — |
| Transaction event | `TendermintEvent` + `AbciEventEntry` | inside `TxResult.Events` | inside `TxResult.Events` | — |
| Broadcast result | `BroadcastTxResult` | `ITxService.BroadcastTxAsync`, `BroadcastTxSyncAsync`, `BroadcastTxCommitAsync`, `CheckTxAsync` | — | `ITendermintGrpcClient.BroadcastTxAsync` |
| Validator | `Validator` | `IValidatorService.GetValidatorsAsync` | `ValidatorSetUpdated` event (`IReadOnlyList<Validator>`) | — |
| Vote | `Vote` | — | `VoteReceived` event | — |
| Node info | `NodeInfo` | `IStatusService.GetStatusAsync` | — | — |
| Sync info | `SyncInfo` | `IStatusService.GetStatusAsync` | — | — |
| Network info | `NetworkInfo` + `NetworkPeer` | `IStatusService.GetNetInfoAsync` | — | — |
| Consensus params | `ConsensusParamsInfo` | `IStatusService.GetConsensusParamsAsync` | — | — |
| Genesis chunk | `GenesisChunk` | `IStatusService.GetGenesisChunkAsync` | — | — |
| Unconfirmed txs | `UnconfirmedTxsInfo` | `IStatusService.GetUnconfirmedTxsAsync`, `GetNumUnconfirmedTxsAsync` | — | — |

---

## Transport-specific internal types (justified, not duplicated)

### REST — `src/CometBFT.Client.Rest/Json/RpcModels.cs`

These types are `internal sealed` and serve only as JSON deserialization targets
for the CometBFT JSON-RPC 2.0 wire format. They are never surfaced in public APIs.

| Internal type | Purpose | Maps to |
|---------------|---------|---------|
| `RpcBroadcastResult` | Deserializes `broadcast_tx_*` response | `BroadcastTxResult` via `MapBroadcastResult` |
| `RpcTxResult` | Deserializes `tx_result` sub-object | `TxResult` via `MapTxResult` |
| `RpcEvent` / `RpcAttribute` | Deserializes ABCI events | `TendermintEvent` / `AbciEventEntry` |
| `RpcValidator` | Deserializes validator entry | `Validator` via `MapValidator` |
| `RpcValidatorsResult` | Wraps validators list | consumed inline |
| `RpcTxSearchResult` | Wraps tx search response | consumed inline |
| `RpcBlockResultsResult` | Wraps block results | consumed inline |
| `RpcAbciInfoResult` / `RpcAbciResponse` | Deserializes abci_info | returned as `IReadOnlyDictionary<string,string>` |
| `RpcStatusResult` / `RpcSyncInfo` / `RpcNodeInfo` / `RpcProtocolVersion` | Deserializes status | `NodeInfo` + `SyncInfo` |
| `RpcBlockResult` / `RpcBlockHeader` / `RpcBlockId` | Deserializes block | `Block` + `BlockHeader` |
| `RpcConsensusParamsResult` + inner types | Deserializes consensus_params | `ConsensusParamsInfo` |
| `RpcNetInfoResult` | Deserializes net_info | `NetworkInfo` + `NetworkPeer` |
| `RpcUnconfirmedTxsResult` | Deserializes unconfirmed_txs | `UnconfirmedTxsInfo` |

**Verdict**: All internal REST models are justified. They are isolated from the public
API surface and do not leak domain duplication.

### gRPC — proto-generated types (`CometBFT.Client.Grpc.Proto` namespace)

The proto-generated types (`RequestPing`, `ResponsePing`, `RequestBroadcastTx`,
`ResponseBroadcastTx`, `ResponseCheckTx`) are auto-generated from
`grpc.proto` and are `internal` to the gRPC project via `GrpcChannelBroadcastApiClient`.
They are never exposed in public APIs.

| Proto type | Maps to |
|-----------|---------|
| `ResponseBroadcastTx.check_tx` (`ResponseCheckTx`) | `BroadcastTxResult` (all fields including `GasWanted`, `GasUsed`) |

**Verdict**: No domain duplication. The full `ResponseCheckTx` shape is now correctly
mapped to `BroadcastTxResult` (Code, Data, Log, Codespace, GasWanted, GasUsed, Hash).

---

## Interface alignment — cross-transport shared concepts

### Block / BlockHeader

- REST: `GetBlockAsync`, `GetHeaderAsync` → returns `Block` / `BlockHeader` ✓
- WebSocket: `NewBlockReceived` → `TendermintEventArgs<Block>` ✓
- WebSocket: `NewBlockHeaderReceived` → `TendermintEventArgs<BlockHeader>` ✓
- gRPC: not applicable (v0.38.9 gRPC surface is BroadcastAPI only) ✓

### TxResult

- REST: `GetTxAsync`, `SearchTxAsync`, `GetBlockResultsAsync` → `TxResult` ✓
- WebSocket: `TxExecuted` → `TendermintEventArgs<TxResult>` ✓
- gRPC: not applicable ✓

### BroadcastTxResult

- REST: `BroadcastTx*Async`, `CheckTxAsync` → `BroadcastTxResult` (Code, Data, Log, Codespace, Hash) ✓
- gRPC: `BroadcastTxAsync` → `BroadcastTxResult` (Code, Data, Log, Codespace, Hash, GasWanted, GasUsed) ✓

**Note**: `GasWanted` and `GasUsed` default to `0` for REST paths (the `broadcast_tx_*`
JSON-RPC response does not include gas fields). The gRPC `check_tx` response populates
all fields. This is the only expected asymmetry and is documented here.

### Validator

- REST: `GetValidatorsAsync` → `IReadOnlyList<Validator>` ✓
- WebSocket: `ValidatorSetUpdated` → `TendermintEventArgs<IReadOnlyList<Validator>>` ✓

---

## Remaining gaps for future phases

- `IUnsafeService` (`dial_seeds`, `dial_peers`) — to be added in Phase B.  
  No new domain types required; these endpoints return no structured payload.
- Explicit `CancellationToken` audit: all public interface methods already carry
  `CancellationToken` parameters. No gap found.
