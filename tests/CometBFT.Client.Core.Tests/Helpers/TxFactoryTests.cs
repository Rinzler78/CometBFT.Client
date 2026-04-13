using CometBFT.Client.Core.Helpers;
using Xunit;

namespace CometBFT.Client.Core.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="TxFactory"/>.
/// </summary>
public sealed class TxFactoryTests
{
    // ── FromString ────────────────────────────────────────────────────────────

    [Fact]
    public void FromString_ReturnsUtf8Bytes()
    {
        var result = TxFactory.FromString("hello");
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, result);
    }

    [Fact]
    public void FromString_EmptyString_ReturnsEmptyArray()
    {
        var result = TxFactory.FromString(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void FromString_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TxFactory.FromString(null!));
    }

    // ── FromKeyValue ──────────────────────────────────────────────────────────

    [Fact]
    public void FromKeyValue_ReturnsKeyEqualsValueUtf8Bytes()
    {
        var result = TxFactory.FromKeyValue("k", "v");
        Assert.Equal(new byte[] { 107, 61, 118 }, result); // "k=v"
    }

    [Fact]
    public void FromKeyValue_MultiCharKeyAndValue_ReturnsCorrectBytes()
    {
        var result = TxFactory.FromKeyValue("mykey", "myvalue");
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("mykey=myvalue"), result);
    }

    [Fact]
    public void FromKeyValue_EmptyValue_ReturnsKeyEqualsBytes()
    {
        var result = TxFactory.FromKeyValue("k", string.Empty);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("k="), result);
    }

    [Fact]
    public void FromKeyValue_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TxFactory.FromKeyValue(null!, "v"));
    }

    [Fact]
    public void FromKeyValue_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TxFactory.FromKeyValue("k", null!));
    }

    [Fact]
    public void FromKeyValue_EmptyKey_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => TxFactory.FromKeyValue(string.Empty, "v"));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void FromKeyValue_KeyContainsEquals_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => TxFactory.FromKeyValue("k=1", "v"));
        Assert.Equal("key", ex.ParamName);
    }

    // ── ToBase64 / FromBase64 ─────────────────────────────────────────────────

    [Fact]
    public void ToBase64_ReturnsValidBase64String()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03 };
        var result = TxFactory.ToBase64(bytes);
        Assert.Equal("AQID", result);
    }

    [Fact]
    public void FromBase64_DecodesCorrectly()
    {
        var result = TxFactory.FromBase64("AQID");
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, result);
    }

    [Fact]
    public void ToBase64_ThenFromBase64_RoundTrip()
    {
        var original = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0xFF };
        var base64 = TxFactory.ToBase64(original);
        var decoded = TxFactory.FromBase64(base64);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void ToBase64_EmptyArray_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, TxFactory.ToBase64(Array.Empty<byte>()));
    }

    [Fact]
    public void ToBase64_NullBytes_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TxFactory.ToBase64(null!));
    }

    [Fact]
    public void FromBase64_NullString_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TxFactory.FromBase64(null!));
    }

    [Fact]
    public void FromBase64_InvalidBase64_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => TxFactory.FromBase64("not-valid-base64!!!"));
    }
}
