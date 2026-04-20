using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides access to a CometBFT/Cosmos node via the Cosmos SDK gRPC service
/// (<c>cosmos.base.tendermint.v1beta1.Service</c>).
/// This service is widely available on Cosmos-ecosystem nodes (port 9090 by convention)
/// and exposes node info, latest block, validator set, and syncing status.
/// </summary>
public interface ICometBftSdkGrpcClient : IAsyncDisposable
{
    /// <summary>
    /// Returns node identification and current sync state.
    /// Combines <c>GetNodeInfo</c> and <c>GetSyncing</c> into a single logical call.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A tuple of <see cref="NodeInfo"/> (identification) and <see cref="SyncInfo"/>
    /// (latest block height / catching-up flag).
    /// </returns>
    Task<(NodeInfo NodeInfo, SyncInfo SyncInfo)> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest committed block.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Block"/> describing the latest block.</returns>
    Task<Block> GetLatestBlockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current validator set.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="Validator"/> records.</returns>
    Task<IReadOnlyList<Validator>> GetLatestValidatorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the node is currently catching up (syncing).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if the node is still catching up; <c>false</c> if fully synced.</returns>
    Task<bool> GetSyncingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the committed block at the specified height.
    /// </summary>
    /// <param name="height">The block height to fetch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Block"/> at the given height.</returns>
    Task<Block> GetBlockByHeightAsync(long height, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the validator set at the specified block height.
    /// </summary>
    /// <param name="height">The block height to query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="Validator"/> records at the given height.</returns>
    Task<IReadOnlyList<Validator>> GetValidatorSetByHeightAsync(long height, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a raw ABCI query against the application layer.
    /// </summary>
    /// <param name="path">The ABCI query path (e.g. <c>/app/version</c>, <c>/store/bank/key</c>).</param>
    /// <param name="data">Query data bytes (key or encoded request depending on path).</param>
    /// <param name="height">Block height to query (0 = latest).</param>
    /// <param name="prove">Whether to request a Merkle proof.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="AbciQueryResponse"/> with the application's response.</returns>
    Task<AbciQueryResponse> ABCIQueryAsync(string path, byte[] data, long height = 0, bool prove = false, CancellationToken cancellationToken = default);
}
