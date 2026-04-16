using CometBFT.Client.Core.Codecs;
using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="Block{TTx}"/> and <see cref="BlockExtensions"/>.
/// </summary>
public sealed class BlockGenericTests
{
    // Codec stub: decodes bytes to their length as a string (for easy assertion)
    private sealed class LengthCodec : ITxCodec<int>
    {
        public int Decode(byte[] bytes) => bytes.Length;
        public byte[] Encode(int tx) => new byte[tx];
    }

    private static Block MakeRawBlock(params string[] base64Txs)
    {
        var txs = base64Txs.ToList().AsReadOnly();
        return new Block(42L, "HASH", DateTimeOffset.UtcNow, "PROPOSER", txs);
    }

    // ── Block<TTx> record ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var txs = new List<int> { 1, 2 }.AsReadOnly();
        var time = DateTimeOffset.UtcNow;

        var block = new Block<int>(10L, "H", time, "P", txs);

        Assert.Equal(10L, block.Height);
        Assert.Equal("H", block.Hash);
        Assert.Equal(time, block.Time);
        Assert.Equal("P", block.Proposer);
        Assert.Equal(2, block.Txs.Count);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var txs = new List<string>().AsReadOnly();

        var a = new Block<string>(1L, "H", time, "P", txs);
        var b = new Block<string>(1L, "H", time, "P", txs);

        Assert.Equal(a, b);
    }

    // ── BlockExtensions.Decode ───────────────────────────────────────────────

    [Fact]
    public void Decode_PreservesConsensusFields()
    {
        var time = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var raw = new Block(99L, "HASH", time, "PROPOSER", new List<string>().AsReadOnly());

        var typed = raw.Decode(RawTxCodec.Instance);

        Assert.Equal(99L, typed.Height);
        Assert.Equal("HASH", typed.Hash);
        Assert.Equal(time, typed.Time);
        Assert.Equal("PROPOSER", typed.Proposer);
    }

    [Fact]
    public void Decode_WithRawCodec_TxsStayAsBase64()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var b64 = Convert.ToBase64String(bytes);
        var raw = MakeRawBlock(b64);

        var typed = raw.Decode(RawTxCodec.Instance);

        Assert.Single(typed.Txs);
        Assert.Equal(b64, typed.Txs[0]);
    }

    [Fact]
    public void Decode_WithCustomCodec_TxsAreDecoded()
    {
        // Encode a 3-byte and a 5-byte payload as base64
        var b64_3 = Convert.ToBase64String(new byte[3]);
        var b64_5 = Convert.ToBase64String(new byte[5]);
        var raw = MakeRawBlock(b64_3, b64_5);

        var typed = raw.Decode(new LengthCodec());

        Assert.Equal(2, typed.Txs.Count);
        Assert.Equal(3, typed.Txs[0]);
        Assert.Equal(5, typed.Txs[1]);
    }

    [Fact]
    public void Decode_EmptyTxList_ReturnsBlockWithNoTxs()
    {
        var raw = MakeRawBlock();

        var typed = raw.Decode(RawTxCodec.Instance);

        Assert.Empty(typed.Txs);
    }

    [Fact]
    public void Decode_NullBlock_ThrowsArgumentNullException()
    {
        Block raw = null!;
        Assert.Throws<ArgumentNullException>(() => raw.Decode(RawTxCodec.Instance));
    }

    [Fact]
    public void Decode_NullCodec_ThrowsArgumentNullException()
    {
        var raw = MakeRawBlock();
        Assert.Throws<ArgumentNullException>(() => raw.Decode<string>(null!));
    }
}
