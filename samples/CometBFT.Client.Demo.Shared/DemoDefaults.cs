namespace CometBFT.Client.Demo.Shared;

/// <summary>
/// Default public endpoints used by all demo projects.
/// All four URLs target the same operator (Lava Network) so all demos
/// talk to the same Cosmos Hub infrastructure.
///
/// IMPORTANT — endpoint type distinction:
///   RpcUrl / WsUrl → CometBFT JSON-RPC (consensus layer, tendermintrpc)
///   GrpcUrl        → Lava gRPC relay — exposes BOTH:
///                    • CometBFT RPC gRPC: <c>tendermint.rpc.grpc.v1beta1.BroadcastAPI</c>
///                      (used by <c>ICometBftGrpcClient</c>)
///                    • Cosmos SDK gRPC:   <c>cosmos.base.tendermint.v1beta1.Service</c>
///                      (used by <c>ICometBftSdkGrpcClient</c>)
/// </summary>
public static class DemoDefaults
{
    /// <summary>CometBFT JSON-RPC / REST endpoint — Lava Network relay, Cosmos Hub mainnet.</summary>
    public const string RpcUrl = "https://cosmoshub.tendermintrpc.lava.build:443";

    /// <summary>
    /// gRPC endpoint — Lava Network relay, Cosmos Hub mainnet.
    /// Serves both <c>cometbft.rpc.grpc.v1beta1.BroadcastAPI</c> (for <c>ICometBftGrpcClient</c>)
    /// and <c>cosmos.base.tendermint.v1beta1.Service</c> (for <c>ICometBftSdkGrpcClient</c>).
    /// </summary>
    public const string GrpcUrl = "https://cosmoshub.grpc.lava.build:443";

    /// <summary>CometBFT WebSocket endpoint — Lava Network relay, Cosmos Hub mainnet.</summary>
    public const string WsUrl = "wss://cosmoshub.tendermintrpc.lava.build:443/websocket";
}
