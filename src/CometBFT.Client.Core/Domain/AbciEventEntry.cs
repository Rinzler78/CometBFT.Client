namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents a key-value entry attached to an ABCI event.
/// </summary>
/// <param name="Key">The entry key (base64-encoded in some protocol versions).</param>
/// <param name="Value">The entry value (base64-encoded in some protocol versions), or <c>null</c> if absent.</param>
/// <param name="Index">Whether this entry is indexed for querying.</param>
public sealed record AbciEventEntry(
    string Key,
    string? Value,
    bool Index);
