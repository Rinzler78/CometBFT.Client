namespace CometBFT.Client.Fixture.Tests;

internal sealed record WebSocketServerReply(string JsonRpc, int Id, object? Result = null, object? Error = null)
{
    public static WebSocketServerReply Ok(int id) => new("2.0", id, Result: new { });
}
