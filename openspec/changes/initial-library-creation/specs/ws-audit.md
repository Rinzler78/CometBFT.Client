# WebSocket Audit — CometBFT.Client

**Audit date**: 2026-04-12  
**Protocol target**: CometBFT v0.38.9

## Verdict

**WebSocket coverage is complete** for the public subscription surface exposed by
CometBFT v0.38.9. All 5 standard event subscriptions are implemented and exposed
through `ITendermintWebSocketClient`.

---

## CometBFT v0.38.9 WebSocket subscription matrix

| Subscription query | tm.event | .NET subscription method | .NET event | Status |
|-------------------|----------|--------------------------|------------|--------|
| `tm.event='NewBlock'` | NewBlock | `SubscribeNewBlockAsync` | `NewBlockReceived` (`Block`) | ✓ Implemented |
| `tm.event='NewBlockHeader'` | NewBlockHeader | `SubscribeNewBlockHeaderAsync` | `NewBlockHeaderReceived` (`BlockHeader`) | ✓ Implemented |
| `tm.event='Tx'` | Tx | `SubscribeTxAsync` | `TxExecuted` (`TxResult`) | ✓ Implemented |
| `tm.event='Vote'` | Vote | `SubscribeVoteAsync` | `VoteReceived` (`Vote`) | ✓ Implemented |
| `tm.event='ValidatorSetUpdates'` | ValidatorSetUpdates | `SubscribeValidatorSetUpdatesAsync` | `ValidatorSetUpdated` (`IReadOnlyList<Validator>`) | ✓ Implemented |

---

## Consensus-internal events (not publicly subscribed in v0.38.9)

The following events exist in CometBFT internals but are **not** exposed as public
WebSocket subscriptions in v0.38.9 (they require node-level access or are only
available via consensus debugging):

| Event | Reason not implemented |
|-------|----------------------|
| `RoundState` | Not a standard subscription event; accessed via `dump_consensus_state` REST |
| `CompleteProposal` | Consensus-internal; not advertised in v0.38.9 public WS API |
| `TimeoutPropose` / `TimeoutWait` | Consensus-internal; not available via standard subscription |
| `PolkaVote` | Not part of the v0.38.9 public event set |
| `Evidence` | Available via `broadcast_evidence` REST, not as a WS subscription event |

---

## Connection lifecycle methods

| Method | Status |
|--------|--------|
| `ConnectAsync` | ✓ Implemented |
| `DisconnectAsync` | ✓ Implemented |
| `UnsubscribeAllAsync` | ✓ Implemented |

---

## Domain types used in WebSocket events

| Event | Domain type | Core module |
|-------|------------|-------------|
| `NewBlockReceived` | `Block` | `CometBFT.Client.Core.Domain.Block` |
| `NewBlockHeaderReceived` | `BlockHeader` | `CometBFT.Client.Core.Domain.BlockHeader` |
| `TxExecuted` | `TxResult` | `CometBFT.Client.Core.Domain.TxResult` |
| `VoteReceived` | `Vote` | `CometBFT.Client.Core.Domain.Vote` |
| `ValidatorSetUpdated` | `IReadOnlyList<Validator>` | `CometBFT.Client.Core.Domain.Validator` |

All domain types are shared with the REST client — no duplication (see `domain-matrix.md`).

---

## Test coverage summary

| Test type | Coverage |
|-----------|----------|
| Unit (without connection guard) | NewBlock, Tx, Vote, ValidatorSetUpdates, NewBlockHeader, UnsubscribeAll |
| Unit (event handler wire-up) | NewBlockReceived, TxExecuted, NewBlockHeaderReceived, VoteReceived, ValidatorSetUpdated |
| Integration (live) | NewBlock connection + event reception |
| E2E (live) | NewBlock flow |

Gaps closed by this audit cycle: `ValidatorSetUpdated` event wire-up test added;
integration and E2E extended to cover additional subscriptions where feasible.
