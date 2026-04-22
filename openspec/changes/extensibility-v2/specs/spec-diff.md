# Spec Diff — extensibility-v2

Changes to design rules and public API surface introduced by v2.0.0.

---

## 1. Domain type rules

### Before

> All domain types are `sealed record`. Consumers cannot extend them.

### After

**Abstract base pairs** — when a raw type and its generic counterpart share a common
property set, a shared abstract base is introduced:

| Abstract base | Concrete types |
|--------------|----------------|
| `BlockBase(Height, Hash, Time, Proposer)` | `Block`, `Block<TTx>` |
| `TxResultBase(Hash, Height, Index, Code, Data, Log, Info, GasWanted, GasUsed, Events, Codespace)` | `TxResult`, `TxResult<TTx>` |

**Applicative types** — types that application layers (Cosmos, Osmosis, …) may need to enrich
are **non-sealed**:

`Validator`, `BroadcastTxResult`, `BlockHeader`, `ConsensusParamsInfo`, `NodeInfo`,
`SyncInfo`, `UnconfirmedTxsInfo`, `BlockchainInfo`, `Block`, `Block<TTx>`, `TxResult`, `TxResult<TTx>`

**Protocol-pure types** — remain `sealed` (never enriched by consumers):

`Vote`, `AbciEventEntry`, `CometBftEvent`, `AbciQueryResponse`, `AbciProofOps`, `AbciProofOp`,
`GenesisChunk`, `ProtocolVersion`, `NetworkInfo`, `NetworkPeer`, `RawTxCodec`

---

## 2. Service interface rules

### Before

> Service interfaces return concrete types directly (`Block`, `TxResult`, `Validator`).
> No generic parameter. Consumers must redefine the full interface surface.

### After

Every service interface that returns an applicative type exposes a **generic type parameter**
with a base-type constraint. A non-generic **shim** preserves the existing usage:

| Generic interface | Constraint | Shim |
|------------------|------------|------|
| `IBlockService<TBlock>` | `where TBlock : BlockBase` | `IBlockService : IBlockService<Block>` |
| `ITxService<TTxResult>` | `where TTxResult : TxResultBase` | `ITxService : ITxService<TxResult>` |
| `IValidatorService<TValidator>` | `where TValidator : Validator` | `IValidatorService : IValidatorService<Validator>` |
| `ICometBftRestClient<TBlock, TTxResult, TValidator>` | all three constraints | `ICometBftRestClient : ICometBftRestClient<Block, TxResult, Validator>` |
| `ICometBftWebSocketClient<TTx, TBlock, TTxResult, TValidator>` | `TBlock : Block<TTx>`, `TTxResult : TxResult<TTx>`, `TValidator : Validator` | `ICometBftWebSocketClient<TTx>`, `ICometBftWebSocketClient` |

**Note on WebSocket constraints:** `TBlock : Block<TTx>` (not `BlockBase`) because the WebSocket
always emits decoded blocks; consumers must be able to access `Txs : IReadOnlyList<TTx>` without
a cast. Similarly `TTxResult : TxResult<TTx>` to guarantee `Transaction : TTx`.

**`GetBlockResultsAsync` stays raw:** returns `IReadOnlyList<TxResult>` regardless of `TBlock`,
to avoid a cross-service type dependency between `IBlockService` and `ITxService`.

---

## 3. DI extension rules

### Before

> `AddCometBftRest()` and `AddCometBftWebSocket<TTx>()` hardcode `CometBftRestClient` and
> `CometBftWebSocketClient<TTx>`. Consumers must duplicate the Polly pipeline to register
> a derived client.

### After

Generic overloads accept any `TInterface` / `TClient` pair:

```csharp
AddCometBftRest<TInterface, TClient>(configure)
    where TInterface : class, ICometBftRestClient
    where TClient    : class, TInterface

AddCometBftWebSocket<TTx, TInterface, TClient>(configure, codec)
    where TTx        : notnull
    where TInterface : class, ICometBftWebSocketClient<TTx>
    where TClient    : class, TInterface
```

Non-generic overloads delegate to the generic ones — no behavioral change.

---

## 4. Extension pattern (consumer reference)

```csharp
// 1. Extend a domain type
record CosmosBlock<TTx>(
    long Height, string Hash, DateTimeOffset Time, string Proposer,
    IReadOnlyList<TTx> Txs,
    string AppHash, string ChainId)
    : Block<TTx>(Height, Hash, Time, Proposer, Txs)
    where TTx : notnull;

// 2. Extend the REST interface — no method redefinition needed
interface ICosmosRestClient
    : ICometBftRestClient<CosmosBlock<string>, TxResult, Validator> { }

// 3. Register with the existing Polly pipeline
services.AddCometBftRest<ICosmosRestClient, CosmosRestClient>(o => { ... });

// 4. Extend the WebSocket interface
interface ICosmosWebSocketClient
    : ICometBftWebSocketClient<CosmosTx, CosmosBlock<CosmosTx>, CosmosTxResult, CosmosValidator> { }

services.AddCometBftWebSocket<CosmosTx, ICosmosWebSocketClient, CosmosWebSocketClient>(o => { ... }, codec);
```
