using System.Text.Json.Serialization;
using System.Text.Json;

namespace CometBFT.Client.Rest.Json;

/// <summary>
/// Represents a JSON-RPC 2.0 response envelope returned by CometBFT RPC endpoints.
/// </summary>
/// <typeparam name="T">The type of the result payload.</typeparam>
internal sealed class JsonRpcResponse<T>
{
    /// <summary>Gets the JSON-RPC protocol version string.</summary>
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }

    /// <summary>
    /// Gets the request correlation ID.
    /// Public providers are inconsistent here and may return a number or a string,
    /// so the wire model keeps the raw JSON value instead of enforcing one shape.
    /// </summary>
    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    /// <summary>Gets the result payload, or <c>null</c> if there was an error.</summary>
    [JsonPropertyName("result")]
    public T? Result { get; init; }

    /// <summary>Gets the error object if the request failed.</summary>
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}

/// <summary>
/// Represents a JSON-RPC 2.0 error object.
/// </summary>
internal sealed class JsonRpcError
{
    /// <summary>Gets the numeric error code.</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>Gets the human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Gets additional error data.</summary>
    [JsonPropertyName("data")]
    public string? Data { get; init; }
}
