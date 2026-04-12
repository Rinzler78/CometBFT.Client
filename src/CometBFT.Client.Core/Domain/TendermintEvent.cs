namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents an ABCI event emitted during transaction or block processing.
/// </summary>
/// <param name="Type">The event type identifier (e.g., "transfer", "message").</param>
/// <param name="Attributes">The key-value attributes associated with this event.</param>
public sealed record TendermintEvent(
    string Type,
    IReadOnlyList<AbciEventEntry> Attributes);
