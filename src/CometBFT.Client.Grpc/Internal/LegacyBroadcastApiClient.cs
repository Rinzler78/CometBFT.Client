using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Tendermint.Client.Grpc.LegacyProto;

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

    /// <summary>
    /// Initializes a new instance of <see cref="LegacyBroadcastApiClient"/> with an injected client.
    /// </summary>
    internal LegacyBroadcastApiClient(BroadcastAPI.BroadcastAPIClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException)
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
        return BroadcastApiClientBase.BuildResult(
            checkTx.Code, checkTx.Data, checkTx.Log,
            checkTx.GasWanted, checkTx.GasUsed, checkTx.Codespace, txBytes);
    }
}
