namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Shared base for all CometBFT transaction result representations.
/// Carries the ABCI response fields common to both the raw <see cref="TxResult"/>
/// and the decoded <see cref="TxResult{TTx}"/>, and serves as the extension point
/// for application-layer result types (e.g. a Cosmos result that adds RawLog).
/// </summary>
/// <param name="Hash">The hex-encoded transaction hash.</param>
/// <param name="Height">The block height in which the transaction was included.</param>
/// <param name="Index">The transaction index within the block.</param>
/// <param name="Code">The ABCI response code (0 = success).</param>
/// <param name="Data">The base64-encoded ABCI response data.</param>
/// <param name="Log">The ABCI response log string.</param>
/// <param name="Info">The ABCI response info string.</param>
/// <param name="GasWanted">The amount of gas requested by the transaction.</param>
/// <param name="GasUsed">The amount of gas consumed by the transaction.</param>
/// <param name="Events">The list of ABCI events emitted by the transaction.</param>
/// <param name="Codespace">The namespace for the response code.</param>
public abstract record TxResultBase(
    string Hash,
    long Height,
    int Index,
    uint Code,
    string? Data,
    string? Log,
    string? Info,
    long GasWanted,
    long GasUsed,
    IReadOnlyList<CometBftEvent> Events,
    string? Codespace);

/// <summary>
/// Represents the result of executing a transaction on the CometBFT chain,
/// with the raw transaction bytes preserved as a base64-encoded string.
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
public record TxResult(
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
    IReadOnlyList<CometBftEvent> Events,
    string? Codespace) : TxResultBase(Hash, Height, Index, Code, Data, Log, Info, GasWanted, GasUsed, Events, Codespace);
