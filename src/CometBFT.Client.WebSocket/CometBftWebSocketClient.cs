using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using CometBFT.Client.Core.Codecs;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket.Internal;
using CometBFT.Client.WebSocket.Json;
using Microsoft.Extensions.Options;
using Websocket.Client;

namespace CometBFT.Client.WebSocket;

/// <summary>
/// WebSocket-based subscription client for CometBFT events.
/// Transactions and blocks are decoded into <typeparamref name="TTx"/>
/// using the provided <see cref="ITxCodec{TTx}"/>.
/// Uses <see cref="WebsocketClient"/> with automatic reconnection.
/// </summary>
/// <typeparam name="TTx">The application-specific transaction type.</typeparam>
public class CometBftWebSocketClient<TTx> : ICometBftWebSocketClient<TTx> where TTx : notnull
{
    private readonly CometBftWebSocketOptions _options;
    private readonly IWebSocketClientFactory _factory;
    private readonly ITxCodec<TTx> _codec;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _activeSubscriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// Pending subscribe acknowledgments keyed by JSON-RPC request id.
    /// Completed by <see cref="OnMessageReceived"/> when the server ack arrives.
    /// Internal for testability.
    /// </summary>
    internal readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _pendingAcks = new();

    private readonly Subject<NewBlockEventsData> _newBlockEventsSubject = new();
    private readonly Subject<CompleteProposalData> _completeProposalSubject = new();
    private readonly Subject<ValidatorSetUpdatesData> _validatorSetUpdatesSubject = new();
    private readonly Subject<NewEvidenceData> _newEvidenceSubject = new();
    private readonly Subject<CometBftEvent> _consensusInternalSubject = new();

    private WebsocketClient? _client;
    private IDisposable? _messageSubscription;
    private IDisposable? _reconnectionSubscription;
    private int _requestId;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<Block<TTx>>>? NewBlockReceived;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<BlockHeader>>? NewBlockHeaderReceived;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<TxResult<TTx>>>? TxExecuted;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<Vote>>? VoteReceived;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<IReadOnlyList<Validator>>>? ValidatorSetUpdated;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<Exception>>? ErrorOccurred;

    /// <inheritdoc />
    public IObservable<NewBlockEventsData> NewBlockEventsStream => _newBlockEventsSubject.AsObservable();

    /// <inheritdoc />
    public IObservable<CompleteProposalData> CompleteProposalStream => _completeProposalSubject.AsObservable();

    /// <inheritdoc />
    public IObservable<ValidatorSetUpdatesData> ValidatorSetUpdatesStream => _validatorSetUpdatesSubject.AsObservable();

    /// <inheritdoc />
    public IObservable<NewEvidenceData> NewEvidenceStream => _newEvidenceSubject.AsObservable();

    /// <inheritdoc />
    public IObservable<CometBftEvent> ConsensusInternalStream => _consensusInternalSubject.AsObservable();

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketClient{TTx}"/>.
    /// </summary>
    /// <param name="options">The WebSocket configuration options.</param>
    /// <param name="codec">The codec used to decode transaction bytes into <typeparamref name="TTx"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="codec"/> is <c>null</c>.
    /// </exception>
    public CometBftWebSocketClient(IOptions<CometBftWebSocketOptions> options, ITxCodec<TTx> codec)
        : this(options, new DefaultWebSocketClientFactory(), codec)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketClient{TTx}"/> with a custom WebSocket client factory.
    /// </summary>
    /// <param name="options">The WebSocket configuration options.</param>
    /// <param name="factory">The factory used to create the underlying WebSocket client.</param>
    /// <param name="codec">The codec used to decode transaction bytes into <typeparamref name="TTx"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/>, <paramref name="factory"/>, or <paramref name="codec"/> is <c>null</c>.
    /// </exception>
    internal CometBftWebSocketClient(
        IOptions<CometBftWebSocketOptions> options,
        IWebSocketClientFactory factory,
        ITxCodec<TTx> codec)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is not null)
            {
                return;
            }

            Uri uri;
            try
            {
                uri = new Uri(_options.BaseUrl);
            }
            catch (UriFormatException ex)
            {
                throw new CometBftWebSocketException($"Invalid WebSocket URL: {_options.BaseUrl}", ex);
            }

            WebsocketClient? newClient = null;
            IDisposable? newMessageSub = null;
            IDisposable? newReconnectSub = null;
            try
            {
                newClient = _factory.Create(uri, _options);

                newMessageSub = newClient.MessageReceived.Subscribe(OnMessageReceived);
                newReconnectSub = newClient.ReconnectionHappened.Subscribe(OnReconnected);

                await newClient.StartOrFail().ConfigureAwait(false);

                _client = newClient;
                _messageSubscription = newMessageSub;
                _reconnectionSubscription = newReconnectSub;
            }
            catch (Exception ex) when (ex is not CometBftWebSocketException)
            {
                newReconnectSub?.Dispose();
                newMessageSub?.Dispose();
                newClient?.Dispose();
                throw new CometBftWebSocketException("Failed to connect to CometBFT WebSocket endpoint.", ex);
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is null)
            {
                return;
            }

            await _client.Stop(
                System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                "Client disconnecting").ConfigureAwait(false);

            _reconnectionSubscription?.Dispose();
            _reconnectionSubscription = null;
            _messageSubscription?.Dispose();
            _messageSubscription = null;
            _client.Dispose();
            _client = null;
            _activeSubscriptions.Clear();
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc />
    public Task SubscribeNewBlockAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='NewBlock'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeNewBlockHeaderAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='NewBlockHeader'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeTxAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='Tx'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeVoteAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='Vote'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeValidatorSetUpdatesAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='ValidatorSetUpdates'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeNewBlockEventsAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='NewBlockEvents'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeCompleteProposalAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='CompleteProposal'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeNewEvidenceAsync(CancellationToken cancellationToken = default) =>
        SendSubscribeAsync("tm.event='NewEvidence'", cancellationToken);

    /// <inheritdoc />
    public Task SubscribeConsensusInternalAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            SendSubscribeAsync("tm.event='TimeoutPropose'", cancellationToken),
            SendSubscribeAsync("tm.event='TimeoutWait'", cancellationToken),
            SendSubscribeAsync("tm.event='Lock'", cancellationToken),
            SendSubscribeAsync("tm.event='Unlock'", cancellationToken),
            SendSubscribeAsync("tm.event='Relock'", cancellationToken),
            SendSubscribeAsync("tm.event='PolkaAny'", cancellationToken),
            SendSubscribeAsync("tm.event='PolkaNil'", cancellationToken),
            SendSubscribeAsync("tm.event='PolkaAgain'", cancellationToken),
            SendSubscribeAsync("tm.event='MissingProposalBlock'", cancellationToken));

    /// <inheritdoc />
    public Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var id = Interlocked.Increment(ref _requestId);
        var request = new WsUnsubscribeAllRequest { Id = id };
        _client!.Send(JsonSerializer.Serialize(request, CometBftWebSocketJsonContext.Default.WsUnsubscribeAllRequest));
        _activeSubscriptions.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);

        foreach (var (_, tcs) in _pendingAcks)
        {
            tcs.TrySetCanceled();
        }

        _pendingAcks.Clear();
        _reconnectionSubscription?.Dispose();
        _messageSubscription?.Dispose();

        _newBlockEventsSubject.OnCompleted();
        _newBlockEventsSubject.Dispose();
        _completeProposalSubject.OnCompleted();
        _completeProposalSubject.Dispose();
        _validatorSetUpdatesSubject.OnCompleted();
        _validatorSetUpdatesSubject.Dispose();
        _newEvidenceSubject.OnCompleted();
        _newEvidenceSubject.Dispose();
        _consensusInternalSubject.OnCompleted();
        _consensusInternalSubject.Dispose();

        if (_client is not null)
        {
            try
            {
                await _client.Stop(
                    System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                    "Disposing")
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch
            {
                // Best-effort close — timeout or transport error, continue disposal
            }

            _client.Dispose();
            _client = null;
        }

        _connectLock.Dispose();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task SendSubscribeAsync(string query, CancellationToken cancellationToken)
    {
        EnsureConnected();
        _activeSubscriptions.TryAdd(query, 0);

        var id = Interlocked.Increment(ref _requestId);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingAcks[id] = tcs;

        var request = new WsSubscribeRequest { Id = id, Params = new WsSubscribeParams { Query = query } };
        _client!.Send(JsonSerializer.Serialize(request, CometBftWebSocketJsonContext.Default.WsSubscribeRequest));

        using var ackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ackCts.CancelAfter(_options.SubscribeAckTimeout);
        try
        {
            await tcs.Task.WaitAsync(ackCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingAcks.TryRemove(id, out _);
            ErrorOccurred?.Invoke(this, new CometBftEventArgs<Exception>(
                new CometBftWebSocketException(
                    $"Subscribe ACK for query '{query}' not received within {_options.SubscribeAckTimeout.TotalSeconds:F0} s — subscription was sent and may still be active.")));
        }
    }

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_client is null || !_client.IsRunning)
        {
            throw new CometBftWebSocketException(
                "WebSocket client is not connected. Call ConnectAsync first.");
        }
    }

    internal void OnReconnected(ReconnectionInfo _)
    {
        foreach (var query in _activeSubscriptions.Keys)
        {
            var id = Interlocked.Increment(ref _requestId);
            var request = new WsSubscribeRequest { Id = id, Params = new WsSubscribeParams { Query = query } };
            _client?.Send(JsonSerializer.Serialize(request, CometBftWebSocketJsonContext.Default.WsSubscribeRequest));
        }
    }

    internal void OnMessageReceived(ResponseMessage message)
    {
        if (message.MessageType != System.Net.WebSockets.WebSocketMessageType.Text
            || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize(message.Text, CometBftWebSocketJsonContext.Default.WsEnvelope);
            if (envelope is null)
            {
                return;
            }

            if (envelope.Id > 0 && _pendingAcks.TryRemove(envelope.Id, out var ackTcs))
            {
                ackTcs.TrySetResult(true);
                if (envelope.Result?.Data is null)
                {
                    return;
                }
            }

            switch (envelope.Result?.Data)
            {
                case WsNewBlockData newBlockData:
                    var rawBlock = WebSocketMessageParser.ParseNewBlock(newBlockData);
                    if (rawBlock is not null)
                    {
                        Block<TTx> decodedBlock;
                        try
                        {
                            decodedBlock = _codec is RawTxCodec
                                ? (Block<TTx>)(object)rawBlock.DecodeRaw()
                                : rawBlock.Decode(_codec);
                        }
                        catch (Exception decodeEx)
                        {
                            ErrorOccurred?.Invoke(this,
                                new CometBftEventArgs<Exception>(
                                    new InvalidOperationException(
                                        $"Failed to decode block at height {rawBlock.Height}.", decodeEx)));
                            break;
                        }

                        NewBlockReceived?.Invoke(this, new CometBftEventArgs<Block<TTx>>(decodedBlock));
                    }

                    break;

                case WsNewBlockHeaderData headerData:
                    var blockHeader = WebSocketMessageParser.ParseNewBlockHeader(headerData);
                    if (blockHeader is not null)
                    {
                        NewBlockHeaderReceived?.Invoke(this, new CometBftEventArgs<BlockHeader>(blockHeader));
                    }

                    break;

                case WsTxData txData:
                    var rawTx = WebSocketMessageParser.ParseTxResult(txData, envelope.Result.Events);
                    if (rawTx is not null)
                    {
                        TxResult<TTx> decodedTx;
                        try
                        {
                            decodedTx = _codec is RawTxCodec
                                ? (TxResult<TTx>)(object)rawTx.DecodeRaw()
                                : rawTx.Decode(_codec);
                        }
                        catch (Exception decodeEx)
                        {
                            ErrorOccurred?.Invoke(this,
                                new CometBftEventArgs<Exception>(
                                    new InvalidOperationException(
                                        $"Failed to decode transaction {rawTx.Hash} at height {rawTx.Height}.", decodeEx)));
                            break;
                        }

                        TxExecuted?.Invoke(this, new CometBftEventArgs<TxResult<TTx>>(decodedTx));
                    }

                    break;

                case WsVoteData voteData:
                    var vote = WebSocketMessageParser.ParseVote(voteData);
                    if (vote is not null)
                    {
                        VoteReceived?.Invoke(this, new CometBftEventArgs<Vote>(vote));
                    }

                    break;

                case WsValidatorSetUpdatesData vsData:
                    var validators = WebSocketMessageParser.ParseValidatorSetUpdates(vsData);
                    if (validators is not null)
                    {
                        ValidatorSetUpdated?.Invoke(this,
                            new CometBftEventArgs<IReadOnlyList<Validator>>(validators));
                    }

                    var vsUpdateData = WebSocketMessageParser.ParseValidatorSetUpdatesData(vsData);
                    if (vsUpdateData is not null)
                    {
                        _validatorSetUpdatesSubject.OnNext(vsUpdateData);
                    }

                    break;

                case WsNewBlockEventsData nbeData:
                    var nbe = WebSocketMessageParser.ParseNewBlockEvents(nbeData);
                    if (nbe is not null)
                    {
                        _newBlockEventsSubject.OnNext(nbe);
                    }

                    break;

                case WsCompleteProposalData cpData:
                    var cp = WebSocketMessageParser.ParseCompleteProposal(cpData);
                    if (cp is not null)
                    {
                        _completeProposalSubject.OnNext(cp);
                    }

                    break;

                case WsNewEvidenceData neData:
                    var ne = WebSocketMessageParser.ParseNewEvidence(neData);
                    if (ne is not null)
                    {
                        _newEvidenceSubject.OnNext(ne);
                    }

                    break;

                case WsConsensusInternalData ciData:
                    _consensusInternalSubject.OnNext(new CometBftEvent(ciData.EventTopic, []));
                    break;
            }
        }
        catch (JsonException ex)
        {
            ErrorOccurred?.Invoke(this, new CometBftEventArgs<Exception>(ex));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            ErrorOccurred?.Invoke(this, new CometBftEventArgs<Exception>(ex));
        }
    }
}

/// <summary>
/// WebSocket-based subscription client for CometBFT events.
/// Transactions and blocks are surfaced as raw base64-encoded strings.
/// </summary>
/// <remarks>
/// This is the default, backward-compatible client equivalent to
/// <see cref="CometBftWebSocketClient{TTx}"/> with <c>TTx = string</c>
/// and <see cref="RawTxCodec"/> as the codec.
/// Use <see cref="CometBftWebSocketClient{TTx}"/> directly to receive
/// decoded, strongly-typed transactions.
/// </remarks>
public sealed class CometBftWebSocketClient : CometBftWebSocketClient<string>, ICometBftWebSocketClient
{
    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketClient"/>.
    /// </summary>
    /// <param name="options">The WebSocket configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public CometBftWebSocketClient(IOptions<CometBftWebSocketOptions> options)
        : base(options, RawTxCodec.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketClient"/> with a custom WebSocket client factory.
    /// </summary>
    /// <param name="options">The WebSocket configuration options.</param>
    /// <param name="factory">The factory used to create the underlying WebSocket client.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="factory"/> is <c>null</c>.</exception>
    internal CometBftWebSocketClient(IOptions<CometBftWebSocketOptions> options, IWebSocketClientFactory factory)
        : base(options, factory, RawTxCodec.Instance)
    {
    }
}
