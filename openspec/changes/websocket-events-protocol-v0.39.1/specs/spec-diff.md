# Spec Diff — websocket-events-completeness

Changes to public API surface introduced by v2.1.0.

**Compatibility caveat:** all changes are additive for *consumers* of the
interfaces (no member is removed or signature-changed). Adding members to the
public `ICometBftWebSocketClient<...>` generic interface is a source-compat
break for **third-party implementers** of that interface — they must add the
new members to compile against v2.1.0. We assume the interface is implemented
only by this repo's client; library consumers that only inject / consume the
interface are unaffected.

---

## 1. Protocol version

| | Value |
|---|---|
| Before | `v0.38.9` |
| After | `v0.39.1` |

`<ProtocolVersion>` in `Directory.Build.props` is updated. No client-facing breaking changes
exist between v0.38.x and v0.39.x on the WebSocket or REST surface.

---

## 2. New domain types (protocol-pure, sealed)

### `NewBlockEventsData`

Payload for the `NewBlockEvents` WebSocket event. Carries the committed block header and
all ABCI events from `FinalizeBlock` (formerly `BeginBlock`/`EndBlock`).
The `Events` list is the primary source for indexing on-chain activity without per-block REST polling.

```csharp
public sealed record NewBlockEventsData(
    BlockHeader Header,
    long Height,
    IReadOnlyList<CometBftEvent> Events   // CometBftEvent = (Type: string, Attributes: IReadOnlyList<AbciEventEntry>)
);
```

Each `CometBftEvent` in `Events` has a `Type` string (e.g. `"ibc_transfer"`, `"coin_received"`)
and an `Attributes` list of `AbciEventEntry` key-value pairs. Reuses existing `BlockHeader`,
`CometBftEvent`, and `AbciEventEntry` — no new sub-types required.

### `CompleteProposalData`

Payload for `CompleteProposal` consensus event.

```csharp
public sealed record CompleteProposalData(
    long Height,
    int Round,
    string BlockId
);
```

### `ValidatorSetUpdatesData`

Payload for `ValidatorSetUpdates` event.

```csharp
public sealed record ValidatorSetUpdatesData(
    IReadOnlyList<Validator> ValidatorUpdates
);
```

Reuses `Validator` (non-sealed, extensible).

### `NewEvidenceData`

Payload for `NewEvidence` event.

```csharp
public sealed record NewEvidenceData(
    long Height,
    string EvidenceType,
    string Validator
);
```

---

## 3. WebSocket interface additions

### `ICometBftWebSocketClient<TTx, TBlock, TTxResult, TValidator>` — new members

The four new typed streams are added to the generic interface. The non-generic shim
`ICometBftWebSocketClient<TTx> : ICometBftWebSocketClient<TTx, Block<TTx>, TxResult<TTx>, Validator>`
inherits them automatically.

```csharp
// 🔴 Critical — DeFi indexing
IObservable<NewBlockEventsData> NewBlockEventsStream { get; }

// 🟡 Moderate — consensus monitoring
IObservable<CompleteProposalData> CompleteProposalStream { get; }
IObservable<ValidatorSetUpdatesData> ValidatorSetUpdatesStream { get; }
IObservable<NewEvidenceData> NewEvidenceStream { get; }

// 🟢 Low — consensus internal (merged into a single opt-in stream, raw CometBftEvent)
// Topics: TimeoutPropose, TimeoutWait, Lock, Unlock, Relock,
//         PolkaAny, PolkaNil, PolkaAgain, MissingProposalBlock
IObservable<CometBftEvent> ConsensusInternalStream { get; }
```

All `*Stream` properties are initialized at construction time and safe to subscribe
before `ConnectAsync` is called.

**Relay rate-limit:** CometBFT's default `max_subscriptions_per_client = 5`. Subscribing
to more than 5 topics per connection causes the relay to reject the excess with a JSON-RPC
error delivered via `ErrorOccurred` (the `Task` still completes). Low-priority streams
(`ConsensusInternalStream`) should be activated last and are the first to drop under
a constrained budget.

**Subscription topic strings** (passed verbatim to `tm.event` query filter):

| Stream | `tm.event` value |
|--------|-----------------|
| `NewBlockEventsStream` | `NewBlockEvents` |
| `CompleteProposalStream` | `CompleteProposal` |
| `ValidatorSetUpdatesStream` | `ValidatorSetUpdates` |
| `NewEvidenceStream` | `NewEvidence` |
| `ConsensusInternalStream` | merged subscription — the nine consensus-internal topics listed in `WebSocketQueries.ConsensusInternalTopics` all dispatch into a single `Subject<CometBftEvent>`; the consumer sees a single stream of low-priority consensus events |

---

## 4. JSON deserialization

`NewBlockEventsData`, `CompleteProposalData`, `ValidatorSetUpdatesData`, `NewEvidenceData`
are added to the source-generated `JsonSerializerContext` (`[JsonSerializable]`). No runtime
reflection — all paths are AOT-safe.

---

## 5. DI extensions — no change

No new DI registration methods are required. Existing `AddCometBftWebSocket` overloads
are unchanged. Consumers activate the new streams by calling the corresponding explicit
`SubscribeXAsync` method (e.g. `SubscribeNewBlockEventsAsync`, `SubscribeConsensusInternalAsync`)
after `ConnectAsync`. The client does not auto-subscribe — each topic is opt-in to keep
per-connection rate-limit budgets predictable.

---

## 6. Existing subscriptions — unchanged

| Subscription | `tm.event` | API surface | Status |
|--------|-----------|--------|--------|
| `NewBlock` | `NewBlock` | `NewBlockReceived` (event) | Unchanged |
| `NewBlockHeader` | `NewBlockHeader` | `NewBlockHeaderReceived` (event) | Unchanged |
| `Tx` | `Tx` | `TxExecuted` (event) | Unchanged |
| `Vote` | `Vote` | `VoteReceived` (event) | Unchanged |
