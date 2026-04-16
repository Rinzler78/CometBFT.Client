using CometBFT.Client.Core.Codecs;

namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the result of executing a transaction on the CometBFT chain,
/// with the raw transaction bytes decoded into the application-specific type
/// <typeparamref name="TTx"/>.
/// </summary>
/// <typeparam name="TTx">The application-specific transaction type.</typeparam>
/// <param name="Hash">The hex-encoded transaction hash.</param>
/// <param name="Height">The block height in which the transaction was included.</param>
/// <param name="Index">The transaction index within the block.</param>
/// <param name="Transaction">The decoded transaction.</param>
/// <param name="Code">The ABCI response code (0 = success).</param>
/// <param name="Data">The base64-encoded ABCI response data.</param>
/// <param name="Log">The ABCI response log string.</param>
/// <param name="Info">The ABCI response info string.</param>
/// <param name="GasWanted">The amount of gas requested by the transaction.</param>
/// <param name="GasUsed">The amount of gas consumed by the transaction.</param>
/// <param name="Events">The list of ABCI events emitted by the transaction.</param>
/// <param name="Codespace">The namespace for the response code.</param>
/// <remarks>
/// Obtain instances via <see cref="TxResultExtensions.Decode{TTx}(TxResult, ITxCodec{TTx})"/>
/// rather than constructing directly.
/// </remarks>
public sealed record TxResult<TTx>(
    string Hash,
    long Height,
    int Index,
    TTx Transaction,
    uint Code,
    string? Data,
    string? Log,
    string? Info,
    long GasWanted,
    long GasUsed,
    IReadOnlyList<CometBftEvent> Events,
    string? Codespace);

/// <summary>
/// Extension methods for converting a raw <see cref="TxResult"/> into a typed <see cref="TxResult{TTx}"/>.
/// </summary>
public static class TxResultExtensions
{
    /// <summary>
    /// Decodes the raw transaction bytes using <paramref name="codec"/>,
    /// returning a strongly-typed <see cref="TxResult{TTx}"/>.
    /// </summary>
    /// <typeparam name="TTx">The application-specific transaction type.</typeparam>
    /// <param name="result">The raw transaction result containing base64-encoded transaction bytes.</param>
    /// <param name="codec">The codec used to decode the transaction.</param>
    /// <returns>A new <see cref="TxResult{TTx}"/> with the decoded transaction.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result"/> or <paramref name="codec"/> is <c>null</c>.
    /// </exception>
    public static TxResult<TTx> Decode<TTx>(this TxResult result, ITxCodec<TTx> codec)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(codec);

        var transaction = codec.Decode(Convert.FromBase64String(result.TxBytes));

        return new TxResult<TTx>(
            result.Hash,
            result.Height,
            result.Index,
            transaction,
            result.Code,
            result.Data,
            result.Log,
            result.Info,
            result.GasWanted,
            result.GasUsed,
            result.Events,
            result.Codespace);
    }
}
