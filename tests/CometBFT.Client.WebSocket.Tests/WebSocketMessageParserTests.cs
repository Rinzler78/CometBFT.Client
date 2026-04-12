using System.Text.Json.Nodes;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using CometBFT.Client.WebSocket.Internal;
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
    // ── ParseNewBlock ────────────────────────────────────────────────────────

    [Fact]
    public void ParseNewBlock_HappyPath_ReturnsBlock()
    {
        var json = JsonNode.Parse("""
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
        """)!;

        var block = WebSocketMessageParser.ParseNewBlock(json);

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
        var json = JsonNode.Parse("""
        {
          "result": {
            "data": {
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
        """)!;

        var block = WebSocketMessageParser.ParseNewBlock(json);

        Assert.NotNull(block);
        Assert.Empty(block.Txs);
    }

    [Fact]
    public void ParseNewBlock_MissingBlockNode_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"result":{"data":{"value":{}}}}""")!;

        var block = WebSocketMessageParser.ParseNewBlock(json);

        Assert.Null(block);
    }

    // ── ParseNewBlockHeader ──────────────────────────────────────────────────

    [Fact]
    public void ParseNewBlockHeader_HappyPath_ReturnsHeader()
    {
        var json = JsonNode.Parse("""
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
        """)!;

        var header = WebSocketMessageParser.ParseNewBlockHeader(json);

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
        var json = JsonNode.Parse("""{"result":{"data":{"value":{}}}}""")!;

        var header = WebSocketMessageParser.ParseNewBlockHeader(json);

        Assert.Null(header);
    }

    // ── ParseTxResult ────────────────────────────────────────────────────────

    [Fact]
    public void ParseTxResult_HappyPath_ReturnsTxResult()
    {
        var json = JsonNode.Parse("""
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
        """)!;

        var tx = WebSocketMessageParser.ParseTxResult(json);

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
        var json = JsonNode.Parse("""
        {
          "result": {
            "data": {
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
        """)!;

        var tx = WebSocketMessageParser.ParseTxResult(json);

        Assert.NotNull(tx);
        Assert.Empty(tx.Events);
    }

    [Fact]
    public void ParseTxResult_MissingTxResultNode_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"result":{"data":{"value":{}}}}""")!;

        var tx = WebSocketMessageParser.ParseTxResult(json);

        Assert.Null(tx);
    }

    [Fact]
    public void ParseTxResult_MissingTxHashInEvents_ReturnsEmptyHash()
    {
        var json = JsonNode.Parse("""
        {
          "result": {
            "data": {
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
        """)!;

        var tx = WebSocketMessageParser.ParseTxResult(json);

        Assert.NotNull(tx);
        Assert.Equal(string.Empty, tx.Hash);
    }

    // ── ParseVote ────────────────────────────────────────────────────────────

    [Fact]
    public void ParseVote_HappyPath_ReturnsVote()
    {
        var json = JsonNode.Parse("""
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
        """)!;

        var vote = WebSocketMessageParser.ParseVote(json);

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
        var json = JsonNode.Parse("""
        {
          "result": {
            "data": {
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
        """)!;

        var vote = WebSocketMessageParser.ParseVote(json);

        Assert.NotNull(vote);
        Assert.Equal(1, vote.Type);
    }

    [Fact]
    public void ParseVote_MissingVoteNode_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"result":{"data":{"value":{}}}}""")!;

        var vote = WebSocketMessageParser.ParseVote(json);

        Assert.Null(vote);
    }

    // ── ParseValidatorSetUpdates ─────────────────────────────────────────────

    [Fact]
    public void ParseValidatorSetUpdates_HappyPath_ReturnsValidators()
    {
        var json = JsonNode.Parse("""
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
        """)!;

        var validators = WebSocketMessageParser.ParseValidatorSetUpdates(json);

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
        var json = JsonNode.Parse("""
        {
          "result": {
            "data": {
              "value": {
                "validator_updates": []
              }
            }
          }
        }
        """)!;

        var validators = WebSocketMessageParser.ParseValidatorSetUpdates(json);

        Assert.NotNull(validators);
        Assert.Empty(validators);
    }

    [Fact]
    public void ParseValidatorSetUpdates_MissingUpdatesNode_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"result":{"data":{"value":{}}}}""")!;

        var validators = WebSocketMessageParser.ParseValidatorSetUpdates(json);

        Assert.Null(validators);
    }

    // ── OnMessageReceived dispatch ───────────────────────────────────────────

    [Fact]
    public void OnMessageReceived_NewBlock_FiresNewBlockReceivedEvent()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
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
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
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
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
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
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
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
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
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
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
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
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
        var eventFired = false;
        client.NewBlockReceived += (_, _) => eventFired = true;

        // Subscription ACK — result is empty object
        client.OnMessageReceived(ResponseMessage.TextMessage("""{"jsonrpc":"2.0","id":1,"result":{}}"""));

        Assert.False(eventFired);
    }

    [Fact]
    public void OnMessageReceived_MalformedJson_DoesNotThrow()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        var ex = Record.Exception(() =>
            client.OnMessageReceived(ResponseMessage.TextMessage("not valid json {{{")));

        Assert.Null(ex);
    }

    [Fact]
    public void OnMessageReceived_EmptyText_DoesNotThrow()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        var ex = Record.Exception(() =>
            client.OnMessageReceived(ResponseMessage.TextMessage("   ")));

        Assert.Null(ex);
    }
}
