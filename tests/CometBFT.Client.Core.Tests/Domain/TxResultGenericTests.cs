using CometBFT.Client.Core.Codecs;
using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="TxResult{TTx}"/> and <see cref="TxResultExtensions"/>.
/// </summary>
public sealed class TxResultGenericTests
{
    // Codec stub: decodes bytes to their length
    private sealed class LengthCodec : ITxCodec<int>
    {
        public int Decode(byte[] bytes) => bytes.Length;
        public byte[] Encode(int tx) => new byte[tx];
    }

    private static TxResult MakeRawTxResult(string? b64TxBytes = null)
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var txBytes = b64TxBytes ?? Convert.ToBase64String(bytes);
        return new TxResult(
            "HASH", 10L, 0, txBytes, 0u,
            "DATA", "LOG", "INFO", 100L, 80L,
            new List<CometBftEvent>().AsReadOnly(), null);
    }

    // ── TxResult<TTx> record ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var events = new List<CometBftEvent>().AsReadOnly();
        var tx = new TxResult<string>(
            "HASH", 5L, 1, "decoded-tx",
            0u, "DATA", "LOG", "INFO", 200L, 150L, events, "sdk");

        Assert.Equal("HASH", tx.Hash);
        Assert.Equal(5L, tx.Height);
        Assert.Equal(1, tx.Index);
        Assert.Equal("decoded-tx", tx.Transaction);
        Assert.Equal(0u, tx.Code);
        Assert.Equal("DATA", tx.Data);
        Assert.Equal("LOG", tx.Log);
        Assert.Equal("INFO", tx.Info);
        Assert.Equal(200L, tx.GasWanted);
        Assert.Equal(150L, tx.GasUsed);
        Assert.Equal("sdk", tx.Codespace);
    }

    // ── TxResultExtensions.Decode ────────────────────────────────────────────

    [Fact]
    public void Decode_PreservesConsensusFields()
    {
        var raw = MakeRawTxResult();

        var typed = raw.Decode(RawTxCodec.Instance);

        Assert.Equal(raw.Hash, typed.Hash);
        Assert.Equal(raw.Height, typed.Height);
        Assert.Equal(raw.Index, typed.Index);
        Assert.Equal(raw.Code, typed.Code);
        Assert.Equal(raw.Data, typed.Data);
        Assert.Equal(raw.Log, typed.Log);
        Assert.Equal(raw.Info, typed.Info);
        Assert.Equal(raw.GasWanted, typed.GasWanted);
        Assert.Equal(raw.GasUsed, typed.GasUsed);
        Assert.Equal(raw.Codespace, typed.Codespace);
        Assert.Same(raw.Events, typed.Events);
    }

    [Fact]
    public void Decode_WithRawCodec_TransactionIsBase64()
    {
        var bytes = new byte[] { 10, 20, 30 };
        var b64 = Convert.ToBase64String(bytes);
        var raw = MakeRawTxResult(b64);

        var typed = raw.Decode(RawTxCodec.Instance);

        Assert.Equal(b64, typed.Transaction);
    }

    [Fact]
    public void Decode_WithCustomCodec_TransactionIsDecoded()
    {
        var bytes = new byte[7];
        var b64 = Convert.ToBase64String(bytes);
        var raw = MakeRawTxResult(b64);

        var typed = raw.Decode(new LengthCodec());

        Assert.Equal(7, typed.Transaction);
    }

    [Fact]
    public void Decode_NullResult_ThrowsArgumentNullException()
    {
        TxResult raw = null!;
        Assert.Throws<ArgumentNullException>(() => raw.Decode(RawTxCodec.Instance));
    }

    [Fact]
    public void Decode_NullCodec_ThrowsArgumentNullException()
    {
        var raw = MakeRawTxResult();
        Assert.Throws<ArgumentNullException>(() => raw.Decode<string>(null!));
    }

    // ── TxResultExtensions.DecodeRaw ─────────────────────────────────────────

    [Fact]
    public void DecodeRaw_ReusesTxBytesAsTransaction()
    {
        var raw = MakeRawTxResult();
        var typed = raw.DecodeRaw();
        Assert.Same(raw.TxBytes, typed.Transaction);
    }

    [Fact]
    public void DecodeRaw_PreservesConsensusFields()
    {
        var raw = MakeRawTxResult();
        var typed = raw.DecodeRaw();
        Assert.Equal(raw.Hash, typed.Hash);
        Assert.Equal(raw.Height, typed.Height);
        Assert.Equal(raw.Index, typed.Index);
        Assert.Equal(raw.Code, typed.Code);
        Assert.Equal(raw.Data, typed.Data);
        Assert.Equal(raw.Log, typed.Log);
        Assert.Equal(raw.Info, typed.Info);
        Assert.Equal(raw.GasWanted, typed.GasWanted);
        Assert.Equal(raw.GasUsed, typed.GasUsed);
        Assert.Same(raw.Events, typed.Events);
    }

    [Fact]
    public void DecodeRaw_NullResult_ThrowsArgumentNullException()
    {
        TxResult raw = null!;
        Assert.Throws<ArgumentNullException>(() => raw.DecodeRaw());
    }
}
