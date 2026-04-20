using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Options;
using NSubstitute;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using Websocket.Client;
using Xunit;
using WsJson = CometBFT.Client.WebSocket.Json;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class WebSocketBranchCoverageTests
{
    private static IOptions<CometBftWebSocketOptions> OptionsFor(string url, int ackMilliseconds = 200) =>
        Options.Create(new CometBftWebSocketOptions
        {
            BaseUrl = url,
            SubscribeAckTimeout = TimeSpan.FromMilliseconds(ackMilliseconds),
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        });

    [Fact]
    public void ParseNewBlock_InvalidAndMissingFields_DefaultToSafeValues()
    {
        var data = new WsJson.WsNewBlockData
        {
            Value = new WsJson.WsNewBlockValue
            {
                BlockId = null,
                Block = new WsJson.WsBlock
                {
                    Header = new WsJson.WsBlockHeader { Height = "oops", ProposerAddress = string.Empty },
                    Data = null,
                },
            },
        };

        var block = Internal.WebSocketMessageParser.ParseNewBlock(data);

        Assert.NotNull(block);
        Assert.Equal(0L, block.Height);
        Assert.Equal(string.Empty, block.Hash);
        Assert.Equal(string.Empty, block.Proposer);
        Assert.Equal(DateTimeOffset.MinValue, block.Time);
        Assert.Empty(block.Txs);
    }

    [Fact]
    public void ParseNewBlock_NullValue_ReturnsNull()
    {
        var block = Internal.WebSocketMessageParser.ParseNewBlock(new WsJson.WsNewBlockData { Value = null });
        Assert.Null(block);
    }

    [Fact]
    public void ParseNewBlock_NullHeader_DefaultsToSafeValues()
    {
        var block = Internal.WebSocketMessageParser.ParseNewBlock(new WsJson.WsNewBlockData
        {
            Value = new WsJson.WsNewBlockValue
            {
                BlockId = new WsJson.WsBlockId { Hash = "HASH" },
                Block = new WsJson.WsBlock
                {
                    Header = null,
                    Data = new WsJson.WsBlockTxData { Txs = ["AQ=="] },
                },
            },
        });

        Assert.NotNull(block);
        Assert.Equal(0L, block.Height);
        Assert.Equal("HASH", block.Hash);
        Assert.Equal("AQ==", block.Txs[0]);
    }

    [Fact]
    public void ParseNewBlockHeader_InvalidAndMissingFields_DefaultToSafeValues()
    {
        var data = new WsJson.WsNewBlockHeaderData
        {
            Value = new WsJson.WsNewBlockHeaderValue
            {
                Header = new WsJson.WsFullBlockHeader
                {
                    Height = "oops",
                    Time = "not-a-date",
                    Version = null,
                    LastBlockId = null,
                },
            },
        };

        var header = Internal.WebSocketMessageParser.ParseNewBlockHeader(data);

        Assert.NotNull(header);
        Assert.Equal(0L, header.Height);
        Assert.Equal(DateTimeOffset.MinValue, header.Time);
        Assert.Equal(string.Empty, header.Version);
        Assert.Equal(string.Empty, header.LastBlockId);
        Assert.Equal(string.Empty, header.ProposerAddress);
    }

    [Fact]
    public void ParseTxResult_InvalidAndMissingFields_DefaultToSafeValues()
    {
        var data = new WsJson.WsTxData
        {
            Value = new WsJson.WsTxValue
            {
                TxResult = new WsJson.WsTxResult
                {
                    Height = "oops",
                    Result = new WsJson.WsExecResult
                    {
                        GasWanted = "oops",
                        GasUsed = "oops",
                        Events = null,
                    },
                },
            },
        };

        var tx = Internal.WebSocketMessageParser.ParseTxResult(data, new Dictionary<string, List<string>>());

        Assert.NotNull(tx);
        Assert.Equal(0L, tx.Height);
        Assert.Equal(0L, tx.GasWanted);
        Assert.Equal(0L, tx.GasUsed);
        Assert.Equal(string.Empty, tx.Hash);
        Assert.Empty(tx.Events);
    }

    [Fact]
    public void ParseTxResult_NullExecutionResult_DefaultsToSafeValues()
    {
        var data = new WsJson.WsTxData
        {
            Value = new WsJson.WsTxValue
            {
                TxResult = new WsJson.WsTxResult
                {
                    Height = "12",
                    Index = 1,
                    Result = null,
                },
            },
        };

        var tx = Internal.WebSocketMessageParser.ParseTxResult(data, new Dictionary<string, List<string>> { ["tx.hash"] = ["HASH"] });

        Assert.NotNull(tx);
        Assert.Equal(12L, tx.Height);
        Assert.Equal(0u, tx.Code);
        Assert.Empty(tx.Events);
    }

    [Fact]
    public void ParseVote_InvalidFields_DefaultToSafeValues()
    {
        var data = new WsJson.WsVoteData
        {
            Value = new WsJson.WsVoteValue
            {
                Vote = new WsJson.WsVote
                {
                    Height = "oops",
                    Timestamp = "not-a-date",
                    ValidatorAddress = string.Empty,
                },
            },
        };

        var vote = Internal.WebSocketMessageParser.ParseVote(data);

        Assert.NotNull(vote);
        Assert.Equal(0L, vote.Height);
        Assert.Equal(DateTimeOffset.MinValue, vote.Timestamp);
        Assert.Equal(string.Empty, vote.ValidatorAddress);
    }

    [Fact]
    public void ParseVote_NullValue_ReturnsNull()
    {
        var vote = Internal.WebSocketMessageParser.ParseVote(new WsJson.WsVoteData { Value = null });
        Assert.Null(vote);
    }

    [Fact]
    public void ParseValidatorSetUpdates_InvalidAndMissingFields_DefaultToSafeValues()
    {
        var data = new WsJson.WsValidatorSetUpdatesData
        {
            Value = new WsJson.WsValidatorSetUpdatesValue
            {
                ValidatorUpdates =
                [
                    new WsJson.WsValidator
                    {
                        Address = "VAL1",
                        PubKey = null,
                        Power = "oops",
                    },
                ],
            },
        };

        var validators = Internal.WebSocketMessageParser.ParseValidatorSetUpdates(data);

        Assert.NotNull(validators);
        Assert.Single(validators);
        Assert.Equal(string.Empty, validators[0].PubKey);
        Assert.Equal(0L, validators[0].VotingPower);
    }

    [Fact]
    public void OnMessageReceived_NullEnvelope_DoesNothing()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("null")));
        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_AckWithData_CompletesAckAndPublishesEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var ack = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[1] = ack;

        Block<string>? received = null;
        client.NewBlockReceived += (_, args) => received = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "jsonrpc":"2.0",
          "id":1,
          "result":{
            "data":{
              "type":"tendermint/event/NewBlock",
              "value":{
                "block_id":{"hash":"AA"},
                "block":{
                  "header":{"height":"7","time":"2024-01-01T00:00:00Z","proposer_address":"P"},
                  "data":{"txs":[]}
                }
              }
            }
          }
        }
        """));

        Assert.True(ack.Task.IsCompletedSuccessfully);
        Assert.NotNull(received);
        Assert.Equal(7L, received.Height);
    }

    [Fact]
    public void OnMessageReceived_WhenEventHandlerThrows_FiresErrorOccurred()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Exception? captured = null;
        client.NewBlockReceived += (_, _) => throw new InvalidOperationException("handler failed");
        client.ErrorOccurred += (_, args) => captured = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{
              "type":"tendermint/event/NewBlock",
              "value":{
                "block_id":{"hash":"AA"},
                "block":{
                  "header":{"height":"1","time":"2024-01-01T00:00:00Z","proposer_address":"P"},
                  "data":{"txs":[]}
                }
              }
            }
          }
        }
        """));

        Assert.NotNull(captured);
        Assert.Equal("handler failed", captured!.Message);
    }

    [Fact]
    public void OnMessageReceived_WhenTxHandlerThrows_FiresErrorOccurred()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Exception? captured = null;
        client.TxExecuted += (_, _) => throw new InvalidOperationException("tx handler failed");
        client.ErrorOccurred += (_, args) => captured = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{
              "type":"tendermint/event/Tx",
              "value":{
                "TxResult":{"height":"1","index":0,"result":{"code":0,"gas_wanted":"1","gas_used":"1","events":[]}}
              }
            },
            "events":{"tx.hash":["HASH"]}
          }
        }
        """));

        Assert.NotNull(captured);
        Assert.Equal("tx handler failed", captured!.Message);
    }

    [Fact]
    public void OnMessageReceived_NewBlockHeaderWithoutHeader_DoesNotFireEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var fired = false;
        client.NewBlockHeaderReceived += (_, _) => fired = true;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/NewBlockHeader","value":{}}
          }
        }
        """));

        Assert.False(fired);
    }

    [Fact]
    public void OnMessageReceived_NewBlockHeaderWithoutSubscribers_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/NewBlockHeader","value":{"header":{"height":"2","time":"2024-01-01T00:00:00Z"}}}
          }
        }
        """)));
        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_WhenHeaderHandlerThrows_FiresErrorOccurred()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Exception? captured = null;
        client.NewBlockHeaderReceived += (_, _) => throw new InvalidOperationException("header handler failed");
        client.ErrorOccurred += (_, args) => captured = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/NewBlockHeader","value":{"header":{"height":"2","time":"2024-01-01T00:00:00Z"}}}
          }
        }
        """));

        Assert.NotNull(captured);
        Assert.Equal("header handler failed", captured!.Message);
    }

    [Fact]
    public void OnMessageReceived_WhenVoteHandlerThrows_FiresErrorOccurred()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Exception? captured = null;
        client.VoteReceived += (_, _) => throw new InvalidOperationException("vote handler failed");
        client.ErrorOccurred += (_, args) => captured = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/Vote","value":{"Vote":{"height":"1","timestamp":"2024-01-01T00:00:00Z"}}}
          }
        }
        """));

        Assert.NotNull(captured);
        Assert.Equal("vote handler failed", captured!.Message);
    }

    [Fact]
    public void OnMessageReceived_WhenValidatorHandlerThrows_FiresErrorOccurred()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Exception? captured = null;
        client.ValidatorSetUpdated += (_, _) => throw new InvalidOperationException("validator handler failed");
        client.ErrorOccurred += (_, args) => captured = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/ValidatorSetUpdates","value":{"validator_updates":[{"address":"VAL","power":"1"}]}}
          }
        }
        """));

        Assert.NotNull(captured);
        Assert.Equal("validator handler failed", captured!.Message);
    }

    [Fact]
    public async Task SubscribeNewBlockAsync_WhenAckTimesOut_FiresErrorOccurred()
    {
        await using var server = new PassiveWebSocketServer(sendAck: false);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(OptionsFor(server.Url));
        Exception? captured = null;
        client.ErrorOccurred += (_, args) => captured = args.Value;

        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();
        await Task.Delay(300);

        Assert.NotNull(captured);
        Assert.Contains("ACK", captured!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnReconnected_WithActiveSubscription_ResendsSubscribeMessage()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(OptionsFor(server.Url, 500));
        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();
        await Task.Delay(150);

        client.OnReconnected(new ReconnectionInfo(ReconnectionType.Lost));
        await Task.Delay(150);

        var subscribeMessages = server.Messages.Count(message => message.Contains("NewBlock", StringComparison.Ordinal));
        Assert.True(subscribeMessages >= 2, $"Expected at least 2 subscribe messages, got {subscribeMessages}.");
    }

    [Fact]
    public void OnReconnected_WithActiveSubscriptionAndNullClient_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var field = typeof(CometBftWebSocketClient<string>).GetField("_activeSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var subscriptions = (ConcurrentDictionary<string, byte>)field.GetValue(client)!;
        subscriptions.TryAdd("tm.event='NewBlock'", 0);

        var ex = Record.Exception(() => client.OnReconnected(new ReconnectionInfo(ReconnectionType.Lost)));
        Assert.Null(ex);
    }

    [Fact]
    public async Task DisconnectAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        await client.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.DisconnectAsync());
    }

    [Fact]
    public async Task SubscribeNewBlockAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        await client.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SubscribeNewBlockAsync());
    }

    [Fact]
    public async Task UnsubscribeAllAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        await client.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.UnsubscribeAllAsync());
    }

    [Fact]
    public async Task DisconnectAsync_WithCanceledToken_ThrowsOperationCanceledException()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DisconnectAsync(cts.Token));
    }

    [Fact]
    public async Task ConnectAsync_WithCanceledToken_ThrowsOperationCanceledException()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();
        await using var client = new CometBftWebSocketClient(OptionsFor(server.Url));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConnectAsync(cts.Token));
    }

    [Fact]
    public async Task ConnectAsync_WhenFactoryThrows_WrapsInCometBftWebSocketException()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()), new ThrowingFactory());
        var ex = await Assert.ThrowsAsync<CometBFT.Client.Core.Exceptions.CometBftWebSocketException>(() => client.ConnectAsync());
        Assert.Contains("Failed to connect", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubscribeNewBlockAsync_WhenUserCancellationRequested_PropagatesOperationCanceledException()
    {
        await using var server = new PassiveWebSocketServer(sendAck: false);
        await server.StartAsync();
        await using var client = new CometBftWebSocketClient(OptionsFor(server.Url, 1000));
        await client.ConnectAsync();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SubscribeNewBlockAsync(cts.Token));
    }

    [Fact]
    public void OnMessageReceived_TxWithoutTxResult_DoesNotFireTxEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var fired = false;
        client.TxExecuted += (_, _) => fired = true;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/Tx","value":{}},
            "events":{"tx.hash":["HASH"]}
          }
        }
        """));

        Assert.False(fired);
    }

    [Fact]
    public void OnMessageReceived_TxWithoutSubscribers_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{
              "type":"tendermint/event/Tx",
              "value":{"TxResult":{"height":"1","index":0,"result":{"code":0,"gas_wanted":"1","gas_used":"1","events":[]}}}
            },
            "events":{"tx.hash":["HASH"]}
          }
        }
        """)));
        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_NewBlockWithoutBlock_DoesNotFireEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var fired = false;
        client.NewBlockReceived += (_, _) => fired = true;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/NewBlock","value":{}}
          }
        }
        """));

        Assert.False(fired);
    }

    [Fact]
    public void OnMessageReceived_NewBlockWithoutSubscribers_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{
              "type":"tendermint/event/NewBlock",
              "value":{
                "block_id":{"hash":"AA"},
                "block":{
                  "header":{"height":"1","time":"2024-01-01T00:00:00Z","proposer_address":"P"},
                  "data":{"txs":[]}
                }
              }
            }
          }
        }
        """)));
        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_WithTypedCodec_DecodesBlockAndTransaction()
    {
        var codec = NSubstitute.Substitute.For<CometBFT.Client.Core.Codecs.ITxCodec<string>>();
        codec.Decode(NSubstitute.Arg.Any<byte[]>()).Returns(call => Convert.ToBase64String((byte[])call[0]!));
        var client = new CometBftWebSocketClient<string>(Options.Create(new CometBftWebSocketOptions()), new NeverConnectFactory(), codec);

        Block<string>? block = null;
        TxResult<string>? tx = null;
        client.NewBlockReceived += (_, args) => block = args.Value;
        client.TxExecuted += (_, args) => tx = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{
              "type":"tendermint/event/NewBlock",
              "value":{
                "block_id":{"hash":"AA"},
                "block":{
                  "header":{"height":"1","time":"2024-01-01T00:00:00Z","proposer_address":"P"},
                  "data":{"txs":["AQ=="]}
                }
              }
            }
          }
        }
        """));

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{
              "type":"tendermint/event/Tx",
              "value":{
                "TxResult":{"height":"1","index":0,"result":{"code":0,"gas_wanted":"1","gas_used":"1","events":[]}}
              }
            },
            "events":{"tx.hash":["HASH"]}
          }
        }
        """));

        Assert.NotNull(block);
        Assert.NotNull(tx);
    }

    [Fact]
    public void OnMessageReceived_VoteWithoutVote_DoesNotFireVoteEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var fired = false;
        client.VoteReceived += (_, _) => fired = true;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/Vote","value":{}}
          }
        }
        """));

        Assert.False(fired);
    }

    [Fact]
    public void OnMessageReceived_VoteWithoutSubscribers_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/Vote","value":{"Vote":{"height":"1","timestamp":"2024-01-01T00:00:00Z"}}}
          }
        }
        """)));
        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_ValidatorSetUpdatesWithoutUpdates_DoesNotFireEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var fired = false;
        client.ValidatorSetUpdated += (_, _) => fired = true;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/ValidatorSetUpdates","value":{}}
          }
        }
        """));

        Assert.False(fired);
    }

    [Fact]
    public void OnMessageReceived_ValidatorSetUpdatesWithoutSubscribers_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result":{
            "data":{"type":"tendermint/event/ValidatorSetUpdates","value":{"validator_updates":[{"address":"VAL","power":"1"}]}}
          }
        }
        """)));
        Assert.Null(ex);
    }
}

internal sealed class NeverConnectFactory : CometBFT.Client.WebSocket.Internal.IWebSocketClientFactory
{
    public WebsocketClient Create(Uri uri, CometBftWebSocketOptions options) =>
        throw new InvalidOperationException("no real server in unit tests");
}

internal sealed class ThrowingFactory : CometBFT.Client.WebSocket.Internal.IWebSocketClientFactory
{
    public WebsocketClient Create(Uri uri, CometBftWebSocketOptions options) =>
        throw new InvalidOperationException("factory boom");
}

internal sealed class PassiveWebSocketServer : IAsyncDisposable
{
    private readonly bool _sendAck;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
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
        Url = $"ws://127.0.0.1:{port}/";
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _serverTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
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

                if (!_sendAck)
                {
                    continue;
                }

                var id = ExtractId(text);
                if (id is null)
                {
                    continue;
                }

                var ackBytes = Encoding.UTF8.GetBytes($"{{\"jsonrpc\":\"2.0\",\"id\":{id.Value},\"result\":{{}}}}");
                await socket.SendAsync(ackBytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Best effort server for tests.
        }
    }

    private static int? ExtractId(string json)
    {
        try
        {
            var marker = "\"id\":";
            var index = json.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            index += marker.Length;
            var end = index;
            while (end < json.Length && char.IsDigit(json[end]))
            {
                end++;
            }

            return int.Parse(json[index..end], System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { }
        try { await _serverTask.ConfigureAwait(false); } catch { }
        _listener.Close();
        _cts.Dispose();
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
