namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents an immutable CometBFT consensus vote received over WebSocket.
/// </summary>
/// <param name="Type">The vote type (1 = Prevote, 2 = Precommit).</param>
/// <param name="Height">The block height for which the vote was cast.</param>
/// <param name="Round">The consensus round.</param>
/// <param name="ValidatorAddress">The hex-encoded address of the validator that cast the vote.</param>
/// <param name="Timestamp">The UTC timestamp of the vote.</param>
public sealed record Vote(
    int Type,
    long Height,
    int Round,
    string ValidatorAddress,
    DateTimeOffset Timestamp);
