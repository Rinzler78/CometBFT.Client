# Extensibility v2 — Proposal

## Problem

All domain types in CometBFT.Client are `sealed record` and service interfaces return
concrete non-parameterised types. A consumer such as Cosmos.Client that needs to enrich
`Block<TTx>` with `AppHash` or `ChainId` is forced to completely redefine the type and
all associated service methods — redefining instead of extending.

## Goal

Design CometBFT.Client so that every consumer can **extend without redefining**:

```csharp
// Cosmos layer — no redefinition of consensus fields
public record CosmosBlock<TTx>(..., string AppHash) : Block<TTx>(...);

// Cosmos service — inherits all CometBFT methods, adds its own
public interface ICosmosRestClient<TBlock, TTxResult, TVal>
    : ICometBftRestClient<TBlock, TTxResult, TVal> { }
```

## Decisions

| Decision | Rationale |
|----------|-----------|
| `abstract record BlockBase(Height, Hash, Time, Proposer)` | Eliminates 4-property duplication between `Block` and `Block<TTx>`; becomes the extension base |
| `abstract record TxResultBase(Hash, Height, Index, Code, ...)` | Eliminates 11-property duplication between `TxResult` and `TxResult<TTx>` |
| Remove `sealed` from applicative types | `Validator`, `BroadcastTxResult`, `BlockHeader`, etc. must be extendable by consumers |
| `IBlockService<TBlock> where TBlock : BlockBase` | Without this, `ICosmosBlockService` must redefine all methods |
| `ICometBftWebSocketClient<TTx, TBlock, TTxResult, TValidator>` 4 params | Without `TBlock`, a Cosmos WebSocket client emits `Block<CosmosTx>` not `CosmosBlock<CosmosTx>` — consumer forced to cast |
| Non-generic shims | `IBlockService : IBlockService<Block>` — preserves existing usage without behavioural breaking change |
| Generic DI overloads | `AddCometBftRest<TInterface, TClient>()` — Cosmos avoids rewriting the full Polly pipeline |

## Breaking Changes

- `Block` and `Block<TTx>` are no longer `sealed`; they now inherit from `abstract record BlockBase`
- `TxResult` and `TxResult<TTx>` are no longer `sealed`; they now inherit from `abstract record TxResultBase`
- `IBlockService` is now generic (`IBlockService<TBlock>`); non-generic shim preserved
- `ITxService` is now generic (`ITxService<TTxResult>`); non-generic shim preserved
- `IValidatorService` is now generic (`IValidatorService<TValidator>`); non-generic shim preserved
- `ICometBftRestClient` is now generic (3 type params); non-generic shim preserved
- `ICometBftWebSocketClient<TTx>` events now flow through a 4-parameter base interface; shims preserve existing usage
- Removed `sealed` from: `Validator`, `BroadcastTxResult`, `BlockHeader`, `ConsensusParamsInfo`, `NodeInfo`, `SyncInfo`, `UnconfirmedTxsInfo`, `BlockchainInfo`

## Consumer Validation

The design was validated by extrapolating a Cosmos.Client layer and an Osmosis.Client
layer. In both cases, consumers extend CometBFT types without redefining:

```csharp
// Cosmos.Client
record CosmosBlock<TTx>(..., string AppHash) : Block<TTx>(...);
record CosmosTxResult<TTx>(..., string RawLog) : TxResult<TTx>(...);
record CosmosValidator(..., string Moniker) : Validator(...);

interface ICosmosRestClient<TBlock, TTxResult, TVal>
    : ICometBftRestClient<TBlock, TTxResult, TVal>
    where TVal : CosmosValidator { }

// Osmosis.Client (extends Cosmos without redefining)
record OsmosisValidator(..., IReadOnlyList<SuperfluidDelegation>? Delegations)
    : CosmosValidator(...);

interface IOsmosisRestClient<TBlock, TTxResult, TVal>
    : ICosmosRestClient<TBlock, TTxResult, TVal>
    where TVal : OsmosisValidator { }
```

## Version

This is a breaking change → **v2.0.0**.
