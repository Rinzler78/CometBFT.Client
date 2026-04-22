using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

public sealed class NewBlockEventsDataTests
{
    private static BlockHeader MakeHeader(long height = 1) => new(
        "11", "test-chain", height,
        new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, "PROPOSER");

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var header = MakeHeader(42);
        var events = new List<CometBftEvent>
        {
            new("transfer", new List<AbciEventEntry> { new("recipient", "addr1", true) }.AsReadOnly())
        }.AsReadOnly();

        var data = new NewBlockEventsData(header, 42L, events);

        Assert.Equal(header, data.Header);
        Assert.Equal(42L, data.Height);
        Assert.Single(data.Events);
        Assert.Equal("transfer", data.Events[0].Type);
    }

    [Fact]
    public void Constructor_EmptyEvents_IsValid()
    {
        var data = new NewBlockEventsData(MakeHeader(), 1L, []);
        Assert.Empty(data.Events);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var header = MakeHeader(10);
        var events = new List<CometBftEvent>().AsReadOnly();
        var a = new NewBlockEventsData(header, 10L, events);
        var b = new NewBlockEventsData(header, 10L, events);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentHeight_NotEqual()
    {
        var header = MakeHeader(10);
        var events = new List<CometBftEvent>().AsReadOnly();
        var a = new NewBlockEventsData(header, 10L, events);
        var b = new NewBlockEventsData(header, 11L, events);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesHeight()
    {
        var data = new NewBlockEventsData(MakeHeader(5), 5L, []);
        var updated = data with { Height = 99L };
        Assert.Equal(99L, updated.Height);
        Assert.Equal(data.Header, updated.Header);
    }
}
