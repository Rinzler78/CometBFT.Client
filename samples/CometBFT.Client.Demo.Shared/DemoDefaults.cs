namespace CometBFT.Client.Demo.Shared;

/// <summary>
/// Default public endpoints used by all demo projects.
/// All URLs target Cosmos Hub mainnet via Lava Network.
///
/// IMPORTANT — endpoint type distinction:
///   RpcUrl / WsUrl → CometBFT JSON-RPC (consensus layer, tendermintrpc)
///   GrpcUrl        → CometBFT BroadcastAPI gRPC relay —
///                    exposes <c>cometbft.rpc.grpc.BroadcastAPI</c>
///                    (legacy alias: <c>tendermint.rpc.grpc.BroadcastAPI</c>)
///                    (used by <c>ICometBftGrpcClient</c>)
/// </summary>
public static class DemoDefaults
{
    /// <summary>CometBFT JSON-RPC / REST endpoint — Lava Network relay, Cosmos Hub mainnet.</summary>
    public const string RpcUrl = "https://cosmoshub.tendermintrpc.lava.build:443";

    /// <summary>
    /// gRPC endpoint — Lava Network relay, Cosmos Hub mainnet.
    /// Exposes <c>cometbft.rpc.grpc.BroadcastAPI</c>
    /// (legacy alias: <c>tendermint.rpc.grpc.BroadcastAPI</c>)
    /// for <c>ICometBftGrpcClient</c>.
    /// </summary>
    public const string GrpcUrl = "https://cosmoshub.grpc.lava.build:443";

    /// <summary>CometBFT WebSocket endpoint — Lava Network relay, Cosmos Hub mainnet.</summary>
    public const string WsUrl = "wss://cosmoshub.tendermintrpc.lava.build:443/websocket";
}
