namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Payload for the <c>NewBlockEvents</c> WebSocket event (tm.event='NewBlockEvents').
/// Carries the committed block header and all ABCI events from <c>FinalizeBlock</c>,
/// enabling on-chain activity indexing without per-block REST polling.
/// </summary>
/// <param name="Header">The committed block header.</param>
/// <param name="Height">The block height.</param>
/// <param name="Events">
/// All ABCI events emitted during block processing. Each item has a <see cref="CometBftEvent.Type"/>
/// (e.g. <c>"ibc_transfer"</c>) and typed attributes.
/// </param>
public sealed record NewBlockEventsData(
    BlockHeader Header,
    long Height,
    IReadOnlyList<CometBftEvent> Events);
