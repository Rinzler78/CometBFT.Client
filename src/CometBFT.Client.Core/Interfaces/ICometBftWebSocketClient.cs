using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;

namespace CometBFT.Client.Core.Interfaces;

/// <summary>
/// Provides real-time event subscription over WebSocket to a CometBFT node,
/// parameterized over the transaction, block, transaction result, and validator types.
/// </summary>
/// <typeparam name="TTx">The application-specific transaction type.</typeparam>
/// <typeparam name="TBlock">
/// The block type. Must inherit <see cref="Block{TTx}"/> so that <c>Txs</c> is typed as
/// <see cref="IReadOnlyList{T}"/> of <typeparamref name="TTx"/>.
/// </typeparam>
/// <typeparam name="TTxResult">
/// The transaction result type. Must inherit <see cref="TxResult{TTx}"/> so that
/// <c>Transaction</c> is typed as <typeparamref name="TTx"/>.
/// </typeparam>
/// <typeparam name="TValidator">The validator type. Must inherit <see cref="Validator"/>.</typeparam>
/// <remarks>
/// Use <see cref="ICometBftWebSocketClient{TTx}"/> (1-parameter generic shim) or
/// <see cref="ICometBftWebSocketClient"/> (non-generic) for the common cases.
/// </remarks>
public interface ICometBftWebSocketClient<TTx, TBlock, TTxResult, TValidator> : IAsyncDisposable
    where TTx : notnull
    where TBlock : Block<TTx>
    where TTxResult : TxResult<TTx>
    where TValidator : Validator
{
    /// <summary>
    /// Raised when a new block is committed to the chain.
    /// Transactions are decoded into <typeparamref name="TTx"/> via the configured codec.
    /// </summary>
    event EventHandler<CometBftEventArgs<TBlock>>? NewBlockReceived;

    /// <summary>
    /// Raised when a new block header is received (tm.event='NewBlockHeader').
    /// Fires before the full block data is available and carries only the header.
    /// </summary>
    event EventHandler<CometBftEventArgs<BlockHeader>>? NewBlockHeaderReceived;

    /// <summary>
    /// Raised when a transaction has been executed in a block.
    /// The transaction bytes are decoded into <typeparamref name="TTx"/> via the configured codec.
    /// </summary>
    event EventHandler<CometBftEventArgs<TTxResult>>? TxExecuted;

    /// <summary>
    /// Raised when a vote is received during consensus.
    /// </summary>
    event EventHandler<CometBftEventArgs<Vote>>? VoteReceived;

    /// <summary>
    /// Raised when the validator set is updated.
    /// </summary>
    event EventHandler<CometBftEventArgs<IReadOnlyList<TValidator>>>? ValidatorSetUpdated;

    /// <summary>
    /// Raised when an error occurs while processing a received WebSocket message.
    /// The subscription loop is kept alive — this event is purely informational.
    /// </summary>
    event EventHandler<CometBftEventArgs<Exception>>? ErrorOccurred;

    /// <summary>
    /// Raised when the underlying WebSocket connection drops. A reconnection attempt
    /// is already in progress when this fires. Active subscriptions will be replayed
    /// automatically once the connection is restored.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Raised after a successful reconnection, once all active subscriptions have
    /// been replayed to the server. Does not fire for the initial connection.
    /// </summary>
    event EventHandler? Reconnected;

    /// <summary>
    /// Emits on every committed block with the full ABCI event list
    /// (tm.event='NewBlockEvents'). Primary source for on-chain activity indexing
    /// without per-block REST polling.
    /// </summary>
    IObservable<NewBlockEventsData> NewBlockEventsStream { get; }

    /// <summary>
    /// Emits when a consensus complete-proposal step occurs
    /// (tm.event='CompleteProposal').
    /// </summary>
    IObservable<CompleteProposalData> CompleteProposalStream { get; }

    /// <summary>
    /// Emits when the validator set changes (tm.event='ValidatorSetUpdates').
    /// </summary>
    IObservable<ValidatorSetUpdatesData> ValidatorSetUpdatesStream { get; }

    /// <summary>
    /// Emits when new evidence is submitted (tm.event='NewEvidence').
    /// </summary>
    IObservable<NewEvidenceData> NewEvidenceStream { get; }

    /// <summary>
    /// Merged stream for the nine low-priority consensus-internal events:
    /// TimeoutPropose, TimeoutWait, Lock, Unlock, Relock,
    /// PolkaAny, PolkaNil, PolkaAgain, MissingProposalBlock.
    /// Each item carries the topic name as <see cref="CometBftEvent.Type"/>.
    /// </summary>
    IObservable<CometBftEvent> ConsensusInternalStream { get; }

    /// <summary>
    /// Connects to the WebSocket endpoint and begins receiving messages.
    /// </summary>
    /// <remarks>
    /// <b>Subscribe concurrently for best relay performance.</b>
    /// Relays batch-flush ACKs only when multiple subscribe frames arrive simultaneously;
    /// serial awaits stall each ACK for 30–45 s per topic. Use <c>Task.WhenAll</c>:
    /// <code>
    /// await ws.ConnectAsync();
    /// await Task.WhenAll(
    ///     ws.SubscribeNewBlockAsync(),
    ///     ws.SubscribeNewBlockEventsAsync());
    /// </code>
    /// <b>Rate limit:</b> CometBFT's default <c>max_subscriptions_per_client = 5</c>.
    /// Exceeding this limit causes the relay to reject the excess subscribe with a JSON-RPC
    /// error. The returned <see cref="Task"/> still completes successfully; the rejection is
    /// delivered via <see cref="ErrorOccurred"/>. Attach an <c>ErrorOccurred</c> handler
    /// before calling any Subscribe method to observe relay-side rejections.
    /// All <c>*Stream</c> properties are initialized at construction time and safe to
    /// subscribe before this method is called.
    /// </remarks>
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
    /// Subscribes to new block events including all ABCI events
    /// (tm.event='NewBlockEvents').
    /// </summary>
    Task SubscribeNewBlockEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to consensus complete-proposal events
    /// (tm.event='CompleteProposal').
    /// </summary>
    Task SubscribeCompleteProposalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to new evidence events (tm.event='NewEvidence').
    /// </summary>
    Task SubscribeNewEvidenceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to all nine low-priority consensus-internal events
    /// (TimeoutPropose, TimeoutWait, Lock, Unlock, Relock,
    /// PolkaAny, PolkaNil, PolkaAgain, MissingProposalBlock).
    /// All items are routed to <see cref="ConsensusInternalStream"/>.
    /// </summary>
    Task SubscribeConsensusInternalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all active event subscriptions.
    /// </summary>
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides real-time event subscription over WebSocket to a CometBFT node,
/// with transactions decoded into the application-specific type <typeparamref name="TTx"/>.
/// Uses the default <see cref="Block{TTx}"/>, <see cref="TxResult{TTx}"/>, and <see cref="Validator"/> types.
/// </summary>
/// <typeparam name="TTx">
/// The application-specific transaction type.
/// Use <see cref="ICometBftWebSocketClient"/> (non-generic) to receive raw base64 strings.
/// </typeparam>
public interface ICometBftWebSocketClient<TTx>
    : ICometBftWebSocketClient<TTx, Block<TTx>, TxResult<TTx>, Validator>
    where TTx : notnull
{ }

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
