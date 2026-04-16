using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="NodeInfo"/> and <see cref="ProtocolVersion"/> records.
/// </summary>
public sealed class NodeInfoTests
{
    private static ProtocolVersion MakeProtocol(ulong p2p = 8UL, ulong block = 11UL, ulong app = 0UL) =>
        new(p2p, block, app);

    private static NodeInfo MakeNode(
        string id = "node123",
        string listenAddr = "tcp://0.0.0.0:26656",
        string network = "testnet",
        string version = "0.38.9",
        string channels = "40",
        string moniker = "mynode",
        ProtocolVersion? protocol = null) =>
        new(id, listenAddr, network, version, channels, moniker, protocol ?? MakeProtocol());

    // ── NodeInfo ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var protocol = MakeProtocol(8UL, 11UL, 1UL);
        var node = MakeNode(id: "abc", network: "cosmoshub-4", version: "0.38.9", protocol: protocol);

        Assert.Equal("abc", node.Id);
        Assert.Equal("tcp://0.0.0.0:26656", node.ListenAddr);
        Assert.Equal("cosmoshub-4", node.Network);
        Assert.Equal("0.38.9", node.Version);
        Assert.Equal("40", node.Channels);
        Assert.Equal("mynode", node.Moniker);
        Assert.Equal(8UL, node.ProtocolVersion.P2P);
        Assert.Equal(11UL, node.ProtocolVersion.Block);
        Assert.Equal(1UL, node.ProtocolVersion.App);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = MakeNode();
        var b = MakeNode();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentId_NotEqual()
    {
        var a = MakeNode(id: "node1");
        var b = MakeNode(id: "node2");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesNetwork()
    {
        var node = MakeNode(network: "old-net");
        var updated = node with { Network = "new-net" };

        Assert.Equal("new-net", updated.Network);
        Assert.Equal(node.Id, updated.Id);
    }

    // ── ProtocolVersion ──────────────────────────────────────────────────────

    [Fact]
    public void ProtocolVersion_Constructor_SetsAllProperties()
    {
        var pv = new ProtocolVersion(8UL, 11UL, 2UL);

        Assert.Equal(8UL, pv.P2P);
        Assert.Equal(11UL, pv.Block);
        Assert.Equal(2UL, pv.App);
    }

    [Fact]
    public void ProtocolVersion_Equality_SameValues_AreEqual()
    {
        var a = new ProtocolVersion(8UL, 11UL, 0UL);
        var b = new ProtocolVersion(8UL, 11UL, 0UL);
        Assert.Equal(a, b);
    }

    [Fact]
    public void ProtocolVersion_Equality_DifferentBlock_NotEqual()
    {
        var a = new ProtocolVersion(8UL, 10UL, 0UL);
        var b = new ProtocolVersion(8UL, 11UL, 0UL);
        Assert.NotEqual(a, b);
    }
}
