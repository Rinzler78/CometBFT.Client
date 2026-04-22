using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides transaction querying and broadcasting operations for a CometBFT chain,
/// returning transaction results as <typeparamref name="TTxResult"/>.
/// </summary>
/// <typeparam name="TTxResult">
/// The transaction result type. Must inherit <see cref="TxResultBase"/>.
/// Use <see cref="ITxService"/> (non-generic) to work with plain <see cref="TxResult"/> instances.
/// </typeparam>
public interface ITxService<TTxResult> where TTxResult : TxResultBase
{
    /// <summary>
    /// Retrieves a transaction by its hex-encoded hash.
    /// </summary>
    /// <param name="hash">
    /// The transaction hash in hex format. Both bare hex (<c>ABCDEF…</c>) and prefixed hex
    /// (<c>0xABCDEF…</c>) are accepted; the client normalizes the <c>0x</c> prefix
    /// internally before forwarding to CometBFT.
    /// </param>
    /// <param name="prove">Whether to include a Merkle proof in the response.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The <typeparamref name="TTxResult"/> for the specified transaction.</returns>
    Task<TTxResult> GetTxAsync(string hash, bool prove = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for transactions matching an Amino query string.
    /// </summary>
    /// <param name="query">
    /// The Amino query expression (e.g., <c>"tx.height=5"</c> or <c>"transfer.recipient='addr'"</c>).
    /// </param>
    /// <param name="prove">Whether to include Merkle proofs in the results.</param>
    /// <param name="page">The 1-based page number for pagination.</param>
    /// <param name="perPage">The number of results per page (max 100).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of <typeparamref name="TTxResult"/> matching the query.</returns>
    Task<IReadOnlyList<TTxResult>> SearchTxAsync(
        string query,
        bool? prove = null,
        int? page = null,
        int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a transaction asynchronously (fire-and-forget, no wait for CheckTx).
    /// </summary>
    /// <param name="txBytes">The base64-encoded transaction bytes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BroadcastTxResult"/> with the transaction hash.</returns>
    Task<BroadcastTxResult> BroadcastTxAsync(string txBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a transaction synchronously (waits for CheckTx to complete).
    /// </summary>
    /// <param name="txBytes">The base64-encoded transaction bytes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BroadcastTxResult"/> with CheckTx result.</returns>
    Task<BroadcastTxResult> BroadcastTxSyncAsync(string txBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a transaction and waits for it to be committed to a block.
    /// </summary>
    /// <param name="txBytes">The base64-encoded transaction bytes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BroadcastTxResult"/> with both CheckTx and DeliverTx results.</returns>
    Task<BroadcastTxResult> BroadcastTxCommitAsync(string txBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a transaction with the application's <c>CheckTx</c> path without adding it to the mempool.
    /// </summary>
    /// <param name="txBytes">The base64-encoded transaction bytes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BroadcastTxResult"/> representing the check result.</returns>
    Task<BroadcastTxResult> CheckTxAsync(string txBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts encoded evidence of misbehavior.
    /// </summary>
    /// <param name="evidence">The evidence payload encoded for the RPC endpoint.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A string dictionary containing the important broadcast result fields.</returns>
    Task<IReadOnlyDictionary<string, string>> BroadcastEvidenceAsync(string evidence, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides transaction querying and broadcasting operations for a CometBFT chain,
/// returning plain <see cref="TxResult"/> instances.
/// </summary>
public interface ITxService : ITxService<TxResult> { }
