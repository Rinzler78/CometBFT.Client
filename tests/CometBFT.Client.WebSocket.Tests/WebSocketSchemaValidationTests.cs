using System.Text.Json;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using CometBFT.Client.WebSocket.Json;
using Websocket.Client;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class WebSocketSchemaValidationTests
{
    public static TheoryData<string, string> MalformedEnvelopes =>
        new()
        {
            {
                "new-block value must be an object",
                """
                {
                  "result": {
                    "data": {
                      "type": "tendermint/event/NewBlock",
                      "value": "oops"
                    }
                  }
                }
                """
            },
            {
                "tx result events must be a JSON object",
                """
                {
                  "result": {
                    "data": {
                      "type": "tendermint/event/Tx",
                      "value": {
                        "TxResult": {
                          "height": "1",
                          "index": 0,
                          "result": {
                            "code": 0,
                            "gas_wanted": "1",
                            "gas_used": "1",
                            "events": []
                          }
                        }
                      }
                    },
                    "events": []
                  }
                }
                """
            },
            {
                "validator updates must be an array",
                """
                {
                  "result": {
                    "data": {
                      "type": "tendermint/event/ValidatorSetUpdates",
                      "value": {
                        "validator_updates": "oops"
                      }
                    }
                  }
                }
                """
            },
            {
                "new block events payload events must be an array",
                """
                {
                  "result": {
                    "data": {
                      "type": "tendermint/event/NewBlockEvents",
                      "value": {
                        "height": "2",
                        "events": "oops"
                      }
                    }
                  }
                }
                """
            },
            {
                "complete proposal round must be numeric",
                """
                {
                  "result": {
                    "data": {
                      "type": "tendermint/event/CompleteProposal",
                      "value": {
                        "height": "3",
                        "round": "oops",
                        "block_id": "BLOCK"
                      }
                    }
                  }
                }
                """
            },
            {
                "new evidence validator must be a string",
                """
                {
                  "result": {
                    "data": {
                      "type": "tendermint/event/NewEvidence",
                      "value": {
                        "height": "4",
                        "evidence_type": "DuplicateVoteEvidence",
                        "validator": true
                      }
                    }
                  }
                }
                """
            }
        };

    [Theory]
    [MemberData(nameof(MalformedEnvelopes))]
    public void Deserialize_WhenSchemaIsInvalid_ThrowsJsonException(string _, string payload)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(payload, CometBftWebSocketJsonContext.Default.WsEnvelope));
    }

    [Theory]
    [MemberData(nameof(MalformedEnvelopes))]
    public void OnMessageReceived_WhenSchemaIsInvalid_FiresErrorOccurredWithJsonException(string _, string payload)
    {
        using var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));
        Exception? captured = null;
        client.ErrorOccurred += (_, args) => captured = args.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage(payload));

        Assert.NotNull(captured);
        Assert.IsType<JsonException>(captured);
    }

    [Theory]
    [MemberData(nameof(MalformedEnvelopes))]
    public void OnMessageReceived_WhenSchemaIsInvalidWithoutErrorSubscriber_DoesNotThrow(string _, string payload)
    {
        using var client = new CometBftWebSocketClient(Options.Create(new CometBftWebSocketOptions()));

        var ex = Record.Exception(() => client.OnMessageReceived(ResponseMessage.TextMessage(payload)));

        Assert.Null(ex);
    }
}
