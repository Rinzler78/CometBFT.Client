namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents information about a CometBFT node.
/// </summary>
/// <param name="Id">The peer ID (hex-encoded public key).</param>
/// <param name="ListenAddr">The address the node is listening on.</param>
/// <param name="Network">The chain/network identifier.</param>
/// <param name="Version">The CometBFT software version.</param>
/// <param name="Channels">The hex-encoded channels bitmask.</param>
/// <param name="Moniker">The human-readable node name.</param>
/// <param name="ProtocolVersion">The protocol version details.</param>
public sealed record NodeInfo(
    string Id,
    string ListenAddr,
    string Network,
    string Version,
    string Channels,
    string Moniker,
    ProtocolVersion ProtocolVersion);

/// <summary>
/// Represents the protocol version triplet for a CometBFT node.
/// </summary>
/// <param name="P2P">The P2P protocol version.</param>
/// <param name="Block">The block protocol version.</param>
/// <param name="App">The application protocol version.</param>
public sealed record ProtocolVersion(
    ulong P2P,
    ulong Block,
    ulong App);
