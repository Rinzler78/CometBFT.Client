# CometBFT.Client.Rest

REST/JSON-RPC 2.0 client for [CometBFT](https://github.com/cometbft/cometbft) nodes.
Targets protocol version **v0.38.9** — all 30 public REST endpoints covered.

## Installation

```
dotnet add package CometBFT.Client.Rest
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;

var services = new ServiceCollection();
services.AddCometBftRest(o => o.BaseUrl = "http://localhost:26657");
var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ICometBftRestClient>();

bool healthy = await client.GetHealthAsync();
var (nodeInfo, syncInfo) = await client.GetStatusAsync();
var block = await client.GetBlockAsync();
var validators = await client.GetValidatorsAsync();
```

## Features

- All public CometBFT REST endpoints (health, status, block, validators, tx, ABCI, net, consensus, genesis, …)
- Unsafe endpoints (`dial_seeds`, `dial_peers`) via `IUnsafeService` when enabled on the node
- Polly retry (exponential + jitter, 3 attempts) + circuit breaker
- Source-generated `System.Text.Json` serialization — zero reflection
- `HttpCompletionOption.ResponseHeadersRead` on all responses
