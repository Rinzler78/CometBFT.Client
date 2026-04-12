using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
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
    public event EventHandler<TendermintEventArgs<Block>>? NewBlockReceived;

    /// <inheritdoc />
    public event EventHandler<TendermintEventArgs<BlockHeader>>? NewBlockHeaderReceived;

    /// <inheritdoc />
    public event EventHandler<TendermintEventArgs<TxResult>>? TxExecuted;

    /// <inheritdoc />
    public event EventHandler<TendermintEventArgs<Vote>>? VoteReceived;

    /// <inheritdoc />
    public event EventHandler<TendermintEventArgs<IReadOnlyList<Validator>>>? ValidatorSetUpdated;

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

    private void OnMessageReceived(ResponseMessage message)
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
                case "tendermint/event/NewBlock":
                    var block = ParseNewBlock(json!);
                    if (block is not null)
                    {
                        NewBlockReceived?.Invoke(this, new TendermintEventArgs<Block>(block));
                    }

                    break;

                case "tendermint/event/NewBlockHeader":
                    var blockHeader = ParseNewBlockHeader(json!);
                    if (blockHeader is not null)
                    {
                        NewBlockHeaderReceived?.Invoke(this, new TendermintEventArgs<BlockHeader>(blockHeader));
                    }

                    break;

                case "tendermint/event/Tx":
                    var tx = ParseTxResult(json!);
                    if (tx is not null)
                    {
                        TxExecuted?.Invoke(this, new TendermintEventArgs<TxResult>(tx));
                    }

                    break;

                case "tendermint/event/Vote":
                    var vote = ParseVote(json!);
                    if (vote is not null)
                    {
                        VoteReceived?.Invoke(this, new TendermintEventArgs<Vote>(vote));
                    }

                    break;

                case "tendermint/event/ValidatorSetUpdates":
                    var validators = ParseValidatorSetUpdates(json!);
                    if (validators is not null)
                    {
                        ValidatorSetUpdated?.Invoke(this, new TendermintEventArgs<IReadOnlyList<Validator>>(validators));
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

    private static Block? ParseNewBlock(JsonNode json)
    {
        var blockNode = json["result"]?["data"]?["value"]?["block"];
        if (blockNode is null)
        {
            return null;
        }

        var header = blockNode["header"];
        var height = long.TryParse(header?["height"]?.GetValue<string>(), out var h) ? h : 0;
        var time = header?["time"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue;
        var proposer = header?["proposer_address"]?.GetValue<string>() ?? string.Empty;
        var txsNode = blockNode["data"]?["txs"]?.AsArray();
        var txs = txsNode?.Select(t => t?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        var hash = json["result"]?["data"]?["value"]?["block_id"]?["hash"]?.GetValue<string>() ?? string.Empty;

        return new Block(height, hash, time, proposer, txs.AsReadOnly());
    }

    private static BlockHeader? ParseNewBlockHeader(JsonNode json)
    {
        var header = json["result"]?["data"]?["value"]?["header"];
        if (header is null)
        {
            return null;
        }

        var version = header["version"]?["block"]?.GetValue<string>() ?? string.Empty;
        var chainId = header["chain_id"]?.GetValue<string>() ?? string.Empty;
        var height = long.TryParse(header["height"]?.GetValue<string>(), out var h) ? h : 0;
        var time = DateTimeOffset.TryParse(header["time"]?.GetValue<string>(), out var t)
            ? t
            : DateTimeOffset.MinValue;
        var lastBlockId = header["last_block_id"]?["hash"]?.GetValue<string>() ?? string.Empty;

        return new BlockHeader(
            Version: version,
            ChainId: chainId,
            Height: height,
            Time: time,
            LastBlockId: lastBlockId,
            LastCommitHash: header["last_commit_hash"]?.GetValue<string>() ?? string.Empty,
            DataHash: header["data_hash"]?.GetValue<string>() ?? string.Empty,
            ValidatorsHash: header["validators_hash"]?.GetValue<string>() ?? string.Empty,
            NextValidatorsHash: header["next_validators_hash"]?.GetValue<string>() ?? string.Empty,
            ConsensusHash: header["consensus_hash"]?.GetValue<string>() ?? string.Empty,
            AppHash: header["app_hash"]?.GetValue<string>() ?? string.Empty,
            LastResultsHash: header["last_results_hash"]?.GetValue<string>() ?? string.Empty,
            EvidenceHash: header["evidence_hash"]?.GetValue<string>() ?? string.Empty,
            ProposerAddress: header["proposer_address"]?.GetValue<string>() ?? string.Empty);
    }

    private static TxResult? ParseTxResult(JsonNode json)
    {
        var txNode = json["result"]?["data"]?["value"]?["TxResult"];
        if (txNode is null)
        {
            return null;
        }

        var height = long.TryParse(txNode["height"]?.GetValue<string>(), out var h) ? h : 0;
        var index = txNode["index"]?.GetValue<int>() ?? 0;
        var resultNode = txNode["result"];
        var code = resultNode?["code"]?.GetValue<uint>() ?? 0;
        var log = resultNode?["log"]?.GetValue<string>();
        var gasWanted = long.TryParse(resultNode?["gas_wanted"]?.GetValue<string>(), out var gw) ? gw : 0;
        var gasUsed = long.TryParse(resultNode?["gas_used"]?.GetValue<string>(), out var gu) ? gu : 0;

        // Hash from the events map (tx.hash is the canonical source)
        var hash = json["result"]?["events"]?["tx.hash"]?[0]?.GetValue<string>() ?? string.Empty;

        // Events from TxResult.result.events
        var eventsNode = resultNode?["events"]?.AsArray();
        var events = eventsNode?
            .Select(e => new TendermintEvent(
                e?["type"]?.GetValue<string>() ?? string.Empty,
                (e?["attributes"]?.AsArray() ?? new JsonArray())
                    .Select(a => new Core.Domain.AbciEventEntry(
                        a?["key"]?.GetValue<string>() ?? string.Empty,
                        a?["value"]?.GetValue<string>(),
                        a?["index"]?.GetValue<bool>() ?? false))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly()
            ?? (IReadOnlyList<TendermintEvent>)Array.Empty<TendermintEvent>();

        return new TxResult(hash, height, index, string.Empty, code, null, log, null,
            gasWanted, gasUsed, events, null);
    }

    private static Vote? ParseVote(JsonNode json)
    {
        var voteNode = json["result"]?["data"]?["value"]?["Vote"];
        if (voteNode is null)
        {
            return null;
        }

        var type = voteNode["type"]?.GetValue<int>() ?? 0;
        var height = long.TryParse(voteNode["height"]?.GetValue<string>(), out var h) ? h : 0;
        var round = voteNode["round"]?.GetValue<int>() ?? 0;
        var validatorAddress = voteNode["validator_address"]?.GetValue<string>() ?? string.Empty;
        var timestamp = DateTimeOffset.TryParse(voteNode["timestamp"]?.GetValue<string>(), out var ts)
            ? ts
            : DateTimeOffset.MinValue;

        return new Vote(type, height, round, validatorAddress, timestamp);
    }

    private static ReadOnlyCollection<Validator>? ParseValidatorSetUpdates(JsonNode json)
    {
        var updatesNode = json["result"]?["data"]?["value"]?["validator_updates"]?.AsArray();
        if (updatesNode is null)
        {
            return null;
        }

        return updatesNode
            .Select(v => new Validator(
                v?["address"]?.GetValue<string>() ?? string.Empty,
                v?["pub_key"]?["data"]?.GetValue<string>() ?? string.Empty,
                long.TryParse(v?["power"]?.GetValue<string>(), out var vp) ? vp : 0,
                0))
            .ToList()
            .AsReadOnly();
    }
}
