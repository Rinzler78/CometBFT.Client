using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="CometBftEvent"/> and <see cref="AbciEventEntry"/> records.
/// </summary>
public sealed class CometBftEventTests
{
    // ── AbciEventEntry ───────────────────────────────────────────────────────

    [Fact]
    public void AbciEventEntry_Constructor_SetsAllProperties()
    {
        var entry = new AbciEventEntry("sender", "cosmos1abc", true);

        Assert.Equal("sender", entry.Key);
        Assert.Equal("cosmos1abc", entry.Value);
        Assert.True(entry.Index);
    }

    [Fact]
    public void AbciEventEntry_Value_CanBeNull()
    {
        var entry = new AbciEventEntry("key", null, false);
        Assert.Null(entry.Value);
        Assert.False(entry.Index);
    }

    [Fact]
    public void AbciEventEntry_Equality_SameValues_AreEqual()
    {
        var a = new AbciEventEntry("key", "val", true);
        var b = new AbciEventEntry("key", "val", true);
        Assert.Equal(a, b);
    }

    [Fact]
    public void AbciEventEntry_Equality_DifferentKey_NotEqual()
    {
        var a = new AbciEventEntry("k1", "val", true);
        var b = new AbciEventEntry("k2", "val", true);
        Assert.NotEqual(a, b);
    }

    // ── CometBftEvent ────────────────────────────────────────────────────────

    [Fact]
    public void CometBftEvent_Constructor_SetsAllProperties()
    {
        var attrs = new List<AbciEventEntry>
        {
            new("sender", "cosmos1abc", true),
            new("amount", "1000uatom", true),
        }.AsReadOnly();

        var evt = new CometBftEvent("transfer", attrs);

        Assert.Equal("transfer", evt.Type);
        Assert.Equal(2, evt.Attributes.Count);
        Assert.Equal("sender", evt.Attributes[0].Key);
        Assert.Equal("amount", evt.Attributes[1].Key);
    }

    [Fact]
    public void CometBftEvent_Attributes_IsReadOnly()
    {
        var evt = new CometBftEvent("transfer", new List<AbciEventEntry>().AsReadOnly());
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.Generic.IList<AbciEventEntry>)evt.Attributes)
                .Add(new AbciEventEntry("k", "v", false)));
    }

    [Fact]
    public void CometBftEvent_Equality_SameValues_AreEqual()
    {
        var attrs = new List<AbciEventEntry>().AsReadOnly();
        var a = new CometBftEvent("message", attrs);
        var b = new CometBftEvent("message", attrs);
        Assert.Equal(a, b);
    }

    [Fact]
    public void CometBftEvent_Equality_DifferentType_NotEqual()
    {
        var attrs = new List<AbciEventEntry>().AsReadOnly();
        var a = new CometBftEvent("transfer", attrs);
        var b = new CometBftEvent("message", attrs);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void CometBftEvent_WithExpression_UpdatesType()
    {
        var attrs = new List<AbciEventEntry>().AsReadOnly();
        var original = new CometBftEvent("transfer", attrs);
        var updated = original with { Type = "message" };

        Assert.Equal("message", updated.Type);
        Assert.Same(original.Attributes, updated.Attributes);
    }
}
