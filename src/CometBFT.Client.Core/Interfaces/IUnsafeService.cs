namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides access to the CometBFT "Unsafe" RPC endpoints.
/// These endpoints are only available when the node is started with
/// <c>--rpc.unsafe=true</c> and must never be called against public-facing nodes.
/// </summary>
/// <remarks>
/// CometBFT v0.38.9 exposes two unsafe endpoints:
/// <list type="bullet">
///   <item><term>dial_seeds</term><description>Instructs the node to dial a list of seed addresses.</description></item>
///   <item><term>dial_peers</term><description>Instructs the node to dial a list of peer addresses with optional flags.</description></item>
/// </list>
/// </remarks>
public interface IUnsafeService
{
    /// <summary>
    /// Instructs the node to dial the specified seed peers.
    /// Requires <c>--rpc.unsafe=true</c> on the target node.
    /// </summary>
    /// <param name="peers">
    /// The list of seed peer addresses in <c>id@host:port</c> format.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DialSeedsAsync(
        IReadOnlyList<string> peers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Instructs the node to dial the specified peers.
    /// Requires <c>--rpc.unsafe=true</c> on the target node.
    /// </summary>
    /// <param name="peers">
    /// The list of peer addresses in <c>id@host:port</c> format.
    /// </param>
    /// <param name="persistent">
    /// When <c>true</c>, adds the peers as persistent peers that the node will
    /// always try to reconnect to.
    /// </param>
    /// <param name="unconditional">
    /// When <c>true</c>, dials the peers unconditionally even if the peer limit
    /// has been reached.
    /// </param>
    /// <param name="isPrivate">
    /// When <c>true</c>, marks the peers as private (not gossiped to other nodes).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DialPeersAsync(
        IReadOnlyList<string> peers,
        bool persistent = false,
        bool unconditional = false,
        bool isPrivate = false,
        CancellationToken cancellationToken = default);
}
