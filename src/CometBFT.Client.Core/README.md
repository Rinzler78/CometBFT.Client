# CometBFT.Client.Core

Core domain types, interfaces, and exceptions for the CometBFT.Client library suite.
Targets [CometBFT](https://github.com/cometbft/cometbft) protocol version **v0.38.9**.

## Contents

- **Domain types** — immutable `sealed record` types shared across all transports:
  `Block`, `BlockHeader`, `TxResult`, `Vote`, `NodeInfo`, `SyncInfo`, `Validator`, `BroadcastTxResult`, `Event`, `Attribute`
- **Transport interfaces** — `ICometBftRestClient`, `ICometBftWebSocketClient`, `ICometBftGrpcClient`, `IUnsafeService`
- **Service interfaces** — `IHealthService`, `IStatusService`, `IBlockService`, `ITxService`, `IValidatorService`, `IAbciService`
- **Options** — `TendermintRestOptions`, `TendermintWebSocketOptions`, `TendermintGrpcOptions`
- **Exceptions** — `TendermintClientException`, `TendermintRestException`, `TendermintWebSocketException`, `TendermintGrpcException`

## Usage

This package is a dependency of the transport packages. Consume it directly only if you need to reference domain types or interfaces without a specific transport.

```csharp
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Interfaces;
```

## Related packages

| Package | Transport |
|---------|-----------|
| `CometBFT.Client.Rest` | REST / JSON-RPC 2.0 |
| `CometBFT.Client.WebSocket` | WebSocket subscriptions |
| `CometBFT.Client.Grpc` | gRPC BroadcastAPI |
| `CometBFT.Client.Extensions` | Dependency injection |
