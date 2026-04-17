namespace CometBFT.Client.Core.Codecs;

/// <summary>
/// Encodes and decodes transactions between their raw byte representation
/// and a strongly-typed model specific to a blockchain application.
/// </summary>
/// <typeparam name="TTx">The application-specific transaction type.</typeparam>
/// <remarks>
/// Implement this interface once per blockchain (e.g. Cosmos SDK, Osmosis)
/// and pass the codec to the typed WebSocket client or broadcast extension methods
/// to get strongly-typed events and results.
/// The library provides <see cref="RawTxCodec"/> as the default no-op implementation
/// that keeps transactions as base64-encoded strings.
/// <para>
/// Implementations must be thread-safe. <see cref="Decode"/> may be called
/// concurrently from multiple threads when WebSocket messages arrive in rapid succession.
/// Stateless implementations (like <see cref="RawTxCodec"/>) are inherently thread-safe.
/// </para>
/// </remarks>
public interface ITxCodec<TTx> where TTx : notnull
{
    /// <summary>
    /// Decodes raw transaction bytes into the application-specific type.
    /// </summary>
    /// <param name="bytes">The raw transaction bytes as received from the CometBFT node.</param>
    /// <returns>The decoded transaction.</returns>
    TTx Decode(byte[] bytes);

    /// <summary>
    /// Encodes an application-specific transaction into raw bytes for broadcasting.
    /// </summary>
    /// <param name="tx">The transaction to encode.</param>
    /// <returns>The raw transaction bytes to send to the CometBFT node.</returns>
    byte[] Encode(TTx tx);
}
