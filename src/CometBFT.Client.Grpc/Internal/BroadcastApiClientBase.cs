using Google.Protobuf;

namespace CometBFT.Client.Grpc.Internal;

/// <summary>
/// Provides the shared BroadcastTx response mapping logic used by both
/// <see cref="GrpcChannelBroadcastApiClient"/> and <see cref="LegacyBroadcastApiClient"/>.
/// </summary>
internal static class BroadcastApiClientBase
{
    /// <summary>
    /// Builds the broadcast result tuple from the check-tx fields returned by the node.
    /// </summary>
    /// <param name="code">The check-tx result code.</param>
    /// <param name="data">The check-tx data bytes.</param>
    /// <param name="log">The check-tx log string.</param>
    /// <param name="gasWanted">The gas requested.</param>
    /// <param name="gasUsed">The gas actually used.</param>
    /// <param name="codespace">The codespace string.</param>
    /// <param name="txBytes">The original transaction bytes, used as a local hash fallback.</param>
    /// <returns>A tuple carrying all broadcast result fields.</returns>
    internal static (uint Code, string? Data, string? Log, long GasWanted, long GasUsed, string? Codespace, string Hash)
        BuildResult(
            uint code,
            ByteString data,
            string log,
            long gasWanted,
            long gasUsed,
            string codespace,
            byte[] txBytes)
    {
        // The gRPC proto (v0.38.9) does not return a transaction hash in the broadcast response.
        // We fall back to the hex-encoding of the submitted tx bytes as a local identifier.
        var hash = Convert.ToHexString(txBytes);

        return (
            code,
            data.IsEmpty ? null : Convert.ToBase64String(data.ToByteArray()),
            string.IsNullOrEmpty(log) ? null : log,
            gasWanted,
            gasUsed,
            string.IsNullOrEmpty(codespace) ? null : codespace,
            hash);
    }
}
