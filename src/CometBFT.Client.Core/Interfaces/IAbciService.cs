namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides ABCI (Application BlockChain Interface) operations for a CometBFT node.
/// </summary>
public interface IAbciService
{
    /// <summary>
    /// Retrieves ABCI application information from the node.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A dictionary of ABCI info key-value pairs.</returns>
    Task<IReadOnlyDictionary<string, string>> GetAbciInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a raw ABCI query against the application.
    /// </summary>
    /// <param name="path">The query path (e.g., <c>"/store/acc/key"</c>).</param>
    /// <param name="data">The hex-encoded query data.</param>
    /// <param name="height">The block height to query at, or <c>null</c> for latest.</param>
    /// <param name="prove">Whether to include a Merkle proof in the response.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A dictionary of query result key-value pairs.</returns>
    Task<IReadOnlyDictionary<string, string>> AbciQueryAsync(
        string path,
        string data,
        long? height = null,
        bool prove = false,
        CancellationToken cancellationToken = default);
}
