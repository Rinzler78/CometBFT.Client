using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="BlockchainInfo"/> record.
/// </summary>
public sealed class BlockchainInfoTests
{
    private static BlockHeader MakeHeader(long height = 1L) =>
        new("11", "testnet", height, DateTimeOffset.UtcNow,
            "PREV", "LC", "DH", "VH", "NVH", "CH", "AH", "LRH", "EH", "PROP");

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var headers = new List<BlockHeader> { MakeHeader(10L), MakeHeader(9L) }.AsReadOnly();
        var info = new BlockchainInfo(99L, headers);

        Assert.Equal(99L, info.LastHeight);
        Assert.Equal(2, info.Headers.Count);
        Assert.Equal(10L, info.Headers[0].Height);
        Assert.Equal(9L, info.Headers[1].Height);
    }

    [Fact]
    public void Headers_IsReadOnly()
    {
        var info = new BlockchainInfo(0L, new List<BlockHeader>().AsReadOnly());
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.Generic.IList<BlockHeader>)info.Headers).Add(MakeHeader()));
    }

    [Fact]
    public void Headers_Empty_IsAllowed()
    {
        var info = new BlockchainInfo(0L, new List<BlockHeader>().AsReadOnly());
        Assert.Empty(info.Headers);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var headers = new List<BlockHeader>().AsReadOnly();
        var a = new BlockchainInfo(5L, headers);
        var b = new BlockchainInfo(5L, headers);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentLastHeight_NotEqual()
    {
        var headers = new List<BlockHeader>().AsReadOnly();
        var a = new BlockchainInfo(5L, headers);
        var b = new BlockchainInfo(6L, headers);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesLastHeight()
    {
        var info = new BlockchainInfo(1L, new List<BlockHeader>().AsReadOnly());
        var updated = info with { LastHeight = 100L };

        Assert.Equal(100L, updated.LastHeight);
    }
}
