# Change: WebSocket Events Completeness + Protocol Version v0.38.9 → v0.39.1 — v2.1.0

## Why

The current WebSocket client (`ICometBftWebSocketClient`) subscribes to five events:
`NewBlock`, `Tx`, `NewRound`, `NewRoundStep`, `Vote`. The CometBFT event system exposes
significantly more events relevant to production clients, most critically `NewBlockEvents`,
which fires on every committed block and carries **all ABCI events** in a single message.
DeFi clients (Cosmos.Client, Osmosis.Client) rely on `NewBlockEvents` to index swaps, IBC
transfers, staking rewards, and governance actions without polling the REST API per block.

A gap audit against `cometbft/cometbft` `types/events.go` (v0.39.1) identified 13 additional
subscribable events not implemented in the current client.

## Goals

1. Add `NewBlockEvents` subscription — critical path for all downstream consumers.
2. Add the remaining consensus-internal events at subscriber's discretion (opt-in topic strings).
3. Bump protocol version target from v0.38.9 to v0.39.1.
4. Keep the change **additive for consumers** — existing `SubscribeXAsync` methods and
   their signatures are unchanged. New members are added to the public
   `ICometBftWebSocketClient<...>` generic interface; this is a source-compat break for
   any third-party code that *implements* the interface (they'd need to implement the
   new members) but is transparent for the much more common case of callers that only
   inject or consume the interface. We assume no external implementer today.

## What Changes

### New WebSocket event topics (wired as `tm.event` filter values)

| Priority | Event constant | `tm.event` value | Description |
|----------|---------------|------------------|-------------|
| 🔴 Critical | `EventNewBlockEvents` | `NewBlockEvents` | Committed block + all ABCI events (IBC, staking, DEX) |
| 🟡 Moderate | `EventCompleteProposal` | `CompleteProposal` | Consensus complete-proposal step |
| 🟡 Moderate | `EventValidatorSetUpdates` | `ValidatorSetUpdates` | Validator set changed |
| 🟡 Moderate | `EventNewEvidence` | `NewEvidence` | New evidence submitted |
| 🟢 Low | `EventTimeoutPropose` | `TimeoutPropose` | Propose timeout |
| 🟢 Low | `EventTimeoutWait` | `TimeoutWait` | Wait timeout |
| 🟢 Low | `EventLock` | `Lock` | Block locked in consensus |
| 🟢 Low | `EventUnlock` | `Unlock` | Block unlocked |
| 🟢 Low | `EventRelock` | `Relock` | Block relocked |
| 🟢 Low | `EventPolkaAny` | `PolkaAny` | Polka on any block |
| 🟢 Low | `EventPolkaNil` | `PolkaNil` | Polka on nil |
| 🟢 Low | `EventPolkaAgain` | `PolkaAgain` | Polka repeated |
| 🟢 Low | `EventMissingProposalBlock` | `MissingProposalBlock` | Proposal block missing |

### New domain type: `NewBlockEventsData`

`NewBlockEvents` delivers a payload distinct from `NewBlock`. It carries the full block
header + a list of `CometBftEvent` items collected from all transactions and
`FinalizeBlock` (formerly `BeginBlock`/`EndBlock` in CometBFT < 0.38). Each `CometBftEvent`
has a `Type` string (e.g. `"ibc_transfer"`) and `Attributes: IReadOnlyList<AbciEventEntry>`
(key-value pairs). A dedicated record is required (typed JSON schema rule — no `JsonElement`).

```csharp
public sealed record NewBlockEventsData(
    BlockHeader Header,
    long Height,
    IReadOnlyList<CometBftEvent> Events   // CometBftEvent = (Type: string, Attributes: IReadOnlyList<AbciEventEntry>)
);
```

### Interface additions

`ICometBftWebSocketClient<TTx, TBlock, TTxResult, TValidator>` gains:

```csharp
IObservable<NewBlockEventsData> NewBlockEventsStream { get; }
IObservable<CompleteProposalData> CompleteProposalStream { get; }
IObservable<ValidatorSetUpdatesData> ValidatorSetUpdatesStream { get; }
IObservable<NewEvidenceData> NewEvidenceStream { get; }
// Low-priority consensus events (opt-in, topic string only — no typed domain type required)
IObservable<CometBftEvent> ConsensusInternalStream { get; } // merged: TimeoutPropose/Wait/Lock/Unlock/Relock/Polka*/MissingProposalBlock
```

Non-generic shims inherit via the existing `ICometBftWebSocketClient : ICometBftWebSocketClient<...>` shim — no change needed.

### Protocol version bump

`<ProtocolVersion>` in `Directory.Build.props`: `v0.38.9` → `v0.39.1`

## Non-Breaking Guarantee

- All existing properties and methods on `ICometBftWebSocketClient` are preserved.
- Existing subscriptions (`NewBlockStream`, `TxStream`, `NewRoundStream`, `NewRoundStepStream`,
  `VoteStream`) are not modified.
- `NewBlockEventsData` is a new `sealed record` (no inheritance impact).
- `CompleteProposalData`, `ValidatorSetUpdatesData`, `NewEvidenceData` are new `sealed record`
  types (protocol-pure).

## Relay Rate-Limit Constraint

CometBFT's default configuration caps subscriptions at **5 per connection**
(`rpc.max_subscriptions_per_client`, defined in `config/config.go`). The demo dashboard
subscribes to 7 topics in a single burst; on a standard node the 6th and 7th subscribes
are rejected with a JSON-RPC error (`"max_subscriptions_per_client 5 reached"`), which
is surfaced via `ErrorOccurred`. The dashboard's `Resilient()` helper handles this
gracefully: surviving topics continue to stream, and the UI status shows
`"Degraded (n/7 topics)"` instead of `"Connected"`.

**Production guidance:** raise `rpc.max_subscriptions_per_client` to at least 8 (7 dashboard
topics + headroom), or drop low-priority topics (e.g. `NewEvidence`, `ValidatorSetUpdates`)
to stay within the default budget.

## Version

Additive change, no interface removal, no type shape change → **v2.1.0**.

## Consumer Validation

Cosmos.Client and Osmosis.Client consume `NewBlockEventsStream` to index on-chain events
without polling per-block REST endpoints. The `NewBlockEventsData.Events` list replaces the
need to call `/tx_search?query=tx.height=N` for each committed block.

```csharp
// Cosmos.Client usage
wsClient.NewBlockEventsStream
    .SelectMany(d => d.Events)
    .Where(e => e.Type == "ibc_transfer")
    .Subscribe(OnIbcTransfer);
```
