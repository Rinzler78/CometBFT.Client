namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the result of the <c>/blockchain</c> RPC endpoint.
/// </summary>
/// <param name="LastHeight">The highest block height available from the node.</param>
/// <param name="Headers">The returned block headers in descending height order.</param>
public record BlockchainInfo(
    long LastHeight,
    IReadOnlyList<BlockHeader> Headers);
