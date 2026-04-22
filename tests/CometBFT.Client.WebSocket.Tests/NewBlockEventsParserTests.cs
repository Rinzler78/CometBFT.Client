using System.Text.Json;
using CometBFT.Client.WebSocket.Internal;
using CometBFT.Client.WebSocket.Json;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class NewBlockEventsParserTests
{
    private static WsEnvelope Deserialize(string json) =>
        JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsEnvelope)!;

    [Fact]
    public void ParseNewBlockEvents_HappyPath_ReturnsData()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlockEvents",
              "value": {
                "height": "42",
                "header": {
                  "version": { "block": "11" },
                  "chain_id": "cosmoshub-4",
                  "height": "42",
                  "time": "2024-06-01T12:00:00+00:00",
                  "proposer_address": "PROPOSER"
                },
                "events": [
                  {
                    "type": "ibc_transfer",
                    "attributes": [
                      { "key": "recipient", "value": "cosmos1abc", "index": true }
                    ]
                  },
                  {
                    "type": "transfer",
                    "attributes": []
                  }
                ]
              }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseNewBlockEvents((WsNewBlockEventsData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(42L, data.Height);
        Assert.Equal(42L, data.Header.Height);
        Assert.Equal("cosmoshub-4", data.Header.ChainId);
        Assert.Equal("PROPOSER", data.Header.ProposerAddress);
        Assert.Equal(2, data.Events.Count);
        Assert.Equal("ibc_transfer", data.Events[0].Type);
        Assert.Single(data.Events[0].Attributes);
        Assert.Equal("recipient", data.Events[0].Attributes[0].Key);
        Assert.Equal("transfer", data.Events[1].Type);
    }

    [Fact]
    public void ParseNewBlockEvents_NullValue_ReturnsNull()
    {
        var data = new WsNewBlockEventsData { Value = null };
        var result = WebSocketMessageParser.ParseNewBlockEvents(data);
        Assert.Null(result);
    }

    [Fact]
    public void ParseNewBlockEvents_EmptyEvents_ReturnsDataWithNoEvents()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlockEvents",
              "value": {
                "height": "1",
                "events": []
              }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseNewBlockEvents((WsNewBlockEventsData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(1L, data.Height);
        Assert.Empty(data.Events);
    }
}
