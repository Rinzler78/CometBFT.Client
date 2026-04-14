using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket.Internal;
using Microsoft.Extensions.Options;
using Websocket.Client;

namespace CometBFT.Client.WebSocket;

/// <summary>
/// WebSocket-based subscription client for CometBFT events.
/// Uses <see cref="WebsocketClient"/> with automatic reconnection.
/// </summary>
public sealed class CometBftWebSocketClient : ICometBftWebSocketClient
{
    private readonly CometBftWebSocketOptions _options;
    private readonly IWebSocketClientFactory _factory;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _activeSubscriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// Pending subscribe acknowledgments keyed by JSON-RPC request id.
    /// Completed by <see cref="OnMessageReceived"/> when the server ack arrives.
    /// Internal for testability.
    /// </summary>
    internal readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _pendingAcks = new();

    private WebsocketClient? _client;
    private IDisposable? _messageSubscription;
    private IDisposable? _reconnectionSubscription;
    private int _requestId;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<Block>>? NewBlockReceived;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<BlockHeader>>? NewBlockHeaderReceived;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<TxResult>>? TxExecuted;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<Vote>>? VoteReceived;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<IReadOnlyList<Validator>>>? ValidatorSetUpdated;

    /// <inheritdoc />
    public event EventHandler<CometBftEventArgs<Exception>>? ErrorOccurred;

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketClient"/>.
    /// </summary>
    /// <param name="options">The WebSocket configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public CometBftWebSocketClient(IOptions<CometBftWebSocketOptions> options)
        : this(options, new DefaultWebSocketClientFactory())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketClient"/> with a custom WebSocket client factory.
    /// </summary>
    /// <param name="options">The WebSocket configuration options.</param>
    /// <param name="factory">The factory used to create the underlying WebSocket client.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="factory"/> is <c>null</c>.</exception>
    internal CometBftWebSocketClient(IOptions<CometBftWebSocketOptions> options, IWebSocketClientFactory factory)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
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
    public Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var id = Interlocked.Increment(ref _requestId);
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "unsubscribe_all",
            id,
            @params = new { },
        });
        _client!.Send(payload);
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

        // Cancel all pending subscribe acknowledgments to unblock any awaiting callers.
        foreach (var (_, tcs) in _pendingAcks)
        {
            tcs.TrySetCanceled();
        }

        _pendingAcks.Clear();
        _reconnectionSubscription?.Dispose();
        _messageSubscription?.Dispose();

        if (_client is not null)
        {
            try
            {
                await _client.Stop(
                    System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                    "Disposing").ConfigureAwait(false);
            }
            catch
            {
                // Best-effort close
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

        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "subscribe",
            id,
            @params = new { query },
        });
        _client!.Send(payload);

        // Wait for the server's JSON-RPC acknowledgment {"jsonrpc":"2.0","id":<id>,"result":{}}.
        // This confirms the subscription is active before the caller begins waiting for events.
        using var ackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ackCts.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await tcs.Task.WaitAsync(ackCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingAcks.TryRemove(id, out _);
            throw new CometBftWebSocketException(
                $"Timeout waiting for server acknowledgment of subscribe query '{query}'.");
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

    private void OnReconnected(ReconnectionInfo _)
    {
        // Re-send all active subscriptions after a reconnect.
        // Iterate .Keys to get the query string — NOT the KeyValuePair.
        foreach (var query in _activeSubscriptions.Keys)
        {
            var id = Interlocked.Increment(ref _requestId);
            var payload = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                method = "subscribe",
                id,
                @params = new { query },
            });
            _client?.Send(payload);
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
            var json = JsonNode.Parse(message.Text);

            // Detect subscribe acknowledgment: id > 0 and result present but without a data field.
            // Example: {"jsonrpc":"2.0","id":1,"result":{}}
            var resultNode = json?["result"];
            var msgId = json?["id"]?.GetValue<int>();
            if (msgId is > 0 && resultNode is not null && resultNode["data"] is null)
            {
                if (_pendingAcks.TryRemove(msgId.Value, out var ackTcs))
                {
                    ackTcs.TrySetResult(true);
                }

                return;
            }

            var eventType = json?["result"]?["data"]?["type"]?.GetValue<string>();

            switch (eventType)
            {
                case CometBftEventType.NewBlock:
                    var block = WebSocketMessageParser.ParseNewBlock(json!);
                    if (block is not null)
                    {
                        NewBlockReceived?.Invoke(this, new CometBftEventArgs<Block>(block));
                    }

                    break;

                case CometBftEventType.NewBlockHeader:
                    var blockHeader = WebSocketMessageParser.ParseNewBlockHeader(json!);
                    if (blockHeader is not null)
                    {
                        NewBlockHeaderReceived?.Invoke(this, new CometBftEventArgs<BlockHeader>(blockHeader));
                    }

                    break;

                case CometBftEventType.Tx:
                    var tx = WebSocketMessageParser.ParseTxResult(json!);
                    if (tx is not null)
                    {
                        TxExecuted?.Invoke(this, new CometBftEventArgs<TxResult>(tx));
                    }

                    break;

                case CometBftEventType.Vote:
                    var vote = WebSocketMessageParser.ParseVote(json!);
                    if (vote is not null)
                    {
                        VoteReceived?.Invoke(this, new CometBftEventArgs<Vote>(vote));
                    }

                    break;

                case CometBftEventType.ValidatorSetUpdates:
                    var validators = WebSocketMessageParser.ParseValidatorSetUpdates(json!);
                    if (validators is not null)
                    {
                        ValidatorSetUpdated?.Invoke(this, new CometBftEventArgs<IReadOnlyList<Validator>>(validators));
                    }

                    break;
            }
        }
        catch (JsonException ex)
        {
            // Parsing errors are non-fatal: the message is malformed or unexpected.
            // Keep the Rx pipeline alive and notify observers.
            ErrorOccurred?.Invoke(this, new CometBftEventArgs<Exception>(ex));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Unexpected error in dispatch or event handler — keep the pipeline alive
            // but surface the exception so callers can react.
            ErrorOccurred?.Invoke(this, new CometBftEventArgs<Exception>(ex));
        }
    }
}
