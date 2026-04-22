using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

public sealed class NewEvidenceDataTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var data = new NewEvidenceData(55L, "DuplicateVoteEvidence", "VALADDR1");

        Assert.Equal(55L, data.Height);
        Assert.Equal("DuplicateVoteEvidence", data.EvidenceType);
        Assert.Equal("VALADDR1", data.Validator);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new NewEvidenceData(1L, "DVE", "ADDR");
        var b = new NewEvidenceData(1L, "DVE", "ADDR");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentValidator_NotEqual()
    {
        var a = new NewEvidenceData(1L, "DVE", "ADDR1");
        var b = new NewEvidenceData(1L, "DVE", "ADDR2");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesHeight()
    {
        var data = new NewEvidenceData(10L, "DVE", "ADDR");
        var updated = data with { Height = 20L };
        Assert.Equal(20L, updated.Height);
        Assert.Equal(data.EvidenceType, updated.EvidenceType);
        Assert.Equal(data.Validator, updated.Validator);
    }
}
