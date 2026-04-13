using System.Text;

namespace CometBFT.Client.Core.Helpers;

/// <summary>
/// Creates raw transaction byte payloads ready to pass to
/// <see cref="Interfaces.ICometBftGrpcClient.BroadcastTxAsync"/> (gRPC) or to the REST
/// broadcast methods that expect a base64-encoded string.
/// </summary>
/// <remarks>
/// CometBFT treats transaction bytes as opaque: they are forwarded unchanged to the ABCI
/// application. The format of those bytes is therefore application-specific.
/// <list type="bullet">
///   <item>
///     <description>
///       For the built-in <b>kvstore</b> ABCI test application the expected format is
///       <c>key=value</c> UTF-8 bytes (see <see cref="FromKeyValue"/>).
///     </description>
///   </item>
///   <item>
///     <description>
///       For Cosmos-SDK applications the bytes must be a serialised <c>TxRaw</c> protobuf
///       message, which is assembled and signed outside this library.
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class TxFactory
{
    /// <summary>
    /// UTF-8 encodes <paramref name="text"/> as raw transaction bytes.
    /// </summary>
    /// <param name="text">The plain-text content to encode.</param>
    /// <returns>The UTF-8 byte representation of <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="text"/> is <c>null</c>.
    /// </exception>
    public static byte[] FromString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>
    /// Encodes a key-value pair as <c>key=value</c> UTF-8 bytes, which is the transaction
    /// format expected by the CometBFT built-in <b>kvstore</b> ABCI application.
    /// </summary>
    /// <param name="key">
    /// The key. Must be non-null, non-empty, and must not contain the <c>=</c> character.
    /// </param>
    /// <param name="value">The value. May be an empty string but must not be <c>null</c>.</param>
    /// <returns>UTF-8 bytes of the string <c>"key=value"</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> or <paramref name="value"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="key"/> is empty or contains the <c>=</c> character.
    /// </exception>
    public static byte[] FromKeyValue(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (key.Length == 0)
            throw new ArgumentException("Key must not be empty.", nameof(key));

        if (key.Contains('=', StringComparison.Ordinal))
            throw new ArgumentException("Key must not contain the '=' character.", nameof(key));

        return Encoding.UTF8.GetBytes($"{key}={value}");
    }

    /// <summary>
    /// Encodes raw transaction bytes as a base64 string suitable for the REST broadcast
    /// methods (e.g. <c>BroadcastTxAsync(string txBytes)</c>).
    /// </summary>
    /// <param name="txBytes">The raw transaction bytes to encode.</param>
    /// <returns>The standard base64 string representation of <paramref name="txBytes"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="txBytes"/> is <c>null</c>.
    /// </exception>
    public static string ToBase64(byte[] txBytes)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        return Convert.ToBase64String(txBytes);
    }

    /// <summary>
    /// Decodes a base64-encoded string back to raw transaction bytes.
    /// </summary>
    /// <param name="base64">The base64 string to decode.</param>
    /// <returns>The decoded byte array.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="base64"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="base64"/> is not a valid base64 string.
    /// </exception>
    public static byte[] FromBase64(string base64)
    {
        ArgumentNullException.ThrowIfNull(base64);
        return Convert.FromBase64String(base64);
    }
}
