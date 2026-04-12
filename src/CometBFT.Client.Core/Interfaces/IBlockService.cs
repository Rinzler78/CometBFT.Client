using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides block retrieval operations for a CometBFT chain.
/// </summary>
public interface IBlockService
{
    /// <summary>
    /// Retrieves a block by its height. Returns the latest block when <paramref name="height"/> is <c>null</c>.
    /// </summary>
    /// <param name="height">The block height, or <c>null</c> for the latest block.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The <see cref="Block"/> at the specified height.</returns>
    Task<Block> GetBlockAsync(long? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a block by its hex-encoded hash.
    /// </summary>
    /// <param name="hash">
    /// The block hash in hex format. Both bare hex (<c>ABCDEF…</c>) and prefixed hex
    /// (<c>0xABCDEF…</c>) are accepted; the client normalizes the <c>0x</c> prefix
    /// internally before forwarding to CometBFT.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The <see cref="Block"/> with the specified hash.</returns>
    Task<Block> GetBlockByHashAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the execution results for a block at the specified height.
    /// Returns the latest block results when <paramref name="height"/> is <c>null</c>.
    /// </summary>
    /// <param name="height">The block height, or <c>null</c> for the latest block results.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of <see cref="TxResult"/> for each transaction in the block.</returns>
    Task<IReadOnlyList<TxResult>> GetBlockResultsAsync(long? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a block header by height.
    /// </summary>
    /// <param name="height">The block height, or <c>null</c> for the latest header.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching <see cref="BlockHeader"/>.</returns>
    Task<BlockHeader> GetHeaderAsync(long? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a block header by hash.
    /// </summary>
    /// <param name="hash">The block hash in bare or <c>0x</c>-prefixed hexadecimal form.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching <see cref="BlockHeader"/>.</returns>
    Task<BlockHeader> GetHeaderByHashAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves block headers within a height range.
    /// </summary>
    /// <param name="minHeight">The minimum height to include.</param>
    /// <param name="maxHeight">The maximum height to include.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BlockchainInfo"/> result containing the returned headers.</returns>
    Task<BlockchainInfo> GetBlockchainAsync(long? minHeight = null, long? maxHeight = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves commit information for the specified block height.
    /// </summary>
    /// <param name="height">The block height, or <c>null</c> for the latest commit.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A string dictionary containing the important commit fields.</returns>
    Task<IReadOnlyDictionary<string, string>> GetCommitAsync(long? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches blocks by FinalizeBlock events.
    /// </summary>
    /// <param name="query">The CometBFT event query string.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="perPage">The number of results per page.</param>
    /// <param name="orderBy">The sort order, typically <c>asc</c> or <c>desc</c>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of matching <see cref="Block"/> values.</returns>
    Task<IReadOnlyList<Block>> SearchBlocksAsync(
        string query,
        int? page = null,
        int? perPage = null,
        string? orderBy = null,
        CancellationToken cancellationToken = default);
}
