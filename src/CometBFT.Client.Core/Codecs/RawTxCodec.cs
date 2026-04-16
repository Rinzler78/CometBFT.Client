namespace CometBFT.Client.Core.Codecs;

/// <summary>
/// Default no-op codec that keeps transactions as base64-encoded strings.
/// Used automatically when no application-specific codec is provided,
/// preserving full backward compatibility with the untyped API.
/// </summary>
public sealed class RawTxCodec : ITxCodec<string>
{
    /// <summary>
    /// Shared singleton instance. Thread-safe — the codec is stateless.
    /// </summary>
    public static readonly RawTxCodec Instance = new();

    private RawTxCodec() { }

    /// <inheritdoc />
    /// <returns>The base64-encoded string representation of <paramref name="bytes"/>.</returns>
    public string Decode(byte[] bytes) => Convert.ToBase64String(bytes);

    /// <inheritdoc />
    /// <returns>The raw bytes decoded from the base64 string <paramref name="tx"/>.</returns>
    public byte[] Encode(string tx) => Convert.FromBase64String(tx);
}
