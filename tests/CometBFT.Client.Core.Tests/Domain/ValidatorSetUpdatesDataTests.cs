using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

public sealed class ValidatorSetUpdatesDataTests
{
    [Fact]
    public void Constructor_SetsValidatorUpdates()
    {
        var validators = new List<Validator>
        {
            new("ADDR1", "PUBKEY1", 1000L, 0),
            new("ADDR2", "PUBKEY2", 500L, -1)
        }.AsReadOnly();

        var data = new ValidatorSetUpdatesData(validators);

        Assert.Equal(2, data.ValidatorUpdates.Count);
        Assert.Equal("ADDR1", data.ValidatorUpdates[0].Address);
        Assert.Equal(500L, data.ValidatorUpdates[1].VotingPower);
    }

    [Fact]
    public void Constructor_EmptyList_IsValid()
    {
        var data = new ValidatorSetUpdatesData([]);
        Assert.Empty(data.ValidatorUpdates);
    }

    [Fact]
    public void Equality_SameReference_AreEqual()
    {
        var validators = new List<Validator>().AsReadOnly();
        var a = new ValidatorSetUpdatesData(validators);
        var b = new ValidatorSetUpdatesData(validators);
        Assert.Equal(a, b);
    }

    [Fact]
    public void WithExpression_ReplacesValidatorUpdates()
    {
        var data = new ValidatorSetUpdatesData([]);
        var newList = new List<Validator> { new("A", "K", 100L, 0) }.AsReadOnly();
        var updated = data with { ValidatorUpdates = newList };
        Assert.Single(updated.ValidatorUpdates);
    }
}
