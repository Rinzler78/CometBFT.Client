using Google.Protobuf;
using Grpc.Net.Client;
using CometBFT.Client.Grpc.LegacyProto;

namespace CometBFT.Client.Grpc.Internal;

/// <summary>
/// Implementation of <see cref="IBroadcastApiClient"/> for legacy Tendermint Core nodes
/// (proto package <c>tendermint.rpc.grpc</c>, Tendermint Core up to v0.37).
/// </summary>
internal sealed class LegacyBroadcastApiClient : IBroadcastApiClient
{
    private readonly BroadcastAPI.BroadcastAPIClient _client;

    /// <summary>
    /// Initializes a new instance of <see cref="LegacyBroadcastApiClient"/>.
    /// </summary>
    /// <param name="channel">A configured and open <see cref="GrpcChannel"/>.</param>
    public LegacyBroadcastApiClient(GrpcChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _client = new BroadcastAPI.BroadcastAPIClient(channel);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.PingAsync(new RequestPing(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(uint Code, string? Data, string? Log, long GasWanted, long GasUsed, string? Codespace, string Hash)> BroadcastTxAsync(
        byte[] txBytes,
        CancellationToken cancellationToken = default)
    {
        var request = new RequestBroadcastTx { Tx = ByteString.CopyFrom(txBytes) };
        var response = await _client.BroadcastTxAsync(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var checkTx = response.CheckTx;
        var hash = Convert.ToHexString(txBytes);

        return (
            checkTx.Code,
            checkTx.Data.IsEmpty ? null : Convert.ToBase64String(checkTx.Data.ToByteArray()),
            string.IsNullOrEmpty(checkTx.Log) ? null : checkTx.Log,
            checkTx.GasWanted,
            checkTx.GasUsed,
            string.IsNullOrEmpty(checkTx.Codespace) ? null : checkTx.Codespace,
            hash);
    }
}
