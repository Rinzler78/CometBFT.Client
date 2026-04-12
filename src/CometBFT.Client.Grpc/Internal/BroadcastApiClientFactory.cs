using Grpc.Core;
using Grpc.Net.Client;
using CometBFT.Client.Core.Options;

namespace CometBFT.Client.Grpc.Internal;

/// <summary>
/// Creates the appropriate <see cref="IBroadcastApiClient"/> for a given <see cref="GrpcProtocol"/>.
/// When <see cref="GrpcProtocol.Auto"/> is selected, probes the node to detect whether it speaks
/// the CometBFT (<c>cometbft.rpc.grpc</c>) or legacy Tendermint Core (<c>tendermint.rpc.grpc</c>) protocol.
/// </summary>
internal static class BroadcastApiClientFactory
{
    /// <summary>
    /// Returns an <see cref="IBroadcastApiClient"/> resolved according to <paramref name="protocol"/>.
    /// For <see cref="GrpcProtocol.Auto"/>, performs a live Ping probe; the method returns as soon
    /// as the protocol is determined.
    /// </summary>
    /// <param name="channel">The open gRPC channel to the node.</param>
    /// <param name="protocol">The desired protocol selection strategy.</param>
    /// <param name="cancellationToken">A token to cancel the probe.</param>
    /// <returns>The resolved <see cref="IBroadcastApiClient"/>.</returns>
    internal static async Task<IBroadcastApiClient> CreateAsync(
        GrpcChannel channel,
        GrpcProtocol protocol,
        CancellationToken cancellationToken)
    {
        return protocol switch
        {
            GrpcProtocol.CometBft => new GrpcChannelBroadcastApiClient(channel),
            GrpcProtocol.TendermintLegacy => new LegacyBroadcastApiClient(channel),
            _ => await DetectAsync(channel, cancellationToken).ConfigureAwait(false),
        };
    }

    private static async Task<IBroadcastApiClient> DetectAsync(
        GrpcChannel channel,
        CancellationToken cancellationToken)
    {
        // Probe CometBFT first: a successful ping means the node speaks cometbft.rpc.grpc.
        // Unimplemented / NotFound means the service is unknown → fall back to legacy.
        // Any other failure (network, auth, …) defaults to CometBFT to avoid masking real errors.
        try
        {
            var stub = new Proto.BroadcastAPI.BroadcastAPIClient(channel);
            await stub.PingAsync(new Proto.RequestPing(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new GrpcChannelBroadcastApiClient(channel);
        }
        catch (RpcException ex) when (
            ex.StatusCode is StatusCode.Unimplemented or StatusCode.NotFound)
        {
            return new LegacyBroadcastApiClient(channel);
        }
        catch
        {
            // Network unavailable, auth error, etc. — default to CometBFT; the caller
            // will surface the real error on the first business call.
            return new GrpcChannelBroadcastApiClient(channel);
        }
    }
}
