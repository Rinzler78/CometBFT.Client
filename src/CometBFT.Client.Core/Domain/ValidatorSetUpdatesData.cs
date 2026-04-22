namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Payload for the <c>ValidatorSetUpdates</c> WebSocket event
/// (tm.event='ValidatorSetUpdates').
/// </summary>
/// <param name="ValidatorUpdates">The updated validator set.</param>
public sealed record ValidatorSetUpdatesData(IReadOnlyList<Validator> ValidatorUpdates);
