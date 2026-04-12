namespace CometBFT.Client.Grpc.Internal;

/// <summary>
/// Internal abstraction over the generated gRPC broadcast stub.
/// Enables unit testing without a live gRPC channel.
/// </summary>
internal interface IBroadcastApiClient
{
    /// <summary>
    /// Sends a ping to verify the gRPC channel is alive.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if the server acknowledged the ping.</returns>
    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a raw transaction via gRPC.
    /// </summary>
    /// <param name="txBytes">The raw transaction bytes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A tuple with all ABCI <c>check_tx</c> fields:
    /// <c>Code</c>, <c>Data</c> (base64 or <c>null</c>), <c>Log</c>, <c>GasWanted</c>, <c>GasUsed</c>, <c>Codespace</c>, <c>Hash</c>.
    /// </returns>
    Task<(uint Code, string? Data, string? Log, long GasWanted, long GasUsed, string? Codespace, string Hash)> BroadcastTxAsync(
        byte[] txBytes,
        CancellationToken cancellationToken = default);
}
