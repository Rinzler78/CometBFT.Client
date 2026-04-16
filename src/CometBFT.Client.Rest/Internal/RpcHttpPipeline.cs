using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Rest.Json;

namespace CometBFT.Client.Rest.Internal;

internal sealed class RpcHttpPipeline
{
    private readonly HttpClient _http;

    internal RpcHttpPipeline(HttpClient http)
    {
        _http = http;
    }

    internal HttpClient GetHttpClient() => _http;

    internal async Task<T> GetRpcResultAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        JsonRpcResponse<T>? envelope;
        try
        {
            using var response = await _http.GetAsync(
                relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            envelope = await JsonSerializer.DeserializeAsync<JsonRpcResponse<T>>(
                stream, CometBftJsonContext.Default.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new CometBftRestException($"HTTP request to '{relativeUrl}' failed.", ex.StatusCode.Value, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CometBftRestException($"HTTP request to '{relativeUrl}' failed.", ex);
        }
        catch (JsonException ex)
        {
            throw new CometBftRestException($"Failed to deserialize response from '{relativeUrl}'.", ex);
        }

        if (envelope is null)
        {
            throw new CometBftRestException($"Received null response from '{relativeUrl}'.");
        }

        if (envelope.Error is not null)
        {
            throw new CometBftRestException(
                envelope.Error.Message ?? "Unknown RPC error.",
                envelope.Error.Code);
        }

        if (envelope.Result is null)
        {
            throw new CometBftRestException($"RPC result was null for '{relativeUrl}'.");
        }

        return envelope.Result;
    }

    internal async Task<JsonNode> GetRpcResultNodeAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        JsonNode? root;
        try
        {
            using var response = await _http.GetAsync(
                relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new CometBftRestException($"HTTP request to '{relativeUrl}' failed.", ex.StatusCode.Value, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CometBftRestException($"HTTP request to '{relativeUrl}' failed.", ex);
        }
        catch (JsonException ex)
        {
            throw new CometBftRestException($"Failed to deserialize response from '{relativeUrl}'.", ex);
        }

        if (root is null)
        {
            throw new CometBftRestException($"Received null response from '{relativeUrl}'.");
        }

        var error = root["error"];
        if (error is not null)
        {
            throw new CometBftRestException(
                error["message"]?.GetValue<string>() ?? "Unknown RPC error.",
                (int)RestClientHelpers.ParseLongNode(error["code"]));
        }

        return root["result"] ?? throw new CometBftRestException($"RPC result was null for '{relativeUrl}'.");
    }

    internal async Task<T> PostRpcResultAsync<T>(string method, string txBytes, CancellationToken cancellationToken)
    {
        var request = new JsonRpcBroadcastRequest
        {
            Method = method,
            Params = new JsonRpcBroadcastParams { Tx = txBytes },
        };

        HttpResponseMessage response;
        try
        {
            var json = JsonSerializer.Serialize(request, CometBftJsonContext.Default.JsonRpcBroadcastRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            response = await _http.PostAsync("/", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            throw new CometBftRestException($"JSON-RPC call '{method}' failed.", ex.StatusCode.Value, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CometBftRestException($"JSON-RPC call '{method}' failed.", ex);
        }

        return await ReadRpcResponseAsync<T>(response, method, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadRpcResponseAsync<T>(
        HttpResponseMessage response,
        string context,
        CancellationToken cancellationToken)
    {
        JsonRpcResponse<T>? envelope;
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            envelope = await JsonSerializer.DeserializeAsync<JsonRpcResponse<T>>(
                stream, CometBftJsonContext.Default.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new CometBftRestException($"Failed to deserialize response for '{context}'.", ex);
        }

        if (envelope is null)
        {
            throw new CometBftRestException($"Received null response for '{context}'.");
        }

        if (envelope.Error is not null)
        {
            throw new CometBftRestException(
                envelope.Error.Message ?? "Unknown RPC error.",
                envelope.Error.Code);
        }

        if (envelope.Result is null)
        {
            throw new CometBftRestException($"RPC result was null for '{context}'.");
        }

        return envelope.Result;
    }
}
