using System.Collections.ObjectModel;
using CometBFT.Client.Core.Codecs;

namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents a CometBFT block whose transactions have been decoded into
/// the application-specific type <typeparamref name="TTx"/>.
/// </summary>
/// <typeparam name="TTx">The application-specific transaction type.</typeparam>
/// <param name="Height">The block height (chain sequence number).</param>
/// <param name="Hash">The hex-encoded block hash.</param>
/// <param name="Time">The block timestamp in UTC.</param>
/// <param name="Proposer">The base64-encoded proposer address.</param>
/// <param name="Txs">The decoded transactions included in this block.</param>
/// <remarks>
/// Obtain instances via <see cref="BlockExtensions.Decode{TTx}(Block, ITxCodec{TTx})"/>
/// rather than constructing directly. All consensus fields are identical to the
/// raw <see cref="Block"/>; only <see cref="Txs"/> carries application-specific data.
/// </remarks>
public sealed record Block<TTx>(
    long Height,
    string Hash,
    DateTimeOffset Time,
    string Proposer,
    IReadOnlyList<TTx> Txs) where TTx : notnull;

/// <summary>
/// Extension methods for converting a raw <see cref="Block"/> into a typed <see cref="Block{TTx}"/>.
/// </summary>
public static class BlockExtensions
{
    /// <summary>
    /// Decodes each transaction in the block using <paramref name="codec"/>,
    /// returning a strongly-typed <see cref="Block{TTx}"/>.
    /// </summary>
    /// <typeparam name="TTx">The application-specific transaction type.</typeparam>
    /// <param name="block">The raw block containing base64-encoded transaction bytes.</param>
    /// <param name="codec">The codec used to decode each transaction.</param>
    /// <returns>A new <see cref="Block{TTx}"/> with decoded transactions.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="block"/> or <paramref name="codec"/> is <c>null</c>.
    /// </exception>
    public static Block<TTx> Decode<TTx>(this Block block, ITxCodec<TTx> codec) where TTx : notnull
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(codec);

        var list = new List<TTx>(block.Txs.Count);
        foreach (var b64 in block.Txs)
            list.Add(codec.Decode(Convert.FromBase64String(b64)));
        var txs = new ReadOnlyCollection<TTx>(list);

        return new Block<TTx>(block.Height, block.Hash, block.Time, block.Proposer, txs);
    }

    /// <summary>
    /// Fast-path conversion for the default raw codec: wraps the existing base64
    /// transaction strings directly without any decode/re-encode roundtrip.
    /// </summary>
    /// <param name="block">The raw block.</param>
    /// <returns>A <see cref="Block{TTx}"/> of <c>string</c> reusing the original <see cref="Block.Txs"/> list.</returns>
    public static Block<string> DecodeRaw(this Block block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return new Block<string>(block.Height, block.Hash, block.Time, block.Proposer, block.Txs);
    }
}
