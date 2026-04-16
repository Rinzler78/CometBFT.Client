using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Block"/> record.
/// </summary>
public sealed class BlockTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var txs = new List<string> { "tx1", "tx2" }.AsReadOnly();
        var time = DateTimeOffset.UtcNow;

        var block = new Block(42L, "AABBCC", time, "proposerXYZ", txs);

        Assert.Equal(42L, block.Height);
        Assert.Equal("AABBCC", block.Hash);
        Assert.Equal(time, block.Time);
        Assert.Equal("proposerXYZ", block.Proposer);
        Assert.Equal(2, block.Txs.Count);
        Assert.Equal("tx1", block.Txs[0]);
        Assert.Equal("tx2", block.Txs[1]);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var txs = new List<string>().AsReadOnly();

        var a = new Block(1L, "HASH1", time, "proposer1", txs);
        var b = new Block(1L, "HASH1", time, "proposer1", txs);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentHeight_NotEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var txs = new List<string>().AsReadOnly();

        var a = new Block(1L, "HASH1", time, "proposer1", txs);
        var b = new Block(2L, "HASH1", time, "proposer1", txs);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_CreatesNewRecordWithChangedProperty()
    {
        var time = DateTimeOffset.UtcNow;
        var txs = new List<string>().AsReadOnly();
        var original = new Block(1L, "HASH1", time, "proposer1", txs);

        var modified = original with { Height = 99L };

        Assert.Equal(99L, modified.Height);
        Assert.Equal(original.Hash, modified.Hash);
        Assert.Equal(original.Time, modified.Time);
        Assert.Equal(original.Proposer, modified.Proposer);
    }

    [Fact]
    public void ToString_ContainsHeight()
    {
        var block = new Block(123L, "AABB", DateTimeOffset.UtcNow, "addr", new List<string>().AsReadOnly());
        Assert.Contains("123", block.ToString());
    }

    [Fact]
    public void Txs_ContainsInitialItems()
    {
        var block = new Block(1L, "H", DateTimeOffset.UtcNow, "p", new List<string> { "a" }.AsReadOnly());
        Assert.Single(block.Txs);
    }

    [Fact]
    public void Txs_IsReadOnly()
    {
        var block = new Block(1L, "H", DateTimeOffset.UtcNow, "p", new List<string> { "a" }.AsReadOnly());
        // ReadOnlyCollection<T> is read-only by design — mutation must throw.
        Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)block.Txs).Add("b"));
    }
}
