namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the current synchronization state of a CometBFT node.
/// </summary>
/// <param name="LatestBlockHash">The hash of the latest block known to this node.</param>
/// <param name="LatestAppHash">The application hash of the latest block.</param>
/// <param name="LatestBlockHeight">The height of the latest block.</param>
/// <param name="LatestBlockTime">The timestamp of the latest block.</param>
/// <param name="EarliestBlockHash">The hash of the earliest block retained by this node.</param>
/// <param name="EarliestAppHash">The application hash of the earliest block.</param>
/// <param name="EarliestBlockHeight">The height of the earliest block retained.</param>
/// <param name="EarliestBlockTime">The timestamp of the earliest block retained.</param>
/// <param name="CatchingUp">Whether the node is currently catching up to the network tip.</param>
public sealed record SyncInfo(
    string LatestBlockHash,
    string LatestAppHash,
    long LatestBlockHeight,
    DateTimeOffset LatestBlockTime,
    string EarliestBlockHash,
    string EarliestAppHash,
    long EarliestBlockHeight,
    DateTimeOffset EarliestBlockTime,
    bool CatchingUp);
