using System.Text.Json.Serialization;

namespace CometBFT.Client.Rest.Json;

/// <summary>Typed JSON-RPC 2.0 request body used for broadcast_tx_* calls.</summary>
internal sealed class JsonRpcBroadcastRequest
{
    /// <summary>Gets the JSON-RPC version string.</summary>
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = "2.0";

    /// <summary>Gets the request correlation ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Gets the RPC method name.</summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    /// <summary>Gets the transaction parameters.</summary>
    [JsonPropertyName("params")]
    public JsonRpcBroadcastParams Params { get; init; } = new();
}

/// <summary>Parameters payload for broadcast_tx_* JSON-RPC calls.</summary>
internal sealed class JsonRpcBroadcastParams
{
    /// <summary>Gets the base64-encoded transaction bytes.</summary>
    [JsonPropertyName("tx")]
    public string Tx { get; init; } = string.Empty;
}
