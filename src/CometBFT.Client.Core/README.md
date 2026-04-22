# CometBFT.Client.Core

Core domain types, interfaces, and exceptions for the CometBFT.Client library suite.
Targets [CometBFT](https://github.com/cometbft/cometbft) protocol version **v0.38.9**.

## Contents

- **Abstract bases** — `BlockBase`, `TxResultBase` — shared bases enabling consumer inheritance without property redefinition
- **Domain types** — immutable `record` types shared across all transports:
  - *Applicative (non-sealed)* — `Block`, `Block<TTx>`, `TxResult`, `TxResult<TTx>`, `BlockHeader`, `Validator`, `BroadcastTxResult`, `NodeInfo`, `SyncInfo`, `ConsensusParamsInfo`, `UnconfirmedTxsInfo`, `BlockchainInfo`
  - *Protocol-pure (sealed)* — `Vote`, `CometBftEvent`, `AbciEventEntry`, `AbciQueryResponse`, `AbciProofOps`, `AbciProofOp`, `GenesisChunk`, `ProtocolVersion`, `NetworkInfo`, `NetworkPeer`, `RawTxCodec`
- **Transport interfaces** — `ICometBftRestClient<TBlock,TTxResult,TValidator>`, `ICometBftWebSocketClient<TTx,TBlock,TTxResult,TValidator>`, `ICometBftGrpcClient`, `IUnsafeService`; non-generic shims preserve existing usage
- **Service interfaces** — `IBlockService<TBlock>`, `ITxService<TTxResult>`, `IValidatorService<TValidator>`, `IHealthService`, `IStatusService`, `IAbciService`; non-generic shims preserve existing usage
- **Options** — `CometBftRestOptions`, `CometBftWebSocketOptions`, `CometBftGrpcOptions`
- **Exceptions** — `CometBftClientException`, `CometBftRestException`, `CometBftWebSocketException`, `CometBftGrpcException`

## Usage

This package is a dependency of the transport packages. Consume it directly only if you need to reference domain types or interfaces without a specific transport.

```csharp
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Interfaces;
```

## Extension Guide (v2.0.0)

Consumers can extend domain types and service interfaces without redefining any existing property or method.

```csharp
// 1. Extend a domain type
record CosmosBlock<TTx>(
    long Height, string Hash, DateTimeOffset Time, string Proposer,
    IReadOnlyList<TTx> Txs,
    string AppHash,
    string ChainId)
    : Block<TTx>(Height, Hash, Time, Proposer, Txs)
    where TTx : notnull;

// 2. Extend the REST client interface — no method redefinition needed
interface ICosmosRestClient
    : ICometBftRestClient<CosmosBlock<string>, TxResult, Validator> { }

// 3. Register with the same Polly pipeline
services.AddCometBftRest<ICosmosRestClient, CosmosRestClient>(o => { ... });

// 4. Extend the WebSocket client interface
interface ICosmosWebSocketClient
    : ICometBftWebSocketClient<CosmosTx, CosmosBlock<CosmosTx>, CosmosTxResult, CosmosValidator> { }

services.AddCometBftWebSocket<CosmosTx, ICosmosWebSocketClient, CosmosWebSocketClient>(o => { ... }, codec);
```

## Related packages

| Package | Transport |
|---------|-----------|
| `CometBFT.Client.Rest` | REST / JSON-RPC 2.0 |
| `CometBFT.Client.WebSocket` | WebSocket subscriptions |
| `CometBFT.Client.Grpc` | gRPC BroadcastAPI |
| `CometBFT.Client.Extensions` | Dependency injection |
