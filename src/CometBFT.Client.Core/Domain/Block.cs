namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents an immutable CometBFT block.
/// </summary>
/// <param name="Height">The block height (chain sequence number).</param>
/// <param name="Hash">The hex-encoded block hash.</param>
/// <param name="Time">The block timestamp in UTC.</param>
/// <param name="Proposer">The base64-encoded proposer address.</param>
/// <param name="Txs">The list of base64-encoded transactions included in this block.</param>
public sealed record Block(
    long Height,
    string Hash,
    DateTimeOffset Time,
    string Proposer,
    IReadOnlyList<string> Txs);
