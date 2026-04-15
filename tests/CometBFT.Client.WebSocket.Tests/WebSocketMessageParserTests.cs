using System.Text;
using System.Text.Json;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using CometBFT.Client.WebSocket.Internal;
using CometBFT.Client.WebSocket.Json;
using Microsoft.Extensions.Options;
using Websocket.Client;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Unit tests for <see cref="WebSocketMessageParser"/> covering all 5 event types,
/// plus dispatch tests via <see cref="CometBftWebSocketClient.OnMessageReceived"/>.
/// </summary>
public sealed class WebSocketMessageParserTests
{
    // Helper: deserialize an inline JSON string.
    private static WsEnvelope Deserialize(string json) =>
        JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsEnvelope)!;

    // Helper: deserialize a fixture file captured from Cosmos Hub.
    private static WsEnvelope LoadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var json = File.ReadAllText(path, Encoding.UTF8);
        return Deserialize(json);
    }

    // ── ParseNewBlock ────────────────────────────────────────────────────────

    [Fact]
    public void ParseNewBlock_HappyPath_ReturnsBlock()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlock",
              "value": {
                "block_id": { "hash": "BLOCKHASH42" },
                "block": {
                  "header": {
                    "height": "42",
                    "time": "2024-06-01T12:00:00+00:00",
                    "proposer_address": "PROPOSER1"
                  },
                  "data": { "txs": ["dHgx", "dHgy"] }
                }
              }
            }
          }
        }
        """);

        var block = WebSocketMessageParser.ParseNewBlock((WsNewBlockData)envelope.Result!.Data!);

        Assert.NotNull(block);
        Assert.Equal(42L, block.Height);
        Assert.Equal("BLOCKHASH42", block.Hash);
        Assert.Equal("PROPOSER1", block.Proposer);
        Assert.Equal(2, block.Txs.Count);
        Assert.Equal("dHgx", block.Txs[0]);
        Assert.Equal(new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero), block.Time);
    }

    [Fact]
    public void ParseNewBlock_EmptyTxList_ReturnBlockWithNoTxs()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlock",
              "value": {
                "block_id": { "hash": "H" },
                "block": {
                  "header": { "height": "1", "time": "2024-01-01T00:00:00+00:00", "proposer_address": "P" },
                  "data": { "txs": [] }
                }
              }
            }
          }
        }
        """);

        var block = WebSocketMessageParser.ParseNewBlock((WsNewBlockData)envelope.Result!.Data!);

        Assert.NotNull(block);
        Assert.Empty(block.Txs);
    }

    [Fact]
    public void ParseNewBlock_MissingBlockNode_ReturnsNull()
    {
        var data = new WsNewBlockData { Value = new WsNewBlockValue { Block = null } };

        var block = WebSocketMessageParser.ParseNewBlock(data);

        Assert.Null(block);
    }

    // ── ParseNewBlockHeader ──────────────────────────────────────────────────

    [Fact]
    public void ParseNewBlockHeader_HappyPath_ReturnsHeader()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlockHeader",
              "value": {
                "header": {
                  "version": { "block": "11" },
                  "chain_id": "cosmoshub-4",
                  "height": "100",
                  "time": "2024-06-01T12:00:00+00:00",
                  "last_block_id": { "hash": "PREVHASH" },
                  "last_commit_hash": "LC",
                  "data_hash": "DH",
                  "validators_hash": "VH",
                  "next_validators_hash": "NVH",
                  "consensus_hash": "CH",
                  "app_hash": "AH",
                  "last_results_hash": "LRH",
                  "evidence_hash": "EH",
                  "proposer_address": "PROP"
                }
              }
            }
          }
        }
        """);

        var header = WebSocketMessageParser.ParseNewBlockHeader((WsNewBlockHeaderData)envelope.Result!.Data!);

        Assert.NotNull(header);
        Assert.Equal(100L, header.Height);
        Assert.Equal("cosmoshub-4", header.ChainId);
        Assert.Equal("11", header.Version);
        Assert.Equal("PREVHASH", header.LastBlockId);
        Assert.Equal("VH", header.ValidatorsHash);
        Assert.Equal("PROP", header.ProposerAddress);
    }

    [Fact]
    public void ParseNewBlockHeader_MissingHeaderNode_ReturnsNull()
    {
        var data = new WsNewBlockHeaderData { Value = new WsNewBlockHeaderValue { Header = null } };

        var header = WebSocketMessageParser.ParseNewBlockHeader(data);

        Assert.Null(header);
    }

    // ── ParseTxResult ────────────────────────────────────────────────────────

    [Fact]
    public void ParseTxResult_HappyPath_ReturnsTxResult()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/Tx",
              "value": {
                "TxResult": {
                  "height": "88",
                  "index": 2,
                  "result": {
                    "code": 0,
                    "log": "ok",
                    "gas_wanted": "200000",
                    "gas_used": "80000",
                    "events": [
                      {
                        "type": "transfer",
                        "attributes": [
                          { "key": "recipient", "value": "cosmos1abc", "index": true },
                          { "key": "amount", "value": "1000uatom", "index": false }
                        ]
                      }
                    ]
                  }
                }
              }
            },
            "events": {
              "tx.hash": ["TXHASH88"]
            }
          }
        }
        """);

        var tx = WebSocketMessageParser.ParseTxResult(
            (WsTxData)envelope.Result!.Data!,
            envelope.Result.Events);

        Assert.NotNull(tx);
        Assert.Equal("TXHASH88", tx.Hash);
        Assert.Equal(88L, tx.Height);
        Assert.Equal(2, tx.Index);
        Assert.Equal(0u, tx.Code);
        Assert.Equal("ok", tx.Log);
        Assert.Equal(200000L, tx.GasWanted);
        Assert.Equal(80000L, tx.GasUsed);
        Assert.Single(tx.Events);
        Assert.Equal("transfer", tx.Events[0].Type);
        Assert.Equal(2, tx.Events[0].Attributes.Count);
        Assert.Equal("recipient", tx.Events[0].Attributes[0].Key);
        Assert.Equal("cosmos1abc", tx.Events[0].Attributes[0].Value);
        Assert.True(tx.Events[0].Attributes[0].Index);
    }

    [Fact]
    public void ParseTxResult_NoEvents_ReturnsEmptyEventList()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/Tx",
              "value": {
                "TxResult": {
                  "height": "1",
                  "index": 0,
                  "result": { "code": 0, "gas_wanted": "0", "gas_used": "0", "events": [] }
                }
              }
            },
            "events": { "tx.hash": ["HASH1"] }
          }
        }
        """);

        var tx = WebSocketMessageParser.ParseTxResult(
            (WsTxData)envelope.Result!.Data!,
            envelope.Result.Events);

        Assert.NotNull(tx);
        Assert.Empty(tx.Events);
    }

    [Fact]
    public void ParseTxResult_MissingTxResultNode_ReturnsNull()
    {
        var data = new WsTxData { Value = new WsTxValue { TxResult = null } };

        var tx = WebSocketMessageParser.ParseTxResult(data, null);

        Assert.Null(tx);
    }

    [Fact]
    public void ParseTxResult_MissingTxHashInEvents_ReturnsEmptyHash()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/Tx",
              "value": {
                "TxResult": {
                  "height": "5",
                  "index": 0,
                  "result": { "code": 0, "gas_wanted": "0", "gas_used": "0", "events": [] }
                }
              }
            }
          }
        }
        """);

        var tx = WebSocketMessageParser.ParseTxResult(
            (WsTxData)envelope.Result!.Data!,
            envelope.Result.Events);

        Assert.NotNull(tx);
        Assert.Equal(string.Empty, tx.Hash);
    }

    // ── ParseVote ────────────────────────────────────────────────────────────

    [Fact]
    public void ParseVote_HappyPath_ReturnsVote()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/Vote",
              "value": {
                "Vote": {
                  "type": 2,
                  "height": "500",
                  "round": 0,
                  "validator_address": "VALADDR1",
                  "timestamp": "2024-06-01T12:00:00+00:00"
                }
              }
            }
          }
        }
        """);

        var vote = WebSocketMessageParser.ParseVote((WsVoteData)envelope.Result!.Data!);

        Assert.NotNull(vote);
        Assert.Equal(2, vote.Type);
        Assert.Equal(500L, vote.Height);
        Assert.Equal(0, vote.Round);
        Assert.Equal("VALADDR1", vote.ValidatorAddress);
        Assert.Equal(new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero), vote.Timestamp);
    }

    [Fact]
    public void ParseVote_Prevote_TypeIsOne()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/Vote",
              "value": {
                "Vote": {
                  "type": 1,
                  "height": "1",
                  "round": 0,
                  "validator_address": "V",
                  "timestamp": "2024-01-01T00:00:00+00:00"
                }
              }
            }
          }
        }
        """);

        var vote = WebSocketMessageParser.ParseVote((WsVoteData)envelope.Result!.Data!);

        Assert.NotNull(vote);
        Assert.Equal(1, vote.Type);
    }

    [Fact]
    public void ParseVote_MissingVoteNode_ReturnsNull()
    {
        var data = new WsVoteData { Value = new WsVoteValue { Vote = null } };

        var vote = WebSocketMessageParser.ParseVote(data);

        Assert.Null(vote);
    }

    // ── ParseValidatorSetUpdates ─────────────────────────────────────────────

    [Fact]
    public void ParseValidatorSetUpdates_HappyPath_ReturnsValidators()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/ValidatorSetUpdates",
              "value": {
                "validator_updates": [
                  {
                    "address": "VAL1",
                    "pub_key": { "data": "PUBKEY1" },
                    "power": "1000"
                  },
                  {
                    "address": "VAL2",
                    "pub_key": { "data": "PUBKEY2" },
                    "power": "2000"
                  }
                ]
              }
            }
          }
        }
        """);

        var validators = WebSocketMessageParser.ParseValidatorSetUpdates((WsValidatorSetUpdatesData)envelope.Result!.Data!);

        Assert.NotNull(validators);
        Assert.Equal(2, validators.Count);
        Assert.Equal("VAL1", validators[0].Address);
        Assert.Equal("PUBKEY1", validators[0].PubKey);
        Assert.Equal(1000L, validators[0].VotingPower);
        Assert.Equal("VAL2", validators[1].Address);
        Assert.Equal(2000L, validators[1].VotingPower);
    }

    [Fact]
    public void ParseValidatorSetUpdates_EmptyList_ReturnsEmptyCollection()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/ValidatorSetUpdates",
              "value": {
                "validator_updates": []
              }
            }
          }
        }
        """);

        var validators = WebSocketMessageParser.ParseValidatorSetUpdates((WsValidatorSetUpdatesData)envelope.Result!.Data!);

        Assert.NotNull(validators);
        Assert.Empty(validators);
    }

    [Fact]
    public void ParseValidatorSetUpdates_MissingUpdatesNode_ReturnsNull()
    {
        var data = new WsValidatorSetUpdatesData { Value = new WsValidatorSetUpdatesValue { ValidatorUpdates = null } };

        var validators = WebSocketMessageParser.ParseValidatorSetUpdates(data);

        Assert.Null(validators);
    }

    // ── OnMessageReceived dispatch ───────────────────────────────────────────

    [Fact]
    public void OnMessageReceived_NewBlock_FiresNewBlockReceivedEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Block? received = null;
        client.NewBlockReceived += (_, args) => received = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlock",
              "value": {
                "block_id": { "hash": "H1" },
                "block": {
                  "header": { "height": "1", "time": "2024-01-01T00:00:00+00:00", "proposer_address": "P" },
                  "data": { "txs": [] }
                }
              }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal(1L, received.Height);
    }

    [Fact]
    public void OnMessageReceived_NewBlockHeader_FiresNewBlockHeaderReceivedEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        BlockHeader? received = null;
        client.NewBlockHeaderReceived += (_, args) => received = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlockHeader",
              "value": {
                "header": {
                  "version": { "block": "11" },
                  "chain_id": "testnet",
                  "height": "77",
                  "time": "2024-06-01T00:00:00+00:00",
                  "last_block_id": { "hash": "" },
                  "last_commit_hash": "", "data_hash": "", "validators_hash": "",
                  "next_validators_hash": "", "consensus_hash": "", "app_hash": "",
                  "last_results_hash": "", "evidence_hash": "", "proposer_address": ""
                }
              }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal(77L, received.Height);
        Assert.Equal("testnet", received.ChainId);
    }

    [Fact]
    public void OnMessageReceived_Tx_FiresTxExecutedEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        TxResult? received = null;
        client.TxExecuted += (_, args) => received = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/Tx",
              "value": {
                "TxResult": {
                  "height": "55",
                  "index": 0,
                  "result": { "code": 0, "gas_wanted": "100", "gas_used": "50", "events": [] }
                }
              }
            },
            "events": { "tx.hash": ["TXHASH55"] }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal("TXHASH55", received.Hash);
        Assert.Equal(55L, received.Height);
    }

    [Fact]
    public void OnMessageReceived_Vote_FiresVoteReceivedEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Vote? received = null;
        client.VoteReceived += (_, args) => received = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/Vote",
              "value": {
                "Vote": {
                  "type": 2,
                  "height": "300",
                  "round": 0,
                  "validator_address": "VALIDATOR1",
                  "timestamp": "2024-06-01T12:00:00+00:00"
                }
              }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal(300L, received.Height);
        Assert.Equal("VALIDATOR1", received.ValidatorAddress);
    }

    [Fact]
    public void OnMessageReceived_ValidatorSetUpdates_FiresValidatorSetUpdatedEvent()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        IReadOnlyList<Validator>? received = null;
        client.ValidatorSetUpdated += (_, args) => received = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/ValidatorSetUpdates",
              "value": {
                "validator_updates": [
                  { "address": "V1", "pub_key": { "data": "PK1" }, "power": "500" }
                ]
              }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Single(received);
        Assert.Equal("V1", received[0].Address);
        Assert.Equal(500L, received[0].VotingPower);
    }

    [Fact]
    public void OnMessageReceived_UnknownEventType_NoEventFired()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var eventFired = false;
        client.NewBlockReceived += (_, _) => eventFired = true;
        client.NewBlockHeaderReceived += (_, _) => eventFired = true;
        client.TxExecuted += (_, _) => eventFired = true;
        client.VoteReceived += (_, _) => eventFired = true;
        client.ValidatorSetUpdated += (_, _) => eventFired = true;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {"result":{"data":{"type":"tendermint/event/Unknown","value":{}}}}
        """));

        Assert.False(eventFired);
    }

    [Fact]
    public void OnMessageReceived_EmptyResult_NoEventFired()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var eventFired = false;
        client.NewBlockReceived += (_, _) => eventFired = true;

        // Subscription ACK — result is empty object; no domain event should fire
        client.OnMessageReceived(ResponseMessage.TextMessage("""{"jsonrpc":"2.0","id":1,"result":{}}"""));

        Assert.False(eventFired);
    }

    [Fact]
    public async Task OnMessageReceived_SubscribeAck_CompletesPendingTask()
    {
        // Arrange: register a pending ack as SendSubscribeAsync would
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client._pendingAcks[1] = tcs;

        // Act: simulate server ack for request id=1
        client.OnMessageReceived(ResponseMessage.TextMessage("""{"jsonrpc":"2.0","id":1,"result":{}}"""));

        // Assert: the pending task is completed and its result is true
        Assert.True(tcs.Task.IsCompleted);
        Assert.True(await tcs.Task);
    }

    [Fact]
    public void OnMessageReceived_SubscribeAck_UnknownId_DoesNotThrow()
    {
        // Ack for an id that has no registered pending task must be silently ignored
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));

        var ex = Record.Exception(() =>
            client.OnMessageReceived(ResponseMessage.TextMessage("""{"jsonrpc":"2.0","id":99,"result":{}}""")));

        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_EventWithIdZero_IsDispatchedAsEvent()
    {
        // Events arrive with id=0 and must NOT be treated as acks
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        var eventFired = false;
        client.NewBlockReceived += (_, _) => eventFired = true;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "jsonrpc": "2.0",
          "id": 0,
          "result": {
            "data": {
              "type": "tendermint/event/NewBlock",
              "value": {
                "block_id": { "hash": "HASH1" },
                "block": {
                  "header": { "height": "10", "time": "2024-01-01T00:00:00+00:00", "proposer_address": "P" },
                  "data": { "txs": [] }
                }
              }
            }
          }
        }
        """));

        Assert.True(eventFired);
    }

    [Fact]
    public void OnMessageReceived_MalformedJson_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));

        var ex = Record.Exception(() =>
            client.OnMessageReceived(ResponseMessage.TextMessage("not valid json {{{")));

        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_EmptyText_DoesNotThrow()
    {
        var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));

        var ex = Record.Exception(() =>
            client.OnMessageReceived(ResponseMessage.TextMessage("   ")));

        Assert.Null(ex);
    }

    // ── Outgoing request serialization ───────────────────────────────────────

    [Fact]
    public void WsSubscribeRequest_Serializes_CorrectJsonRpcShape()
    {
        var request = new WsSubscribeRequest
        {
            Id = 3,
            Params = new WsSubscribeParams { Query = "tm.event='NewBlock'" },
        };

        var json = JsonSerializer.Serialize(request, CometBftWebSocketJsonContext.Default.WsSubscribeRequest);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal("subscribe", root.GetProperty("method").GetString());
        Assert.Equal(3, root.GetProperty("id").GetInt32());
        Assert.Equal("tm.event='NewBlock'", root.GetProperty("params").GetProperty("query").GetString());
    }

    [Fact]
    public void WsUnsubscribeAllRequest_Serializes_CorrectJsonRpcShape()
    {
        var request = new WsUnsubscribeAllRequest { Id = 7 };

        var json = JsonSerializer.Serialize(request, CometBftWebSocketJsonContext.Default.WsUnsubscribeAllRequest);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal("unsubscribe_all", root.GetProperty("method").GetString());
        Assert.Equal(7, root.GetProperty("id").GetInt32());
        Assert.True(root.TryGetProperty("params", out _));
    }

    // ── Fixture-based tests (real Cosmos Hub payloads) ───────────────────────
    // Captured live from wss://cosmoshub.tendermintrpc.lava.build at block 30674661.
    // These tests validate that the full deserialization → domain-object pipeline
    // handles real network payloads — not just the shapes we imagined.

    [Fact]
    public void ParseNewBlock_RealFixture_DeserializesWithoutError()
    {
        var envelope = LoadFixture("new_block_with_txs.json");
        var data = Assert.IsType<WsNewBlockData>(envelope.Result!.Data);

        var block = WebSocketMessageParser.ParseNewBlock(data);

        Assert.NotNull(block);
    }

    [Fact]
    public void ParseNewBlock_RealFixture_HeightIsCorrect()
    {
        var envelope = LoadFixture("new_block_with_txs.json");
        var data = Assert.IsType<WsNewBlockData>(envelope.Result!.Data);

        var block = WebSocketMessageParser.ParseNewBlock(data);

        Assert.Equal(30_674_661L, block!.Height);
    }

    [Fact]
    public void ParseNewBlock_RealFixture_HashIsPopulated()
    {
        var envelope = LoadFixture("new_block_with_txs.json");
        var data = Assert.IsType<WsNewBlockData>(envelope.Result!.Data);

        var block = WebSocketMessageParser.ParseNewBlock(data);

        Assert.Equal("7656AFA79A263C93CC20A1F0775DE3F48193FA4BF25D8B8C7CCB4ED8C83C1DC2", block!.Hash);
    }

    [Fact]
    public void ParseNewBlock_RealFixture_ProposerAddressIsCorrect()
    {
        var envelope = LoadFixture("new_block_with_txs.json");
        var data = Assert.IsType<WsNewBlockData>(envelope.Result!.Data);

        var block = WebSocketMessageParser.ParseNewBlock(data);

        Assert.Equal("638C11545DF20961BDE0373D1602ECECB3BC6CD0", block!.Proposer);
    }

    [Fact]
    public void ParseNewBlock_RealFixture_TimeIsParsed()
    {
        var envelope = LoadFixture("new_block_with_txs.json");
        var data = Assert.IsType<WsNewBlockData>(envelope.Result!.Data);

        var block = WebSocketMessageParser.ParseNewBlock(data);

        // Nanosecond precision in the wire timestamp is truncated to microsecond by DateTimeOffset.
        Assert.Equal(2026, block!.Time.Year);
        Assert.Equal(4, block.Time.Month);
        Assert.Equal(15, block.Time.Day);
        Assert.Equal(13, block.Time.Hour);
        Assert.Equal(2, block.Time.Minute);
    }

    [Fact]
    public void ParseNewBlock_RealFixture_HasAtLeastOneTx()
    {
        var envelope = LoadFixture("new_block_with_txs.json");
        var data = Assert.IsType<WsNewBlockData>(envelope.Result!.Data);

        var block = WebSocketMessageParser.ParseNewBlock(data);

        Assert.NotEmpty(block!.Txs);
        Assert.NotEmpty(block.Txs[0]); // base64-encoded bytes, never empty
    }

    [Fact]
    public void ParseTxResult_RealFixture_DeserializesWithoutError()
    {
        var envelope = LoadFixture("tx_event.json");
        var data = Assert.IsType<WsTxData>(envelope.Result!.Data);

        var tx = WebSocketMessageParser.ParseTxResult(data, envelope.Result.Events);

        Assert.NotNull(tx);
    }

    [Fact]
    public void ParseTxResult_RealFixture_HeightIsCorrect()
    {
        var envelope = LoadFixture("tx_event.json");
        var data = Assert.IsType<WsTxData>(envelope.Result!.Data);

        var tx = WebSocketMessageParser.ParseTxResult(data, envelope.Result.Events);

        Assert.Equal(30_674_661L, tx!.Height);
    }

    [Fact]
    public void ParseTxResult_RealFixture_HashFromTopLevelEvents()
    {
        var envelope = LoadFixture("tx_event.json");
        var data = Assert.IsType<WsTxData>(envelope.Result!.Data);

        var tx = WebSocketMessageParser.ParseTxResult(data, envelope.Result.Events);

        Assert.Equal("32C6A86AED67254A33F15FEEB78D307A64F7803D5B33D7D3D9DFFDB5E7750E7B", tx!.Hash);
    }

    [Fact]
    public void ParseTxResult_RealFixture_GasFieldsAreCorrect()
    {
        var envelope = LoadFixture("tx_event.json");
        var data = Assert.IsType<WsTxData>(envelope.Result!.Data);

        var tx = WebSocketMessageParser.ParseTxResult(data, envelope.Result.Events);

        Assert.Equal(134_178L, tx!.GasWanted);
        Assert.Equal(99_965L, tx!.GasUsed);
    }

    [Fact]
    public void ParseTxResult_RealFixture_CodeIsZeroForSuccessfulTx()
    {
        var envelope = LoadFixture("tx_event.json");
        var data = Assert.IsType<WsTxData>(envelope.Result!.Data);

        var tx = WebSocketMessageParser.ParseTxResult(data, envelope.Result.Events);

        Assert.Equal(0u, tx!.Code);
    }

    [Fact]
    public void ParseTxResult_RealFixture_AbciEventsArePopulated()
    {
        var envelope = LoadFixture("tx_event.json");
        var data = Assert.IsType<WsTxData>(envelope.Result!.Data);

        var tx = WebSocketMessageParser.ParseTxResult(data, envelope.Result.Events);

        // 17 ABCI events in this MsgSend transaction
        Assert.Equal(17, tx!.Events.Count);
        Assert.All(tx.Events, e => Assert.NotEmpty(e.Type));
    }

    [Fact]
    public void ParseVote_RealFixture_DeserializesWithoutError()
    {
        var envelope = LoadFixture("vote_event.json");
        var data = Assert.IsType<WsVoteData>(envelope.Result!.Data);

        var vote = WebSocketMessageParser.ParseVote(data);

        Assert.NotNull(vote);
    }

    [Fact]
    public void ParseVote_RealFixture_HeightIsCorrect()
    {
        var envelope = LoadFixture("vote_event.json");
        var data = Assert.IsType<WsVoteData>(envelope.Result!.Data);

        var vote = WebSocketMessageParser.ParseVote(data);

        Assert.Equal(30_674_660L, vote!.Height);
    }

    [Fact]
    public void ParseVote_RealFixture_TypeAndRoundAreCorrect()
    {
        var envelope = LoadFixture("vote_event.json");
        var data = Assert.IsType<WsVoteData>(envelope.Result!.Data);

        var vote = WebSocketMessageParser.ParseVote(data);

        Assert.Equal(1, vote!.Type);   // prevote
        Assert.Equal(0, vote.Round);
    }

    [Fact]
    public void ParseVote_RealFixture_ValidatorAddressIsCorrect()
    {
        var envelope = LoadFixture("vote_event.json");
        var data = Assert.IsType<WsVoteData>(envelope.Result!.Data);

        var vote = WebSocketMessageParser.ParseVote(data);

        Assert.Equal("B2336DC86A74A6F8552D7F686AC0983EF4E0B0CE", vote!.ValidatorAddress);
    }

    [Fact]
    public void ParseVote_RealFixture_TimestampIsParsed()
    {
        var envelope = LoadFixture("vote_event.json");
        var data = Assert.IsType<WsVoteData>(envelope.Result!.Data);

        var vote = WebSocketMessageParser.ParseVote(data);

        // Prevote timestamp from consensus round — verify date and time components.
        Assert.Equal(2026, vote!.Timestamp.Year);
        Assert.Equal(4, vote.Timestamp.Month);
        Assert.Equal(15, vote.Timestamp.Day);
        Assert.Equal(13, vote.Timestamp.Hour);
        Assert.Equal(2, vote.Timestamp.Minute);
    }

    [Fact]
    public void ParseVote_RealFixture_PrevoteWithEmptyBlockId_StillParses()
    {
        // vote_event.json is a prevote (type=1) with an empty block_id.hash —
        // a normal condition during the prevote phase when the block is not yet locked.
        var envelope = LoadFixture("vote_event.json");
        var data = Assert.IsType<WsVoteData>(envelope.Result!.Data);

        var vote = WebSocketMessageParser.ParseVote(data);

        // Parser must return a valid Vote even with an empty block_id in the wire payload.
        Assert.NotNull(vote);
        Assert.Equal(1, vote.Type);
    }
}
