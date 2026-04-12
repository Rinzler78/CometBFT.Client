using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides gRPC-based operations against a CometBFT node.
/// </summary>
public interface ICometBftGrpcClient : IAsyncDisposable
{
    /// <summary>
    /// Sends a ping to the CometBFT node via gRPC to verify connectivity.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if the node responded successfully; <c>false</c> otherwise.</returns>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a transaction to the CometBFT node via gRPC.
    /// </summary>
    /// <param name="txBytes">The raw transaction bytes as a byte array.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="BroadcastTxResult"/> containing the ABCI response.</returns>
    Task<BroadcastTxResult> BroadcastTxAsync(byte[] txBytes, CancellationToken cancellationToken = default);
}
