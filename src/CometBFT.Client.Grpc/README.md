# CometBFT.Client.Grpc

gRPC client for [CometBFT](https://github.com/cometbft/cometbft) nodes (BroadcastAPI service).
Targets protocol version **v0.38.9**.

## Installation

```
dotnet add package CometBFT.Client.Grpc
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;

var services = new ServiceCollection();
services.AddCometBftGrpc(o => o.Host = "localhost:26657");
var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ICometBftGrpcClient>();

bool alive = await client.PingAsync();

byte[] txBytes = ...; // encoded transaction
BroadcastTxResult result = await client.BroadcastTxAsync(txBytes);
Console.WriteLine($"Code={result.Code} Hash={result.Hash} Gas={result.GasUsed}/{result.GasWanted}");
```

## Features

- `PingAsync` — liveness check
- `BroadcastTxAsync` — submit a transaction, returns full `ResponseCheckTx` mapping
  (Code, Data, Log, GasWanted, GasUsed, Codespace, Hash)
- Polly retry + circuit breaker on the gRPC channel
- Proto vendored from CometBFT `v0.38.9`
