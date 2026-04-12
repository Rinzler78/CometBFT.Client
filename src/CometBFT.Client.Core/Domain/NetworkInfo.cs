namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents a normalized view of the <c>/net_info</c> RPC response.
/// </summary>
/// <param name="Listening">Whether the node is currently listening for inbound connections.</param>
/// <param name="Listeners">Configured listener addresses.</param>
/// <param name="PeerCount">The number of peers returned in the response.</param>
/// <param name="Peers">Connected peers as seen by the node.</param>
public sealed record NetworkInfo(
    bool Listening,
    IReadOnlyList<string> Listeners,
    int PeerCount,
    IReadOnlyList<NetworkPeer> Peers);

/// <summary>
/// Represents a single peer entry from the <c>/net_info</c> RPC response.
/// </summary>
/// <param name="NodeId">The remote node identifier.</param>
/// <param name="Moniker">The peer moniker.</param>
/// <param name="Network">The peer network identifier.</param>
/// <param name="RemoteIp">The remote IP address.</param>
/// <param name="ConnectionStatus">The peer connection status string.</param>
public sealed record NetworkPeer(
    string NodeId,
    string Moniker,
    string Network,
    string RemoteIp,
    string ConnectionStatus);
