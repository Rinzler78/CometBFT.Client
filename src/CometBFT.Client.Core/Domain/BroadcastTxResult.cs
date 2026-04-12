namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the result of broadcasting a transaction to a CometBFT node.
/// </summary>
/// <param name="Code">The ABCI response code (0 = accepted/success).</param>
/// <param name="Data">The base64-encoded ABCI response data, or <c>null</c> if absent.</param>
/// <param name="Log">The ABCI response log string.</param>
/// <param name="Codespace">The namespace for the response code.</param>
/// <param name="Hash">The hex-encoded transaction hash.</param>
/// <param name="GasWanted">The amount of gas requested by the transaction. Populated from gRPC <c>check_tx</c>; <c>0</c> when not available.</param>
/// <param name="GasUsed">The amount of gas actually consumed. Populated from gRPC <c>check_tx</c>; <c>0</c> when not available.</param>
public sealed record BroadcastTxResult(
    uint Code,
    string? Data,
    string? Log,
    string? Codespace,
    string Hash,
    long GasWanted = 0,
    long GasUsed = 0);
