using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using CometBFT.Client.Grpc.Proto;

namespace CometBFT.Client.Grpc.Internal;

/// <summary>
/// Production implementation of <see cref="IBroadcastApiClient"/> that delegates to
/// the proto-generated <see cref="BroadcastAPI.BroadcastAPIClient"/> over a gRPC channel.
/// </summary>
/// <remarks>
/// Compiled from CometBFT v0.38.9 proto:
/// <c>proto/tendermint/rpc/grpc/types.proto</c>
/// (https://github.com/cometbft/cometbft).
/// </remarks>
internal sealed class GrpcChannelBroadcastApiClient : IBroadcastApiClient
{
    private readonly BroadcastAPI.BroadcastAPIClient _client;

    /// <summary>
    /// Initializes a new instance of <see cref="GrpcChannelBroadcastApiClient"/>.
    /// </summary>
    /// <param name="channel">A configured and open <see cref="GrpcChannel"/>.</param>
    public GrpcChannelBroadcastApiClient(GrpcChannel channel)
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
