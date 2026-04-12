namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the result of executing a transaction on the CometBFT chain.
/// </summary>
/// <param name="Hash">The hex-encoded transaction hash.</param>
/// <param name="Height">The block height in which the transaction was included.</param>
/// <param name="Index">The transaction index within the block.</param>
/// <param name="TxBytes">The base64-encoded raw transaction bytes.</param>
/// <param name="Code">The ABCI response code (0 = success).</param>
/// <param name="Data">The base64-encoded ABCI response data.</param>
/// <param name="Log">The ABCI response log string.</param>
/// <param name="Info">The ABCI response info string.</param>
/// <param name="GasWanted">The amount of gas requested by the transaction.</param>
/// <param name="GasUsed">The amount of gas consumed by the transaction.</param>
/// <param name="Events">The list of ABCI events emitted by the transaction.</param>
/// <param name="Codespace">The namespace for the response code.</param>
public sealed record TxResult(
    string Hash,
    long Height,
    int Index,
    string TxBytes,
    uint Code,
    string? Data,
    string? Log,
    string? Info,
    long GasWanted,
    long GasUsed,
    IReadOnlyList<TendermintEvent> Events,
    string? Codespace);
