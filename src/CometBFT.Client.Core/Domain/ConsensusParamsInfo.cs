namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents a normalized subset of CometBFT consensus parameters.
/// </summary>
/// <param name="BlockMaxBytes">Maximum block size in bytes.</param>
/// <param name="BlockMaxGas">Maximum gas per block.</param>
/// <param name="EvidenceMaxAgeNumBlocks">Maximum evidence age in blocks.</param>
/// <param name="EvidenceMaxAgeDuration">Maximum evidence age duration.</param>
/// <param name="ValidatorPubKeyTypes">Allowed validator public key types.</param>
/// <param name="VersionApp">The application protocol version.</param>
public sealed record ConsensusParamsInfo(
    long BlockMaxBytes,
    long BlockMaxGas,
    long EvidenceMaxAgeNumBlocks,
    string EvidenceMaxAgeDuration,
    IReadOnlyList<string> ValidatorPubKeyTypes,
    long VersionApp);
