namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents a CometBFT validator participating in consensus.
/// </summary>
/// <param name="Address">The hex-encoded validator address.</param>
/// <param name="PubKey">The base64-encoded public key of the validator.</param>
/// <param name="VotingPower">The current voting power of the validator.</param>
/// <param name="ProposerPriority">The proposer priority used in round-robin proposer selection.</param>
public record Validator(
    string Address,
    string PubKey,
    long VotingPower,
    long ProposerPriority);
