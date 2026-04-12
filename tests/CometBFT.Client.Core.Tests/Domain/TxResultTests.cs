using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for the <see cref="TxResult"/> record.
/// </summary>
public sealed class TxResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var events = new List<CometBftEvent>().AsReadOnly();
        var tx = new TxResult("HASH", 10L, 0, "TXBYTES", 0u, "DATA", "LOG", "INFO", 100L, 90L, events, null);

        Assert.Equal("HASH", tx.Hash);
        Assert.Equal(10L, tx.Height);
        Assert.Equal(0, tx.Index);
        Assert.Equal(0u, tx.Code);
        Assert.Equal("DATA", tx.Data);
        Assert.Equal("LOG", tx.Log);
        Assert.Equal(100L, tx.GasWanted);
        Assert.Equal(90L, tx.GasUsed);
        Assert.Null(tx.Codespace);
    }

    [Fact]
    public void Code_Zero_IsSuccess()
    {
        var tx = new TxResult("H", 1L, 0, "B", 0u, null, null, null, 0, 0, new List<CometBftEvent>().AsReadOnly(), null);
        Assert.Equal(0u, tx.Code);
    }

    [Fact]
    public void Code_NonZero_IsError()
    {
        var tx = new TxResult("H", 1L, 0, "B", 1u, null, "error", null, 0, 0, new List<CometBftEvent>().AsReadOnly(), "sdk");
        Assert.NotEqual(0u, tx.Code);
        Assert.Equal("sdk", tx.Codespace);
    }
}
