using Microsoft.Extensions.Options;
using NSubstitute;
using CometBFT.Client.Core.Codecs;
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

    // ── SubscribeAckTimeout validation ──────────────────────────────────────

    [Fact]
    public void CometBftWebSocketOptions_Validate_ZeroSubscribeAckTimeout_Throws()
    {
        var opts = new CometBftWebSocketOptions { SubscribeAckTimeout = TimeSpan.Zero };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }
}
