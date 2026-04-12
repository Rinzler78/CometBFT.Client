using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="Validator"/> record.
/// </summary>
public sealed class ValidatorTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var v = new Validator("ADDR1", "PUBKEY1", 1000L, 50L);

        Assert.Equal("ADDR1", v.Address);
        Assert.Equal("PUBKEY1", v.PubKey);
        Assert.Equal(1000L, v.VotingPower);
        Assert.Equal(50L, v.ProposerPriority);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Validator("A", "PK", 100L, 0L);
        var b = new Validator("A", "PK", 100L, 0L);

        Assert.Equal(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesVotingPower()
    {
        var original = new Validator("A", "PK", 100L, 0L);
        var updated = original with { VotingPower = 200L };

        Assert.Equal(200L, updated.VotingPower);
        Assert.Equal("A", updated.Address);
    }
}
