using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="ConsensusParamsInfo"/> record.
/// </summary>
public sealed class ConsensusParamsInfoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var keyTypes = new List<string> { "ed25519" }.AsReadOnly();
        var info = new ConsensusParamsInfo(
            BlockMaxBytes: 22020096L,
            BlockMaxGas: -1L,
            EvidenceMaxAgeNumBlocks: 100000L,
            EvidenceMaxAgeDuration: "172800000000000",
            ValidatorPubKeyTypes: keyTypes,
            VersionApp: 1L);

        Assert.Equal(22020096L, info.BlockMaxBytes);
        Assert.Equal(-1L, info.BlockMaxGas);
        Assert.Equal(100000L, info.EvidenceMaxAgeNumBlocks);
        Assert.Equal("172800000000000", info.EvidenceMaxAgeDuration);
        Assert.Single(info.ValidatorPubKeyTypes);
        Assert.Equal("ed25519", info.ValidatorPubKeyTypes[0]);
        Assert.Equal(1L, info.VersionApp);
    }

    [Fact]
    public void ValidatorPubKeyTypes_IsReadOnly()
    {
        var info = new ConsensusParamsInfo(0, 0, 0, "", new List<string> { "ed25519" }.AsReadOnly(), 0);
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.Generic.IList<string>)info.ValidatorPubKeyTypes).Add("secp256k1"));
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var types = new List<string> { "ed25519" }.AsReadOnly();
        var a = new ConsensusParamsInfo(100L, -1L, 50000L, "dur", types, 2L);
        var b = new ConsensusParamsInfo(100L, -1L, 50000L, "dur", types, 2L);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentBlockMaxBytes_NotEqual()
    {
        var types = new List<string>().AsReadOnly();
        var a = new ConsensusParamsInfo(100L, -1L, 50000L, "dur", types, 0L);
        var b = new ConsensusParamsInfo(200L, -1L, 50000L, "dur", types, 0L);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_UpdatesVersionApp()
    {
        var info = new ConsensusParamsInfo(0, 0, 0, "", new List<string>().AsReadOnly(), 1L);
        var updated = info with { VersionApp = 2L };

        Assert.Equal(2L, updated.VersionApp);
        Assert.Equal(info.BlockMaxBytes, updated.BlockMaxBytes);
    }
}
