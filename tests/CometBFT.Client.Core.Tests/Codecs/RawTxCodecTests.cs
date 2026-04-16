using CometBFT.Client.Core.Codecs;
using Xunit;

namespace CometBFT.Client.Core.Tests.Codecs;

/// <summary>
/// Unit tests for <see cref="RawTxCodec"/>.
/// </summary>
public sealed class RawTxCodecTests
{
    private readonly RawTxCodec _codec = RawTxCodec.Instance;

    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(RawTxCodec.Instance, RawTxCodec.Instance);
    }

    [Fact]
    public void Decode_ReturnsBase64String()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var result = _codec.Decode(bytes);
        Assert.Equal(Convert.ToBase64String(bytes), result);
    }

    [Fact]
    public void Encode_ReturnsOriginalBytes()
    {
        var bytes = new byte[] { 10, 20, 30 };
        var b64 = Convert.ToBase64String(bytes);
        Assert.Equal(bytes, _codec.Encode(b64));
    }

    [Fact]
    public void RoundTrip_BytesToBase64ToBytes()
    {
        var original = new byte[] { 0xFF, 0x00, 0xAB };
        var decoded = _codec.Decode(original);
        var reEncoded = _codec.Encode(decoded);
        Assert.Equal(original, reEncoded);
    }

    [Fact]
    public void Decode_EmptyArray_ReturnsEmptyBase64()
    {
        var result = _codec.Decode([]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ImplementsITxCodecString()
    {
        Assert.IsAssignableFrom<ITxCodec<string>>(_codec);
    }
}
