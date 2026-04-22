namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the header of a CometBFT block containing chain metadata.
/// </summary>
/// <param name="Version">The protocol version of the block.</param>
/// <param name="ChainId">The identifier of the blockchain network.</param>
/// <param name="Height">The block height.</param>
/// <param name="Time">The block timestamp in UTC.</param>
/// <param name="LastBlockId">The hash of the previous block ID.</param>
/// <param name="LastCommitHash">The hash of the last commit.</param>
/// <param name="DataHash">The Merkle hash of all transactions in this block.</param>
/// <param name="ValidatorsHash">The hash of the current validator set.</param>
/// <param name="NextValidatorsHash">The hash of the next validator set.</param>
/// <param name="ConsensusHash">The hash of the consensus parameters.</param>
/// <param name="AppHash">The application state hash after the previous block.</param>
/// <param name="LastResultsHash">The Merkle hash of the results of the previous block's transactions.</param>
/// <param name="EvidenceHash">The hash of evidence of misbehaviour.</param>
/// <param name="ProposerAddress">The address of the block proposer.</param>
public record BlockHeader(
    string Version,
    string ChainId,
    long Height,
    DateTimeOffset Time,
    string LastBlockId,
    string LastCommitHash,
    string DataHash,
    string ValidatorsHash,
    string NextValidatorsHash,
    string ConsensusHash,
    string AppHash,
    string LastResultsHash,
    string EvidenceHash,
    string ProposerAddress);
