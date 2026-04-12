using System.Text.Json;
using System.Text.Json.Nodes;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket.Internal;
using Websocket.Client;

namespace CometBFT.Client.WebSocket;

/// <summary>
/// WebSocket-based subscription client for CometBFT events.
/// Uses <see cref="WebsocketClient"/> with automatic reconnection.
/// </summary>
public sealed class CometBftWebSocketClient : ICometBftWebSocketClient
{
    private readonly CometBftWebSocketOptions _options;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly HashSet<string> _activeSubscriptions = new(StringComparer.Ordinal);
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

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketClient"/>.
    /// </summary>
    /// <param name="options">The WebSocket configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public CometBftWebSocketClient(CometBftWebSocketOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
                newClient = new WebsocketClient(uri)
                {
                    ReconnectTimeout = _options.ReconnectTimeout,
                    ErrorReconnectTimeout = _options.ErrorReconnectTimeout,
                };

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

    /// <inheritdoc />
    public async Task SubscribeNewBlockAsync(CancellationToken cancellationToken = default)
    {
        await SendSubscribeAsync("tm.event='NewBlock'", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SubscribeNewBlockHeaderAsync(CancellationToken cancellationToken = default)
    {
        await SendSubscribeAsync("tm.event='NewBlockHeader'", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SubscribeTxAsync(CancellationToken cancellationToken = default)
    {
        await SendSubscribeAsync("tm.event='Tx'", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SubscribeVoteAsync(CancellationToken cancellationToken = default)
    {
        await SendSubscribeAsync("tm.event='Vote'", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SubscribeValidatorSetUpdatesAsync(CancellationToken cancellationToken = default)
    {
        await SendSubscribeAsync("tm.event='ValidatorSetUpdates'", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
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
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
        _activeSubscriptions.Add(query);
        var id = Interlocked.Increment(ref _requestId);
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "subscribe",
            id,
            @params = new { query },
        });
        _client!.Send(payload);
        await Task.CompletedTask.ConfigureAwait(false);
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
        foreach (var query in _activeSubscriptions)
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
        catch (Exception)
        {
            // Swallow all exceptions to keep the Rx pipeline alive.
            // A malformed or unexpected message must never kill the subscription loop.
        }
    }

}
