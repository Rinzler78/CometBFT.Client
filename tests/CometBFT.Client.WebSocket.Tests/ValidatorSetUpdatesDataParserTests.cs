using System.Text.Json;
using CometBFT.Client.WebSocket.Internal;
using CometBFT.Client.WebSocket.Json;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class ValidatorSetUpdatesDataParserTests
{
    private static WsEnvelope Deserialize(string json) =>
        JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsEnvelope)!;

    [Fact]
    public void ParseValidatorSetUpdatesData_HappyPath_ReturnsData()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/ValidatorSetUpdates",
              "value": {
                "validator_updates": [
                  { "address": "ADDR1", "pub_key": { "data": "KEY1" }, "power": "1000" },
                  { "address": "ADDR2", "pub_key": { "data": "KEY2" }, "power": "500" }
                ]
              }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseValidatorSetUpdatesData(
            (WsValidatorSetUpdatesData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(2, data.ValidatorUpdates.Count);
        Assert.Equal("ADDR1", data.ValidatorUpdates[0].Address);
        Assert.Equal(1000L, data.ValidatorUpdates[0].VotingPower);
        Assert.Equal("ADDR2", data.ValidatorUpdates[1].Address);
        Assert.Equal(500L, data.ValidatorUpdates[1].VotingPower);
    }

    [Fact]
    public void ParseValidatorSetUpdatesData_NullValidatorUpdates_ReturnsNull()
    {
        var result = WebSocketMessageParser.ParseValidatorSetUpdatesData(
            new WsValidatorSetUpdatesData { Value = null });
        Assert.Null(result);
    }

    [Fact]
    public void ParseValidatorSetUpdatesData_EmptyList_ReturnsEmptyData()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/ValidatorSetUpdates",
              "value": { "validator_updates": [] }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseValidatorSetUpdatesData(
            (WsValidatorSetUpdatesData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Empty(data.ValidatorUpdates);
    }
}
