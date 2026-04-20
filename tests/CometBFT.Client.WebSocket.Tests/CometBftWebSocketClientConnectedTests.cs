using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Integration-style tests for <see cref="CometBftWebSocketClient"/> connected paths.
/// Uses a local <see cref="TestWebSocketServer"/> that echoes JSON-RPC subscribe acks.
/// </summary>
public sealed class CometBftWebSocketClientConnectedTests : IAsyncLifetime
{
    private TestWebSocketServer _server = null!;

    public async ValueTask InitializeAsync()
    {
        _server = new TestWebSocketServer();
        await _server.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    private IOptions<CometBftWebSocketOptions> ServerOptions(TimeSpan? ackTimeout = null) =>
        Options.Create(new CometBftWebSocketOptions
        {
            BaseUrl = _server.Url,
            SubscribeAckTimeout = ackTimeout ?? TimeSpan.FromSeconds(5),
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(10),
        });

    // ── ConnectAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectAsync_ToRealServer_Succeeds()
    {
        await using var client = new CometBftWebSocketClient(ServerOptions());
        var ex = await Record.ExceptionAsync(() => client.ConnectAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ReturnsImmediately()
    {
        await using var client = new CometBftWebSocketClient(ServerOptions());
        await client.ConnectAsync();

        var ex = await Record.ExceptionAsync(() => client.ConnectAsync());
        Assert.Null(ex);
    }

    // ── SubscribeNewBlockAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SubscribeNewBlockAsync_WhenConnected_ReceivesAck()
    {
        await using var client = new CometBftWebSocketClient(ServerOptions());
        await client.ConnectAsync();

        var ex = await Record.ExceptionAsync(() => client.SubscribeNewBlockAsync());
        Assert.Null(ex);
    }

    // ── UnsubscribeAllAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task UnsubscribeAllAsync_WhenConnected_Succeeds()
    {
        await using var client = new CometBftWebSocketClient(ServerOptions());
        await client.ConnectAsync();

        var ex = await Record.ExceptionAsync(() => client.UnsubscribeAllAsync());
        Assert.Null(ex);
    }

    // ── DisconnectAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_WhenConnected_Succeeds()
    {
        var client = new CometBftWebSocketClient(ServerOptions());
        await client.ConnectAsync();

        var ex = await Record.ExceptionAsync(() => client.DisconnectAsync());
        await client.DisposeAsync();

        Assert.Null(ex);
    }

    // ── DisposeAsync when connected ──────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_WhenConnected_Succeeds()
    {
        var client = new CometBftWebSocketClient(ServerOptions());
        await client.ConnectAsync();

        var ex = await Record.ExceptionAsync(() => client.DisposeAsync().AsTask());
        Assert.Null(ex);
    }

    // ── ConnectAsync failure path (StartOrFail throws) ───────────────────────

    [Fact]
    public async Task ConnectAsync_ConnectionRefused_ThrowsCometBftWebSocketException()
    {
        var opts = Options.Create(new CometBftWebSocketOptions
        {
            BaseUrl = "ws://127.0.0.1:1/",
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        });
        await using var client = new CometBftWebSocketClient(opts);

        await Assert.ThrowsAsync<Core.Exceptions.CometBftWebSocketException>(() => client.ConnectAsync());
    }
}

/// <summary>
/// Minimal WebSocket echo server for testing connected-client paths.
/// Accepts connections and responds to any text message with a JSON-RPC ack
/// using the same id found in the received message.
/// </summary>
internal sealed class TestWebSocketServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task _serverTask = Task.CompletedTask;

    public string Url { get; private set; } = "";

    public Task StartAsync()
    {
        var port = FindFreePort();
        Url = $"ws://127.0.0.1:{port}/";
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _serverTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            if (ctx.Request.IsWebSocketRequest)
            {
                _ = HandleConnectionAsync(ctx, ct);
            }
        }
    }

    private static async Task HandleConnectionAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
        var ws = wsCtx.WebSocket;
        var buffer = new byte[4096];

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ConfigureAwait(false);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        if (doc.RootElement.TryGetProperty("id", out var idProp))
                        {
                            var id = idProp.GetInt32();
                            var ackBytes = Encoding.UTF8.GetBytes(
                                $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{}}}}");
                            await ws.SendAsync(
                                ackBytes, WebSocketMessageType.Text, true, ct)
                                .ConfigureAwait(false);
                        }
                    }
                    catch { /* ignore malformed messages */ }
                }
            }
        }
        finally
        {
            ws.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { }
        try { await _serverTask.ConfigureAwait(false); } catch { }
        _cts.Dispose();
    }

    private static int FindFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
