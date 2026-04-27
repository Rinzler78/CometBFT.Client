using System.Text.Json;
using System.Text.Json.Serialization;
using CometBFT.Client.Core.Events;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Typed JSON-RPC reply envelope emitted by the passive test server.
/// Every server frame goes through <see cref="JsonSerializer"/> on this record —
/// no hardcoded JSON lives in the fixture.
/// </summary>
internal sealed record WebSocketServerReply(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("result"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WebSocketServerResult? Result = null,
    [property: JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WebSocketServerError? Error = null)
{
    /// <summary>Successful subscribe ACK: <c>{"jsonrpc":"2.0","id":N,"result":{}}</c>.</summary>
    public static WebSocketServerReply Ok(int id) =>
        new(WebSocketRpcMethods.JsonRpcVersion, id, Result: new WebSocketServerResult());

    /// <summary>JSON-RPC error reply: <c>{"jsonrpc":"2.0","id":N,"error":{code,message}}</c>.</summary>
    public static WebSocketServerReply WithError(int id, int code, string message, string? data = null) =>
        new(WebSocketRpcMethods.JsonRpcVersion, id, Error: new WebSocketServerError(code, message, data));
}

/// <summary>Empty JSON object emitted as <c>result</c> on a successful ACK.</summary>
internal sealed record WebSocketServerResult();

/// <summary>JSON-RPC error payload returned by the server (HTTP-like: code + message).</summary>
internal sealed record WebSocketServerError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Data = null);
