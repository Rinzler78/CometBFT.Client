using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="NetworkInfo"/> and <see cref="NetworkPeer"/> records.
/// </summary>
public sealed class NetworkInfoTests
{
    // ── NetworkPeer ──────────────────────────────────────────────────────────

    [Fact]
    public void NetworkPeer_Constructor_SetsAllProperties()
    {
        var peer = new NetworkPeer("peer1", "node-a", "cosmoshub-4", "10.0.0.1", "connected");

        Assert.Equal("peer1", peer.NodeId);
        Assert.Equal("node-a", peer.Moniker);
        Assert.Equal("cosmoshub-4", peer.Network);
        Assert.Equal("10.0.0.1", peer.RemoteIp);
        Assert.Equal("connected", peer.ConnectionStatus);
    }

    [Fact]
    public void NetworkPeer_Equality_SameValues_AreEqual()
    {
        var a = new NetworkPeer("p1", "m", "net", "1.2.3.4", "ok");
        var b = new NetworkPeer("p1", "m", "net", "1.2.3.4", "ok");
        Assert.Equal(a, b);
    }

    [Fact]
    public void NetworkPeer_Equality_DifferentNodeId_NotEqual()
    {
        var a = new NetworkPeer("p1", "m", "net", "1.2.3.4", "ok");
        var b = new NetworkPeer("p2", "m", "net", "1.2.3.4", "ok");
        Assert.NotEqual(a, b);
    }

    // ── NetworkInfo ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var peer = new NetworkPeer("p1", "m", "net", "1.2.3.4", "ok");
        var peers = new List<NetworkPeer> { peer }.AsReadOnly();
        var listeners = new List<string> { "tcp://0.0.0.0:26656" }.AsReadOnly();

        var info = new NetworkInfo(true, listeners, 1, peers);

        Assert.True(info.Listening);
        Assert.Single(info.Listeners);
        Assert.Equal(1, info.PeerCount);
        Assert.Single(info.Peers);
        Assert.Equal("p1", info.Peers[0].NodeId);
    }

    [Fact]
    public void Listening_False_IsPreserved()
    {
        var info = new NetworkInfo(false, new List<string>().AsReadOnly(), 0, new List<NetworkPeer>().AsReadOnly());
        Assert.False(info.Listening);
    }

    [Fact]
    public void Peers_IsReadOnly()
    {
        var info = new NetworkInfo(true, new List<string>().AsReadOnly(), 0, new List<NetworkPeer>().AsReadOnly());
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.Generic.IList<NetworkPeer>)info.Peers)
                .Add(new NetworkPeer("x", "x", "x", "x", "x")));
    }

    [Fact]
    public void Listeners_IsReadOnly()
    {
        var info = new NetworkInfo(true, new List<string>().AsReadOnly(), 0, new List<NetworkPeer>().AsReadOnly());
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.Generic.IList<string>)info.Listeners).Add("extra"));
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var listeners = new List<string>().AsReadOnly();
        var peers = new List<NetworkPeer>().AsReadOnly();
        var a = new NetworkInfo(true, listeners, 0, peers);
        var b = new NetworkInfo(true, listeners, 0, peers);
        Assert.Equal(a, b);
    }
}
