using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CometBFT.Client.Core.Events;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// In-process WebSocket server for wire-level subscription tests.
/// Records every client-sent frame in <see cref="Messages"/> and optionally ACKs
/// each request by echoing <c>{"jsonrpc":"2.0","id":N,"result":{}}</c>.
/// Lets the test drive pushed events via <see cref="PushAsync"/>.
/// </summary>
internal sealed class PassiveWebSocketServer : IAsyncDisposable
{
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>
    /// Per-request ACK policy. Returns the reply to emit for the given incoming
    /// subscribe/unsubscribe frame (by id + method + query). Return <c>null</c> to
    /// suppress the ACK (simulating a server that swallows the request).
    /// </summary>
    public delegate WebSocketServerReply? AckPolicy(int id, string method, string query);

    private readonly bool _sendAck;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<System.Net.WebSockets.WebSocket> _socketReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _messageSignal = new(0, int.MaxValue);
    private Task _serverTask = Task.CompletedTask;
    private AckPolicy _ackPolicy = (id, _, _) => WebSocketServerReply.Ok(id);

    public PassiveWebSocketServer(bool sendAck)
    {
        _sendAck = sendAck;
    }

    /// <summary>Overrides the default "ACK every request as OK" behaviour.</summary>
    public void SetAckPolicy(AckPolicy policy) => _ackPolicy = policy;

    public string Url { get; private set; } = string.Empty;

    public ConcurrentQueue<string> Messages { get; } = new();

    public Task StartAsync()
    {
        var port = FindFreePort();
        Url = $"ws://{LoopbackHost}:{port}/";
        _listener.Prefixes.Add($"http://{LoopbackHost}:{port}/");
        _listener.Start();
        _serverTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>Pushes a text frame to the connected client.</summary>
    public async Task PushAsync(string json, TimeSpan? connectTimeout = null)
    {
        var socket = await _socketReady.Task.WaitAsync(connectTimeout ?? TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Blocks until the server has received at least <paramref name="count"/> frames,
    /// or <paramref name="timeout"/> elapses. Replaces test-internal <c>Task.Delay</c> drains.
    /// </summary>
    public async Task WaitForMessagesAsync(int count, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        for (var received = Messages.Count; received < count; received = Messages.Count)
        {
            try
            {
                await _messageSignal.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Counts subscribe frames whose query targets the given topic.</summary>
    public int CountSubscribes(WebSocketEventTopic topic)
    {
        var expected = WebSocketQueries.Of(topic);
        var count = 0;
        foreach (var msg in Messages)
        {
            if (TryGetMethodAndQuery(msg, out var method, out var q)
                && method == WebSocketRpcMethods.Subscribe && q == expected)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Counts frames whose <c>method</c> equals the given value.</summary>
    public int CountMethod(string method)
    {
        var count = 0;
        foreach (var msg in Messages)
        {
            if (TryGetMethodAndQuery(msg, out var m, out _) && m == method)
            {
                count++;
            }
        }
        return count;
    }

    private static bool TryGetMethodAndQuery(string json, out string method, out string query)
    {
        method = string.Empty;
        query = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("method", out var m))
            {
                return false;
            }
            method = m.GetString() ?? string.Empty;
            if (doc.RootElement.TryGetProperty("params", out var p)
                && p.TryGetProperty("query", out var q))
            {
                query = q.GetString() ?? string.Empty;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            if (context.Request.IsWebSocketRequest)
            {
                _ = HandleConnectionAsync(context, cancellationToken);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
            }
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var socketContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
        var socket = socketContext.WebSocket;
        _socketReady.TrySetResult(socket);
        var buffer = new byte[4096];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Messages.Enqueue(text);
                _messageSignal.Release();

                if (!_sendAck)
                {
                    continue;
                }

                if (!TryGetMethodAndQuery(text, out var method, out var query)
                    || !TryGetId(text, out var id))
                {
                    continue;
                }

                var reply = _ackPolicy(id, method, query);
                if (reply is null)
                {
                    continue;
                }

                var ackJson = JsonSerializer.Serialize(reply);
                var ackBytes = Encoding.UTF8.GetBytes(ackJson);
                await socket.SendAsync(ackBytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Best effort server for tests.
        }
    }

    private static bool TryGetId(string json, out int id)
    {
        id = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var idEl))
            {
                id = idEl.GetInt32();
                return true;
            }
        }
        catch
        {
            // fall-through: invalid JSON or non-numeric id
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { }
        try { await _serverTask.ConfigureAwait(false); } catch { }
        _listener.Close();
        _cts.Dispose();
        _messageSignal.Dispose();
    }

    private static int FindFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
