using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="BlockHeader"/> record.
/// </summary>
public sealed class BlockHeaderTests
{
    private static BlockHeader Make(
        string version = "11",
        string chainId = "testnet",
        long height = 42L,
        DateTimeOffset? time = null,
        string lastBlockId = "PREVHASH",
        string lastCommitHash = "LC",
        string dataHash = "DH",
        string validatorsHash = "VH",
        string nextValidatorsHash = "NVH",
        string consensusHash = "CH",
        string appHash = "AH",
        string lastResultsHash = "LRH",
        string evidenceHash = "EH",
        string proposerAddress = "PROPOSER") =>
        new(version, chainId, height, time ?? DateTimeOffset.UtcNow,
            lastBlockId, lastCommitHash, dataHash, validatorsHash,
            nextValidatorsHash, consensusHash, appHash, lastResultsHash,
            evidenceHash, proposerAddress);

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var time = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var header = Make(height: 7L, time: time, chainId: "cosmoshub-4", proposerAddress: "ADDR");

        Assert.Equal("11", header.Version);
        Assert.Equal("cosmoshub-4", header.ChainId);
        Assert.Equal(7L, header.Height);
        Assert.Equal(time, header.Time);
        Assert.Equal("PREVHASH", header.LastBlockId);
        Assert.Equal("LC", header.LastCommitHash);
        Assert.Equal("DH", header.DataHash);
        Assert.Equal("VH", header.ValidatorsHash);
        Assert.Equal("NVH", header.NextValidatorsHash);
        Assert.Equal("CH", header.ConsensusHash);
        Assert.Equal("AH", header.AppHash);
        Assert.Equal("LRH", header.LastResultsHash);
        Assert.Equal("EH", header.EvidenceHash);
        Assert.Equal("ADDR", header.ProposerAddress);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = Make(height: 1L, time: time);
        var b = Make(height: 1L, time: time);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentHeight_NotEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = Make(height: 1L, time: time);
        var b = Make(height: 2L, time: time);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesProposerAddress()
    {
        var header = Make(proposerAddress: "OLD");
        var updated = header with { ProposerAddress = "NEW" };

        Assert.Equal("NEW", updated.ProposerAddress);
        Assert.Equal(header.Height, updated.Height);
        Assert.Equal(header.ChainId, updated.ChainId);
    }

    [Fact]
    public void ToString_ContainsHeight()
    {
        var header = Make(height: 999L);
        Assert.Contains("999", header.ToString());
    }
}
