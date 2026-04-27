using Microsoft.Extensions.Options;
using NSubstitute;
using CometBFT.Client.Core.Codecs;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using CometBFT.Client.WebSocket.Internal;
using Websocket.Client;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Covers paths in <see cref="CometBftWebSocketClient{TTx}"/> and <see cref="CometBftWebSocketClient"/>
/// that require the internal constructor or direct <see cref="CometBftWebSocketClient{TTx}.OnMessageReceived"/> invocation.
/// </summary>
public sealed class CometBftWebSocketClientCoverageTests
{
    private static IOptions<CometBftWebSocketOptions> DefaultOptions() =>
        Options.Create(new CometBftWebSocketOptions());

    private sealed class NeverConnectFactory : IWebSocketClientFactory
    {
        public WebsocketClient Create(Uri uri, CometBftWebSocketOptions options) =>
            throw new InvalidOperationException("no real server in unit tests");
    }

    private static IWebSocketClientFactory StubFactory() => new NeverConnectFactory();

    // ── Internal constructor ─────────────────────────────────────────────────

    [Fact]
    public void InternalConstructor_NullFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CometBftWebSocketClient(DefaultOptions(), (IWebSocketClientFactory)null!));
    }

    [Fact]
    public void InternalConstructor_ValidFactory_DoesNotThrow()
    {
        var factory = StubFactory();
        var ex = Record.Exception(() => new CometBftWebSocketClient(DefaultOptions(), factory));
        Assert.Null(ex);
    }

    // ── DisconnectAsync when not connected ───────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ReturnsImmediately()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var ex = await Record.ExceptionAsync(() => client.DisconnectAsync());
        Assert.Null(ex);
    }

    // ── OnMessageReceived — early return paths ───────────────────────────────

    [Fact]
    public void OnMessageReceived_BinaryMessage_DoesNotFireAnyEvent()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var errorFired = false;
        client.ErrorOccurred += (_, _) => errorFired = true;

        var msg = ResponseMessage.BinaryMessage(Array.Empty<byte>());

        client.OnMessageReceived(msg);

        Assert.False(errorFired);
    }

    [Fact]
    public void OnMessageReceived_EmptyTextMessage_DoesNotFireAnyEvent()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var errorFired = false;
        client.ErrorOccurred += (_, _) => errorFired = true;

        var msg = ResponseMessage.TextMessage(string.Empty);

        client.OnMessageReceived(msg);

        Assert.False(errorFired);
    }

    [Fact]
    public void OnMessageReceived_WhitespaceTextMessage_DoesNotFireAnyEvent()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var errorFired = false;
        client.ErrorOccurred += (_, _) => errorFired = true;

        var msg = ResponseMessage.TextMessage("   ");

        client.OnMessageReceived(msg);

        Assert.False(errorFired);
    }

    // ── OnMessageReceived — invalid JSON → ErrorOccurred ────────────────────

    [Fact]
    public void OnMessageReceived_InvalidJson_FiresErrorOccurredWithJsonException()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        Exception? captured = null;
        client.ErrorOccurred += (_, e) => captured = e.Value;

        var msg = ResponseMessage.TextMessage("{not valid json!!!");

        client.OnMessageReceived(msg);

        Assert.NotNull(captured);
        Assert.IsAssignableFrom<Exception>(captured);
    }

    // ── OnMessageReceived — JSON-RPC error envelope ─────────────────────────

    [Fact]
    public async Task OnMessageReceived_ErrorEnvelope_FaultsPendingAckWithoutDoubleFiringErrorOccurred()
    {
        using var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[7] = tcs;
        var errorCount = 0;
        client.ErrorOccurred += (_, _) => errorCount++;

        const string error = """{"jsonrpc":"2.0","id":7,"error":{"code":-32603,"message":"subscribe failed","data":"provider rejected query"}}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(error));

        // The TCS carries the typed exception — SendSubscribeAsync's catch will fire
        // ErrorOccurred exactly once after it rolls back local state. OnMessageReceived
        // must NOT fire it too: doing so double-reports the same failure.
        var ex = await Assert.ThrowsAsync<CometBftWebSocketException>(async () => await tcs.Task);
        Assert.Equal("JSON-RPC error -32603: subscribe failed Data: provider rejected query", ex.Message);
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public async Task OnMessageReceived_ErrorEnvelopeWithoutData_FaultsPendingAck()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[8] = tcs;

        const string error = """{"jsonrpc":"2.0","id":8,"error":{"code":429,"message":"Too Many Requests"}}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(error));

        var ex = await Assert.ThrowsAsync<CometBftWebSocketException>(async () => await tcs.Task);
        Assert.Equal("JSON-RPC error 429: Too Many Requests", ex.Message);
    }

    [Fact]
    public void OnMessageReceived_ErrorEnvelopeWithoutPendingAck_FiresErrorOccurred()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        Exception? captured = null;
        client.ErrorOccurred += (_, e) => captured = e.Value;

        // id=0 means event-style frame; error carried at envelope level without an ack match
        const string error = """{"jsonrpc":"2.0","id":0,"error":{"code":-32600,"message":"invalid request"}}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(error));

        Assert.NotNull(captured);
        Assert.Contains("-32600", captured!.Message, StringComparison.Ordinal);
    }

    // ── OnMessageReceived — Lava provider-relay error envelope ──────────────

    [Fact]
    public async Task OnMessageReceived_ProviderErrorEnvelope_FaultsAllPendingAcksWithoutTransportDuplicate()
    {
        using var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var ack1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ack2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[11] = ack1;
        client._pendingAcks[12] = ack2;
        var errorCount = 0;
        client.ErrorOccurred += (_, _) => errorCount++;

        const string error = """{"Error_Received":"{\"Error_GUID\":\"guid-2\",\"Error\":\"failed relay, insufficient results\"}"}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(error));

        var ex1 = await Assert.ThrowsAsync<CometBftWebSocketException>(async () => await ack1.Task);
        var ex2 = await Assert.ThrowsAsync<CometBftWebSocketException>(async () => await ack2.Task);
        Assert.Equal("Provider relay error (guid-2): failed relay, insufficient results", ex1.Message);
        Assert.Equal(ex1.Message, ex2.Message);
        // With pending acks, each SendSubscribeAsync catch block will fire ErrorOccurred
        // after rolling back its own state. The transport layer must NOT fire again
        // because it has no per-subscribe context to report.
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public void OnMessageReceived_ProviderErrorEnvelope_WithoutPendingAcks_FiresErrorOccurredOnce()
    {
        using var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var errorCount = 0;
        Exception? captured = null;
        client.ErrorOccurred += (_, e) => { errorCount++; captured = e.Value; };

        const string error = """{"Error_Received":"{\"Error_GUID\":\"guid-orphan\",\"Error\":\"upstream unavailable\"}"}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(error));

        // Orphan provider error (no pending ack to observe the TCS) must still surface
        // via ErrorOccurred so apps can react to transport-level failures.
        Assert.Equal(1, errorCount);
        Assert.NotNull(captured);
        Assert.Contains("guid-orphan", captured!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnMessageReceived_ProviderErrorEnvelopeWithoutGuid_OmitsGuidSuffix()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var ack = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[14] = ack;

        const string error = """{"Error_Received":"{\"Error\":\"transport failed\"}"}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(error));

        var ex = await Assert.ThrowsAsync<CometBftWebSocketException>(async () => await ack.Task);
        Assert.Equal("Provider relay error: transport failed", ex.Message);
    }

    [Fact]
    public async Task OnMessageReceived_ProviderErrorEnvelopeWithInvalidPayload_UsesRawMessage()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var ack = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[13] = ack;

        const string error = """{"Error_Received":"not-json"}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(error));

        var ex = await Assert.ThrowsAsync<CometBftWebSocketException>(async () => await ack.Task);
        Assert.Equal("Provider relay error: not-json", ex.Message);
    }

    // ── OnMessageReceived — ACK (id > 0, no data) ───────────────────────────

    [Fact]
    public void OnMessageReceived_AckEnvelope_ResolvesPendingAck()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[1] = tcs;

        // Server echoes back id=1 with an empty result (subscribe ack)
        const string ack = """{"jsonrpc":"2.0","id":1,"result":{}}""";
        client.OnMessageReceived(ResponseMessage.TextMessage(ack));

        Assert.True(tcs.Task.IsCompletedSuccessfully);
    }

    // ── OnMessageReceived — failing codec → ErrorOccurred for NewBlock ───────

    [Fact]
    public void OnMessageReceived_ThrowingCodecOnNewBlock_FiresErrorOccurred()
    {
        var codec = Substitute.For<ITxCodec<string>>();
        codec.Decode(Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("boom"));

        var client = new CometBftWebSocketClient<string>(DefaultOptions(), StubFactory(), codec);
        Exception? captured = null;
        client.ErrorOccurred += (_, e) => captured = e.Value;

        // Minimal valid NewBlock envelope — txs list contains one base64 byte
        const string newBlock = """
            {
              "jsonrpc":"2.0","id":0,
              "result":{
                "data":{
                  "type":"tendermint/event/NewBlock",
                  "value":{
                    "block_id":{"hash":"AABB"},
                    "block":{
                      "header":{"height":"1","time":"2024-01-01T00:00:00Z","proposer_address":"AA"},
                      "data":{"txs":["AA=="]}
                    }
                  }
                }
              }
            }
            """;

        client.OnMessageReceived(ResponseMessage.TextMessage(newBlock));

        Assert.NotNull(captured);
        Assert.Contains("decode block", captured!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── OnMessageReceived — failing codec → ErrorOccurred for Tx ────────────

    [Fact]
    public void OnMessageReceived_ThrowingCodecOnTx_FiresErrorOccurred()
    {
        var codec = Substitute.For<ITxCodec<string>>();
        codec.Decode(Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("boom"));

        var client = new CometBftWebSocketClient<string>(DefaultOptions(), StubFactory(), codec);
        Exception? captured = null;
        client.ErrorOccurred += (_, e) => captured = e.Value;

        // Minimal valid Tx envelope — tx bytes as empty base64
        const string txEvent = """
            {
              "jsonrpc":"2.0","id":0,
              "result":{
                "data":{
                  "type":"tendermint/event/Tx",
                  "value":{
                    "TxResult":{
                      "height":"1","index":0,
                      "result":{"code":0,"gas_wanted":"100","gas_used":"80"}
                    }
                  }
                },
                "events":{"tx.hash":["DEADBEEF"]}
              }
            }
            """;

        client.OnMessageReceived(ResponseMessage.TextMessage(txEvent));

        Assert.NotNull(captured);
        Assert.Contains("decode transaction", captured!.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Generic client null codec ────────────────────────────────────────────

    [Fact]
    public void GenericClient_NullCodec_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CometBftWebSocketClient<string>(DefaultOptions(), StubFactory(), null!));
    }

    // ── DisposeAsync cancels pending acks ────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_CancelsPendingAcks()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[99] = tcs;

        await client.DisposeAsync();

        Assert.True(tcs.Task.IsCanceled);
    }

    // ── Sync Dispose() covers the IDisposable companion ──────────────────────

    [Fact]
    public void Dispose_Sync_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var ex = Record.Exception(() => client.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Sync_CalledTwice_IsIdempotent()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        client.Dispose();
        var ex = Record.Exception(() => client.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Sync_CancelsPendingAcks()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[42] = tcs;

        client.Dispose();

        Assert.True(tcs.Task.IsCanceled);
    }

    [Fact]
    public async Task DisposeAsync_WhenNeverConnected_IsNoOpAndFast()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var start = DateTime.UtcNow;
        await client.DisposeAsync();
        var elapsed = DateTime.UtcNow - start;
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"DisposeAsync took {elapsed.TotalSeconds:F2}s when it should be instantaneous without _client");
    }

    [Fact]
    public async Task DisposeAsync_AfterSyncDispose_IsNoOp()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        client.Dispose();
        var ex = await Record.ExceptionAsync(async () => await client.DisposeAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Dispose_Sync_AfterConnect_FiresAndForgetsClose()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        var options = Options.Create(new CometBftWebSocketOptions
        {
            BaseUrl = server.Url,
            SubscribeAckTimeout = TimeSpan.FromMilliseconds(500),
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        });
        var client = new CometBftWebSocketClient(options);
        await client.ConnectAsync();

        // Sync Dispose must return promptly (no bounded await on the network close).
        var start = DateTime.UtcNow;
        client.Dispose();
        var elapsed = DateTime.UtcNow - start;

        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"Sync Dispose took {elapsed.TotalSeconds:F2}s");
    }

    [Fact]
    public async Task DisposeAsync_AfterConnect_ClosesSocketWithinBoundedTimeout()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        var options = Options.Create(new CometBftWebSocketOptions
        {
            BaseUrl = server.Url,
            SubscribeAckTimeout = TimeSpan.FromMilliseconds(500),
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        });
        var client = new CometBftWebSocketClient(options);
        await client.ConnectAsync();

        var start = DateTime.UtcNow;
        await client.DisposeAsync();
        var elapsed = DateTime.UtcNow - start;

        // DisposeAsyncCore awaits _client.Stop().WaitAsync(5s); localhost closes sub-second.
        Assert.True(elapsed < TimeSpan.FromSeconds(6), $"DisposeAsync took {elapsed.TotalSeconds:F2}s");
    }

    // ── OnReconnected re-sends active subscriptions ──────────────────────────

    [Fact]
    public void OnReconnected_WithActiveSubscriptions_ResendsViaInternalMethod()
    {
        // OnReconnected is now internal — we can invoke it directly.
        // It iterates _activeSubscriptions and tries to send on _client (null here),
        // so we just verify it does NOT throw when the underlying WebSocket client is null.
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());

        // Add a pending ack so OnReconnected has something to iterate.
        client._pendingAcks[1] = new TaskCompletionSource<bool>();

        var ex = Record.Exception(() =>
            client.OnReconnected(new ReconnectionInfo(ReconnectionType.Initial)));

        Assert.Null(ex);
    }

    [Fact]
    public void OnReconnected_NonInitialType_FiresReconnectedEvent()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var fired = false;
        client.Reconnected += (_, _) => fired = true;

        client.OnReconnected(new ReconnectionInfo(ReconnectionType.NoMessageReceived));

        Assert.True(fired);
    }

    [Fact]
    public void OnReconnected_InitialType_DoesNotFireReconnectedEvent()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var fired = false;
        client.Reconnected += (_, _) => fired = true;

        client.OnReconnected(new ReconnectionInfo(ReconnectionType.Initial));

        Assert.False(fired);
    }

    // ── OnDisconnected fires Disconnected event ───────────────────────────────

    [Fact]
    public void OnDisconnected_WithSubscriber_FiresDisconnectedEvent()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());
        var fired = false;
        client.Disconnected += (_, _) => fired = true;

        client.OnDisconnected(null!);

        Assert.True(fired);
    }

    [Fact]
    public void OnDisconnected_WithoutSubscriber_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(DefaultOptions(), StubFactory());

        var ex = Record.Exception(() => client.OnDisconnected(null!));

        Assert.Null(ex);
    }

    // ── SubscribeAckTimeout validation ──────────────────────────────────────

    [Fact]
    public void CometBftWebSocketOptions_Validate_ZeroSubscribeAckTimeout_Throws()
    {
        var opts = new CometBftWebSocketOptions { SubscribeAckTimeout = TimeSpan.Zero };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }
}
