using System.Text.Json;
using CometBFT.Client.WebSocket.Json;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Exercises each type registered in <see cref="CometBftWebSocketJsonContext"/> so that
/// the STJ source-generated TypeInfo paths are measured by coverage.
/// </summary>
public sealed class WsJsonContextCoverageTests
{
    [Fact]
    public void WsSubscribeRequest_RoundTrip()
    {
        var req = new WsSubscribeRequest
        {
            Id = 1,
            Params = new WsSubscribeParams { Query = "tm.event='NewBlock'" }
        };

        var json = JsonSerializer.Serialize(req, CometBftWebSocketJsonContext.Default.WsSubscribeRequest);
        Assert.Contains("subscribe", json);
        Assert.Contains("NewBlock", json);

        var roundtrip = JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsSubscribeRequest);
        Assert.NotNull(roundtrip);
        Assert.Equal(1, roundtrip!.Id);
    }

    [Fact]
    public void WsUnsubscribeAllRequest_RoundTrip()
    {
        var req = new WsUnsubscribeAllRequest { Id = 42 };

        var json = JsonSerializer.Serialize(req, CometBftWebSocketJsonContext.Default.WsUnsubscribeAllRequest);
        Assert.Contains("unsubscribe_all", json);
        Assert.Contains("42", json);

        var roundtrip = JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsUnsubscribeAllRequest);
        Assert.NotNull(roundtrip);
        Assert.Equal(42, roundtrip!.Id);
    }

    [Fact]
    public void WsEnvelope_SubscribeAck_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":5,"result":{}}""";
        var envelope = JsonSerializer.Deserialize(json, CometBftWebSocketJsonContext.Default.WsEnvelope);

        Assert.NotNull(envelope);
        Assert.Equal(5, envelope!.Id);
    }
}
