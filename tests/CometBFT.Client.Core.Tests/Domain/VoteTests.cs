using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Vote"/> record.
/// </summary>
public sealed class VoteTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var time = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var vote = new Vote(1, 100L, 0, "VALADDR", time);

        Assert.Equal(1, vote.Type);
        Assert.Equal(100L, vote.Height);
        Assert.Equal(0, vote.Round);
        Assert.Equal("VALADDR", vote.ValidatorAddress);
        Assert.Equal(time, vote.Timestamp);
    }

    [Theory]
    [InlineData(1, "Prevote")]
    [InlineData(2, "Precommit")]
    public void Type_DistinguishesPrevoteAndPrecommit(int type, string label)
    {
        var vote = new Vote(type, 1L, 0, "ADDR", DateTimeOffset.UtcNow);
        // Type 1 = Prevote, Type 2 = Precommit per CometBFT spec.
        Assert.Equal(type, vote.Type);
        _ = label; // label documents intent only
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new Vote(1, 50L, 0, "ADDR", time);
        var b = new Vote(1, 50L, 0, "ADDR", time);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentHeight_NotEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new Vote(1, 50L, 0, "ADDR", time);
        var b = new Vote(1, 51L, 0, "ADDR", time);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesRound()
    {
        var time = DateTimeOffset.UtcNow;
        var vote = new Vote(1, 100L, 0, "ADDR", time);
        var updated = vote with { Round = 1 };

        Assert.Equal(1, updated.Round);
        Assert.Equal(vote.Height, updated.Height);
        Assert.Equal(vote.ValidatorAddress, updated.ValidatorAddress);
    }
}
