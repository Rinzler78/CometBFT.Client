using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="UnconfirmedTxsInfo"/> record.
/// </summary>
public sealed class UnconfirmedTxsInfoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var txs = new List<string> { "dHgx", "dHgy" }.AsReadOnly();
        var info = new UnconfirmedTxsInfo(Count: 2, Total: 5, TotalBytes: 100, Txs: txs);

        Assert.Equal(2, info.Count);
        Assert.Equal(5, info.Total);
        Assert.Equal(100, info.TotalBytes);
        Assert.Equal(2, info.Txs.Count);
        Assert.Equal("dHgx", info.Txs[0]);
    }

    [Fact]
    public void Txs_IsReadOnly()
    {
        var info = new UnconfirmedTxsInfo(0, 0, 0, new List<string> { "tx" }.AsReadOnly());
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.Generic.IList<string>)info.Txs).Add("extra"));
    }

    [Fact]
    public void Empty_AllCountersAreZero()
    {
        var info = new UnconfirmedTxsInfo(0, 0, 0, new List<string>().AsReadOnly());

        Assert.Equal(0, info.Count);
        Assert.Equal(0, info.Total);
        Assert.Equal(0, info.TotalBytes);
        Assert.Empty(info.Txs);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var txs = new List<string>().AsReadOnly();
        var a = new UnconfirmedTxsInfo(1, 2, 50, txs);
        var b = new UnconfirmedTxsInfo(1, 2, 50, txs);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentTotal_NotEqual()
    {
        var txs = new List<string>().AsReadOnly();
        var a = new UnconfirmedTxsInfo(0, 2, 0, txs);
        var b = new UnconfirmedTxsInfo(0, 3, 0, txs);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesTotal()
    {
        var info = new UnconfirmedTxsInfo(1, 1, 50, new List<string>().AsReadOnly());
        var updated = info with { Total = 10 };

        Assert.Equal(10, updated.Total);
        Assert.Equal(info.Count, updated.Count);
    }
}
