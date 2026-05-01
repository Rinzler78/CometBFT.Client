using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace CometBFT.Client.Fixture.Tests;

internal sealed class PassiveWebSocketServer : IAsyncDisposable
{
    private const string LoopbackHost = "127.0.0.1";

    private readonly bool _sendAck;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource<System.Net.WebSockets.WebSocket> _socketReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _serverTask = Task.CompletedTask;

    public PassiveWebSocketServer(bool sendAck)
    {
        _sendAck = sendAck;
    }

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

    public async Task PushAsync(string json, TimeSpan? connectTimeout = null)
    {
        var socket = await _socketReady.Task.WaitAsync(connectTimeout ?? TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
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

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Close();
                continue;
            }

            _ = HandleConnectionAsync(context, cancellationToken);
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

                if (!_sendAck || !TryGetId(text, out var id))
                {
                    continue;
                }

                var ackJson = JsonSerializer.Serialize(WebSocketServerReply.Ok(id));
                var ackBytes = Encoding.UTF8.GetBytes(ackJson);
                await socket.SendAsync(ackBytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Best effort server for tests.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Close();
        await _serverTask.ConfigureAwait(false);
        _cts.Dispose();
    }

    private static bool TryGetId(string json, out int id)
    {
        id = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("id", out var idElement))
            {
                return false;
            }

            id = idElement.GetInt32();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int FindFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
