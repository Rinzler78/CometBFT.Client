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
}
