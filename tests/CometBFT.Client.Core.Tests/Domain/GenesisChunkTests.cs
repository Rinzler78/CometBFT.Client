using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="GenesisChunk"/> record.
/// </summary>
public sealed class GenesisChunkTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var chunk = new GenesisChunk(2, 5, "YWJj");

        Assert.Equal(2, chunk.Chunk);
        Assert.Equal(5, chunk.Total);
        Assert.Equal("YWJj", chunk.Data);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new GenesisChunk(0, 3, "DATA");
        var b = new GenesisChunk(0, 3, "DATA");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentChunkIndex_NotEqual()
    {
        var a = new GenesisChunk(0, 3, "DATA");
        var b = new GenesisChunk(1, 3, "DATA");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesData()
    {
        var original = new GenesisChunk(0, 2, "OLD");
        var updated = original with { Data = "NEW" };

        Assert.Equal("NEW", updated.Data);
        Assert.Equal(original.Chunk, updated.Chunk);
        Assert.Equal(original.Total, updated.Total);
    }

    [Fact]
    public void FirstChunk_IndexIsZero()
    {
        var chunk = new GenesisChunk(0, 10, "DATA");
        Assert.Equal(0, chunk.Chunk);
    }

    [Fact]
    public void LastChunk_IndexEqualsTotalMinusOne()
    {
        var chunk = new GenesisChunk(9, 10, "DATA");
        Assert.Equal(9, chunk.Chunk);
        Assert.Equal(10, chunk.Total);
    }
}
