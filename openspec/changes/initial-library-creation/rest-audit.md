# REST Audit Matrix — CometBFT v0.38.9

Audit source: upstream OpenAPI `rpc/openapi/openapi.yaml` from CometBFT `v0.38.9`.

## Summary — updated 2026-04-12

- OpenAPI REST paths audited: `30`
- .NET REST methods implemented: **30** (all endpoints including Unsafe)
- Missing REST endpoints in the client: **0**

## Matrix

| OpenAPI path | .NET method | Client | Unit test | Live integration | E2E | Demo REST |
|---|---|---|---|---|---|---|
| `/health` | `GetHealthAsync` | Yes | Yes | Yes | Yes | Yes |
| `/status` | `GetStatusAsync` | Yes | Yes | Yes | Yes | Yes |
| `/net_info` | `GetNetInfoAsync` | Yes | Yes | Yes | No | Yes |
| `/dial_seeds` (Unsafe) | `DialSeedsAsync` | Yes | Yes | Yes* | Yes* | Yes (--unsafe) |
| `/dial_peers` (Unsafe) | `DialPeersAsync` | Yes | Yes | Yes* | Yes* | Yes (--unsafe) |
| `/blockchain` | `GetBlockchainAsync` | Yes | Yes | Yes | No | No |
| `/header` | `GetHeaderAsync` | Yes | Yes | Yes | No | No |
| `/header_by_hash` | `GetHeaderByHashAsync` | Yes | Yes | No | No | No |
| `/block` | `GetBlockAsync` | Yes | Yes | Yes | Yes | Yes |
| `/block_by_hash` | `GetBlockByHashAsync` | Yes | Yes | No | No | No |
| `/block_results` | `GetBlockResultsAsync` | Yes | Yes | Yes | Yes | Yes |
| `/commit` | `GetCommitAsync` | Yes | Yes | Yes | No | No |
| `/validators` | `GetValidatorsAsync` | Yes | Yes | Yes | Yes | Yes |
| `/genesis` | `GetGenesisAsync` | Yes | Yes | Yes | No | No |
| `/genesis_chunked` | `GetGenesisChunkAsync` | Yes | Yes | No | No | No |
| `/dump_consensus_state` | `DumpConsensusStateAsync` | Yes | Yes | No | No | No |
| `/consensus_state` | `GetConsensusStateAsync` | Yes | Yes | No | No | No |
| `/consensus_params` | `GetConsensusParamsAsync` | Yes | Yes | Yes | No | No |
| `/unconfirmed_txs` | `GetUnconfirmedTxsAsync` | Yes | Yes | Yes | No | Yes |
| `/num_unconfirmed_txs` | `GetNumUnconfirmedTxsAsync` | Yes | Yes | Yes | No | No |
| `/tx_search` | `SearchTxAsync` | Yes | Yes | No | No | No |
| `/block_search` | `SearchBlocksAsync` | Yes | Yes | No | No | No |
| `/tx` | `GetTxAsync` | Yes | Yes | No | No | No |
| `/abci_info` | `GetAbciInfoAsync` | Yes | Yes | Yes | Yes | Yes |
| `/abci_query` | `AbciQueryAsync` | Yes | Yes | No | No | No |
| `/broadcast_tx_sync` | `BroadcastTxSyncAsync` | Yes | Yes | No | No | No |
| `/broadcast_tx_async` | `BroadcastTxAsync` | Yes | Yes | No | No | No |
| `/broadcast_tx_commit` | `BroadcastTxCommitAsync` | Yes | Yes | No | No | No |
| `/check_tx` | `CheckTxAsync` | Yes | Yes | No | No | No |
| `/broadcast_evidence` | `BroadcastEvidenceAsync` | Yes | Yes | No | No | No |

*Unsafe endpoints require `TENDERMINT_UNSAFE_RPC_URL` (node started with `--rpc.unsafe=true`).

## Findings

1. REST client parity with OpenAPI is close but not complete: `/dial_seeds` and `/dial_peers` are still missing from the public .NET API.
2. The two missing endpoints are tagged `Unsafe` upstream, which means they require a dedicated validation environment rather than the public Cosmos Hub defaults used by the existing live suites.
3. Unit coverage for implemented REST methods is already broad, but live integration and E2E coverage still exercise only a small operational subset.
4. The REST demo currently covers `GetHealthAsync`, `GetStatusAsync`, `GetBlockAsync`, `GetBlockResultsAsync`, `GetValidatorsAsync`, and `GetAbciInfoAsync`; the rest of the supported REST surface is not yet discoverable through the demo.

## Immediate Follow-up

- Add `dial_seeds` and `dial_peers` to `ITendermintRestClient` and `TendermintRestClient`.
- Add unit tests for the missing unsafe endpoints.
- Define a controlled integration/E2E environment with unsafe RPC enabled.
- Expand the REST demo so every supported REST capability is reachable, even if some advanced or unsafe paths live behind an explicit menu or warning.
