namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the mempool state returned by <c>/unconfirmed_txs</c> and <c>/num_unconfirmed_txs</c>.
/// </summary>
/// <param name="Count">The number of transactions returned in the response window.</param>
/// <param name="Total">The total number of unconfirmed transactions in the mempool.</param>
/// <param name="TotalBytes">The total size in bytes of the unconfirmed transactions.</param>
/// <param name="Txs">The base64-encoded transactions returned by the endpoint.</param>
public record UnconfirmedTxsInfo(
    int Count,
    int Total,
    int TotalBytes,
    IReadOnlyList<string> Txs);
