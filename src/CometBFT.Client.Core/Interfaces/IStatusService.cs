using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides node status operations for a CometBFT node.
/// </summary>
public interface IStatusService
{
    /// <summary>
    /// Retrieves the current status of the CometBFT node including sync state and node info.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple containing <see cref="NodeInfo"/> and <see cref="SyncInfo"/> for the node.</returns>
    Task<(NodeInfo NodeInfo, SyncInfo SyncInfo)> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves network connectivity information for the node.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A normalized <see cref="NetworkInfo"/> snapshot.</returns>
    Task<NetworkInfo> GetNetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the summarized consensus state.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A string dictionary containing the important consensus state fields.</returns>
    Task<IReadOnlyDictionary<string, string>> GetConsensusStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full consensus dump.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A string dictionary containing the important dump fields.</returns>
    Task<IReadOnlyDictionary<string, string>> DumpConsensusStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves consensus parameters at the specified height.
    /// </summary>
    /// <param name="height">The block height, or <c>null</c> for latest.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The normalized consensus parameters.</returns>
    Task<ConsensusParamsInfo> GetConsensusParamsAsync(long? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the genesis document summary.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A string dictionary containing the important genesis fields.</returns>
    Task<IReadOnlyDictionary<string, string>> GetGenesisAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single chunk of the genesis document.
    /// </summary>
    /// <param name="chunk">The chunk index to fetch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The requested <see cref="GenesisChunk"/>.</returns>
    Task<GenesisChunk> GetGenesisChunkAsync(int chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves unconfirmed transactions from the mempool.
    /// </summary>
    /// <param name="limit">The maximum number of transactions to return.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The mempool transaction summary.</returns>
    Task<UnconfirmedTxsInfo> GetUnconfirmedTxsAsync(int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a count-only view of unconfirmed transactions.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The mempool transaction summary.</returns>
    Task<UnconfirmedTxsInfo> GetNumUnconfirmedTxsAsync(CancellationToken cancellationToken = default);
}
