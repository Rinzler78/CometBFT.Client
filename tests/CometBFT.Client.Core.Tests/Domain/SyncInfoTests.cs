using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="SyncInfo"/> record.
/// </summary>
public sealed class SyncInfoTests
{
    private static SyncInfo Make(
        string latestBlockHash = "BLOCKHASH",
        string latestAppHash = "APPHASH",
        long latestBlockHeight = 100L,
        DateTimeOffset? latestBlockTime = null,
        string earliestBlockHash = "EARLIESTHASH",
        string earliestAppHash = "EARLIESTAPP",
        long earliestBlockHeight = 1L,
        DateTimeOffset? earliestBlockTime = null,
        bool catchingUp = false) =>
        new(latestBlockHash, latestAppHash, latestBlockHeight,
            latestBlockTime ?? DateTimeOffset.UtcNow,
            earliestBlockHash, earliestAppHash, earliestBlockHeight,
            earliestBlockTime ?? DateTimeOffset.UnixEpoch,
            catchingUp);

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var latestTime = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var earliestTime = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var info = new SyncInfo(
            "LHASH", "LAHASH", 999L, latestTime,
            "EHASH", "EAHASH", 1L, earliestTime,
            false);

        Assert.Equal("LHASH", info.LatestBlockHash);
        Assert.Equal("LAHASH", info.LatestAppHash);
        Assert.Equal(999L, info.LatestBlockHeight);
        Assert.Equal(latestTime, info.LatestBlockTime);
        Assert.Equal("EHASH", info.EarliestBlockHash);
        Assert.Equal("EAHASH", info.EarliestAppHash);
        Assert.Equal(1L, info.EarliestBlockHeight);
        Assert.Equal(earliestTime, info.EarliestBlockTime);
        Assert.False(info.CatchingUp);
    }

    [Fact]
    public void CatchingUp_True_IsPreserved()
    {
        var info = Make(catchingUp: true);
        Assert.True(info.CatchingUp);
    }

    [Fact]
    public void CatchingUp_False_IsPreserved()
    {
        var info = Make(catchingUp: false);
        Assert.False(info.CatchingUp);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = Make(latestBlockHeight: 50L, latestBlockTime: time);
        var b = Make(latestBlockHeight: 50L, latestBlockTime: time);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentHeight_NotEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = Make(latestBlockHeight: 50L, latestBlockTime: time);
        var b = Make(latestBlockHeight: 51L, latestBlockTime: time);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesCatchingUp()
    {
        var info = Make(catchingUp: false);
        var updated = info with { CatchingUp = true };

        Assert.True(updated.CatchingUp);
        Assert.Equal(info.LatestBlockHeight, updated.LatestBlockHeight);
    }
}
