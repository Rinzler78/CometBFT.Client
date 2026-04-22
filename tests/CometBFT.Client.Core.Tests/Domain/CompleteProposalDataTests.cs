using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

public sealed class CompleteProposalDataTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var data = new CompleteProposalData(100L, 2, "BLOCK-ID42");

        Assert.Equal(100L, data.Height);
        Assert.Equal(2, data.Round);
        Assert.Equal("BLOCK-ID42", data.BlockId);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new CompleteProposalData(1L, 0, "ID");
        var b = new CompleteProposalData(1L, 0, "ID");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentRound_NotEqual()
    {
        var a = new CompleteProposalData(1L, 0, "ID");
        var b = new CompleteProposalData(1L, 1, "ID");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesRound()
    {
        var data = new CompleteProposalData(10L, 0, "ID");
        var updated = data with { Round = 3 };
        Assert.Equal(3, updated.Round);
        Assert.Equal(data.Height, updated.Height);
        Assert.Equal(data.BlockId, updated.BlockId);
    }
}
