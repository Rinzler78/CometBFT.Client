namespace CometBFT.Client.Core.Options;

/// <summary>
/// Selects the gRPC wire protocol (proto package) used by the CometBFT/Tendermint node.
/// </summary>
public enum GrpcProtocol
{
    /// <summary>
    /// Automatically detects the protocol by probing the node on the first call.
    /// Tries <see cref="CometBft"/> first; falls back to <see cref="TendermintLegacy"/>
    /// if the service is not found.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// CometBFT v0.38+ protocol — proto package <c>cometbft.rpc.grpc</c>.
    /// Used by all actively maintained Cosmos chains since late 2023.
    /// </summary>
    CometBft = 1,

    /// <summary>
    /// Legacy Tendermint Core protocol — proto package <c>tendermint.rpc.grpc</c>.
    /// Used by Tendermint Core up to v0.37 (unmaintained).
    /// </summary>
    TendermintLegacy = 2,
}
