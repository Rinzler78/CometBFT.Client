using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="BroadcastTxResult"/> record.
/// </summary>
public sealed class BroadcastTxResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var result = new BroadcastTxResult(
            Code: 0u,
            Data: "DATA",
            Log: "ok",
            Codespace: "sdk",
            Hash: "TXHASH",
            GasWanted: 100L,
            GasUsed: 80L);

        Assert.Equal(0u, result.Code);
        Assert.Equal("DATA", result.Data);
        Assert.Equal("ok", result.Log);
        Assert.Equal("sdk", result.Codespace);
        Assert.Equal("TXHASH", result.Hash);
        Assert.Equal(100L, result.GasWanted);
        Assert.Equal(80L, result.GasUsed);
    }

    [Fact]
    public void GasWanted_DefaultsToZero()
    {
        var result = new BroadcastTxResult(0u, null, null, null, "H");
        Assert.Equal(0L, result.GasWanted);
    }

    [Fact]
    public void GasUsed_DefaultsToZero()
    {
        var result = new BroadcastTxResult(0u, null, null, null, "H");
        Assert.Equal(0L, result.GasUsed);
    }

    [Fact]
    public void Data_CanBeNull()
    {
        var result = new BroadcastTxResult(0u, null, "log", null, "H");
        Assert.Null(result.Data);
    }

    [Fact]
    public void Codespace_CanBeNull()
    {
        var result = new BroadcastTxResult(0u, null, null, null, "H");
        Assert.Null(result.Codespace);
    }

    [Fact]
    public void Code_NonZero_IsError()
    {
        var result = new BroadcastTxResult(5u, null, "error", "sdk", "H");
        Assert.NotEqual(0u, result.Code);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new BroadcastTxResult(0u, "D", "ok", "sdk", "HASH", 10L, 5L);
        var b = new BroadcastTxResult(0u, "D", "ok", "sdk", "HASH", 10L, 5L);
        Assert.Equal(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesHash()
    {
        var original = new BroadcastTxResult(0u, null, null, null, "OLD");
        var updated = original with { Hash = "NEW" };

        Assert.Equal("NEW", updated.Hash);
        Assert.Equal(original.Code, updated.Code);
    }
}
