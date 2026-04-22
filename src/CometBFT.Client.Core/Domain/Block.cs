namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Shared base for all CometBFT block representations.
/// Carries the consensus-layer fields common to both the raw <see cref="Block"/>
/// and the decoded <see cref="Block{TTx}"/>, and serves as the extension point
/// for application-layer block types (e.g. a Cosmos block that adds AppHash).
/// </summary>
/// <param name="Height">The block height (chain sequence number).</param>
/// <param name="Hash">The hex-encoded block hash.</param>
/// <param name="Time">The block timestamp in UTC.</param>
/// <param name="Proposer">The base64-encoded proposer address.</param>
public abstract record BlockBase(
    long Height,
    string Hash,
    DateTimeOffset Time,
    string Proposer);

/// <summary>
/// Represents an immutable CometBFT block with raw base64-encoded transactions.
/// </summary>
/// <param name="Height">The block height (chain sequence number).</param>
/// <param name="Hash">The hex-encoded block hash.</param>
/// <param name="Time">The block timestamp in UTC.</param>
/// <param name="Proposer">The base64-encoded proposer address.</param>
/// <param name="Txs">The list of base64-encoded transactions included in this block.</param>
public record Block(
    long Height,
    string Hash,
    DateTimeOffset Time,
    string Proposer,
    IReadOnlyList<string> Txs) : BlockBase(Height, Hash, Time, Proposer);
