using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides real-time event subscription over WebSocket to a CometBFT node,
/// with transactions decoded into the application-specific type <typeparamref name="TTx"/>.
/// </summary>
/// <typeparam name="TTx">
/// The application-specific transaction type.
/// Use <see cref="ICometBftWebSocketClient"/> (non-generic) to receive raw base64 strings.
/// </typeparam>
public interface ICometBftWebSocketClient<TTx> : IAsyncDisposable
{
    /// <summary>
    /// Raised when a new block is committed to the chain.
    /// Transactions are decoded into <typeparamref name="TTx"/> via the configured codec.
    /// </summary>
    event EventHandler<CometBftEventArgs<Block<TTx>>>? NewBlockReceived;

    /// <summary>
    /// Raised when a new block header is received (tm.event='NewBlockHeader').
    /// Fires before the full block data is available and carries only the header.
    /// </summary>
    event EventHandler<CometBftEventArgs<BlockHeader>>? NewBlockHeaderReceived;

    /// <summary>
    /// Raised when a transaction has been executed in a block.
    /// The transaction bytes are decoded into <typeparamref name="TTx"/> via the configured codec.
    /// </summary>
    event EventHandler<CometBftEventArgs<TxResult<TTx>>>? TxExecuted;

    /// <summary>
    /// Raised when a vote is received during consensus.
    /// </summary>
    event EventHandler<CometBftEventArgs<Vote>>? VoteReceived;

    /// <summary>
    /// Raised when the validator set is updated.
    /// </summary>
    event EventHandler<CometBftEventArgs<IReadOnlyList<Validator>>>? ValidatorSetUpdated;

    /// <summary>
    /// Raised when an error occurs while processing a received WebSocket message.
    /// The subscription loop is kept alive — this event is purely informational.
    /// </summary>
    event EventHandler<CometBftEventArgs<Exception>>? ErrorOccurred;

    /// <summary>
    /// Connects to the WebSocket endpoint and begins receiving messages.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the WebSocket endpoint.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to new block events (tm.event='NewBlock').
    /// </summary>
    Task SubscribeNewBlockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to new block header events (tm.event='NewBlockHeader').
    /// </summary>
    Task SubscribeNewBlockHeaderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to transaction events (tm.event='Tx').
    /// </summary>
    Task SubscribeTxAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to vote events (tm.event='Vote').
    /// </summary>
    Task SubscribeVoteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to validator set update events (tm.event='ValidatorSetUpdates').
    /// </summary>
    Task SubscribeValidatorSetUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all active event subscriptions.
    /// </summary>
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides real-time event subscription over WebSocket to a CometBFT node.
/// Transactions and blocks are surfaced as raw base64-encoded strings.
/// </summary>
/// <remarks>
/// This is the default, backward-compatible interface equivalent to
/// <see cref="ICometBftWebSocketClient{TTx}"/> with <c>TTx = string</c>.
/// Use <see cref="ICometBftWebSocketClient{TTx}"/> with an application-specific
/// <c>ITxCodec&lt;TTx&gt;</c> to receive decoded, strongly-typed transactions.
/// </remarks>
public interface ICometBftWebSocketClient : ICometBftWebSocketClient<string> { }
