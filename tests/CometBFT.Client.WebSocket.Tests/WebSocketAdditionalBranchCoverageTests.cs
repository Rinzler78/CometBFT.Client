using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Options;
using NSubstitute;
using CometBFT.Client.WebSocket;
using Websocket.Client;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class WebSocketAdditionalBranchCoverageTests
{
    [Fact]
    public void OnMessageReceived_InvalidJsonWithoutErrorSubscriber_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new Core.Options.CometBftWebSocketOptions()));
        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("not-valid-json{{{")));
        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_BlockDecodeFailureWithoutErrorSubscriber_DoesNotThrow()
    {
        var codec = NSubstitute.Substitute.For<Core.Codecs.ITxCodec<string>>();
        codec.Decode(NSubstitute.Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("decode boom"));
        var client = new CometBftWebSocketClient<string>(Options.Create(new Core.Options.CometBftWebSocketOptions()), new NeverConnectFactory(), codec);

        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{
              "type":"tendermint/event/NewBlock",
              "value":{
                "block_id":{"hash":"AA"},
                "block":{"header":{"height":"1","time":"2024-01-01T00:00:00Z","proposer_address":"P"},"data":{"txs":["AQ=="]}}
              }
            }
          }
        }
        """)));

        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_TxDecodeFailureWithoutErrorSubscriber_DoesNotThrow()
    {
        var codec = NSubstitute.Substitute.For<Core.Codecs.ITxCodec<string>>();
        codec.Decode(NSubstitute.Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("decode boom"));
        var client = new CometBftWebSocketClient<string>(Options.Create(new Core.Options.CometBftWebSocketOptions()), new NeverConnectFactory(), codec);

        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/Tx","value":{"TxResult":{"height":"1","index":0,"result":{"code":0,"gas_wanted":"1","gas_used":"1","events":[]}}}},
            "events":{"tx.hash":["HASH"]}
          }
        }
        """)));

        Assert.Null(ex);
    }

    [Fact]
    public async Task DisconnectAsync_WithNullSubscriptions_DoesNotThrow()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();
        var client = new CometBftWebSocketClient(Options.Create(new Core.Options.CometBftWebSocketOptions
        {
            BaseUrl = server.Url,
            SubscribeAckTimeout = TimeSpan.FromMilliseconds(200),
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        }));

        await client.ConnectAsync();

        typeof(CometBftWebSocketClient<string>).GetField("_reconnectionSubscription", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, null);
        typeof(CometBftWebSocketClient<string>).GetField("_messageSubscription", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, null);

        var ex = await Record.ExceptionAsync(() => client.DisconnectAsync());
        Assert.Null(ex);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeNewBlockAsync_WithPreCanceledToken_PropagatesOperationCanceledException()
    {
        await using var server = new PassiveWebSocketServer(sendAck: false);
        await server.StartAsync();
        await using var client = new CometBftWebSocketClient(Options.Create(new Core.Options.CometBftWebSocketOptions
        {
            BaseUrl = server.Url,
            SubscribeAckTimeout = TimeSpan.FromSeconds(1),
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        }));
        await client.ConnectAsync();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SubscribeNewBlockAsync(cts.Token));
    }
}
