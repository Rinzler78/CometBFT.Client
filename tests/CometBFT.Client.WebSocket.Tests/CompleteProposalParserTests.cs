using System.Text.Json;
using CometBFT.Client.WebSocket.Internal;
using CometBFT.Client.WebSocket.Json;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class CompleteProposalParserTests
{
    private static WsEnvelope Deserialize(string json) =>
        JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsEnvelope)!;

    [Fact]
    public void ParseCompleteProposal_HappyPath_ReturnsData()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/CompleteProposal",
              "value": {
                "height": "100",
                "round": 2,
                "block_id": "BLOCK-HASH-100"
              }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseCompleteProposal((WsCompleteProposalData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(100L, data.Height);
        Assert.Equal(2, data.Round);
        Assert.Equal("BLOCK-HASH-100", data.BlockId);
    }

    [Fact]
    public void ParseCompleteProposal_NullValue_ReturnsNull()
    {
        var result = WebSocketMessageParser.ParseCompleteProposal(new WsCompleteProposalData { Value = null });
        Assert.Null(result);
    }

    [Fact]
    public void ParseCompleteProposal_ZeroRound_ReturnsData()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/CompleteProposal",
              "value": { "height": "1", "round": 0, "block_id": "ID" }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseCompleteProposal((WsCompleteProposalData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(0, data.Round);
    }

    [Fact]
    public void ParseCompleteProposal_InvalidHeight_DefaultsToZero()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/CompleteProposal",
              "value": { "height": "not-a-number", "round": 1, "block_id": "ID" }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseCompleteProposal((WsCompleteProposalData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(0L, data.Height);
    }
}
