using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides real-time event subscription over WebSocket to a CometBFT node.
/// </summary>
public interface ICometBftWebSocketClient : IAsyncDisposable
{
    /// <summary>
    /// Raised when a new block is committed to the chain.
    /// </summary>
    event EventHandler<TendermintEventArgs<Block>>? NewBlockReceived;

    /// <summary>
    /// Raised when a new block header is received (tm.event='NewBlockHeader').
    /// Fires before the full block data is available and carries only the header.
    /// </summary>
    event EventHandler<TendermintEventArgs<BlockHeader>>? NewBlockHeaderReceived;

    /// <summary>
    /// Raised when a transaction has been executed in a block.
    /// </summary>
    event EventHandler<TendermintEventArgs<TxResult>>? TxExecuted;

    /// <summary>
    /// Raised when a vote is received during consensus.
    /// </summary>
    event EventHandler<TendermintEventArgs<Vote>>? VoteReceived;

    /// <summary>
    /// Raised when the validator set is updated.
    /// </summary>
    event EventHandler<TendermintEventArgs<IReadOnlyList<Validator>>>? ValidatorSetUpdated;

    /// <summary>
    /// Connects to the WebSocket endpoint and begins receiving messages.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the WebSocket endpoint.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to new block events (tm.event='NewBlock').
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscribeNewBlockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to new block header events (tm.event='NewBlockHeader').
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscribeNewBlockHeaderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to transaction events (tm.event='Tx').
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscribeTxAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to vote events (tm.event='Vote').
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscribeVoteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to validator set update events (tm.event='ValidatorSetUpdates').
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscribeValidatorSetUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all active event subscriptions.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);
}
