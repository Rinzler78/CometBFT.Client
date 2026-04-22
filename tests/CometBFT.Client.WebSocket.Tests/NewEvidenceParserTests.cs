using System.Text.Json;
using CometBFT.Client.WebSocket.Internal;
using CometBFT.Client.WebSocket.Json;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class NewEvidenceParserTests
{
    private static WsEnvelope Deserialize(string json) =>
        JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsEnvelope)!;

    [Fact]
    public void ParseNewEvidence_HappyPath_ReturnsData()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewEvidence",
              "value": {
                "height": "77",
                "evidence_type": "DuplicateVoteEvidence",
                "validator": "VALADDR77"
              }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseNewEvidence((WsNewEvidenceData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(77L, data.Height);
        Assert.Equal("DuplicateVoteEvidence", data.EvidenceType);
        Assert.Equal("VALADDR77", data.Validator);
    }

    [Fact]
    public void ParseNewEvidence_NullValue_ReturnsNull()
    {
        var result = WebSocketMessageParser.ParseNewEvidence(new WsNewEvidenceData { Value = null });
        Assert.Null(result);
    }

    [Fact]
    public void ParseNewEvidence_MissingHeight_DefaultsToZero()
    {
        var envelope = Deserialize("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewEvidence",
              "value": { "evidence_type": "DVE", "validator": "ADDR" }
            }
          }
        }
        """);

        var data = WebSocketMessageParser.ParseNewEvidence((WsNewEvidenceData)envelope.Result!.Data!);

        Assert.NotNull(data);
        Assert.Equal(0L, data.Height);
    }
}
