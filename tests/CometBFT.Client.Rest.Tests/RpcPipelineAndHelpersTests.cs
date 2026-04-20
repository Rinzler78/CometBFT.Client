using System.Net;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Rest;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

/// <summary>
/// Tests targeting previously uncovered branches in
/// <c>RpcHttpPipeline</c> and <c>RestClientHelpers</c>.
/// All scenarios are exercised through the public <see cref="CometBftRestClient"/> surface.
/// </summary>
public sealed class RpcPipelineAndHelpersTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly CometBftRestClient _client;
    private readonly HttpClient _http;

    public RpcPipelineAndHelpersTests()
    {
        _server = WireMockServer.Start();
        var options = new CometBftRestOptions { BaseUrl = _server.Url! };
        _http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _client = new CometBftRestClient(_http, Options.Create(options));
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetRpcResultAsync<T> — error branches (lines 32/34, 36/38, 43, 55)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRpcResultAsync_ThrowsCometBftRestException_OnHttpRequestException()
    {
        // Lines 32 + 34 in RpcHttpPipeline: catch (HttpRequestException) in GetRpcResultAsync.
        // Stop the server so the HTTP call fails with HttpRequestException.
        _server.Stop();

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetStatusAsync());
    }

    [Fact]
    public async Task GetRpcResultAsync_ThrowsCometBftRestException_OnInvalidJson()
    {
        // Lines 36 + 38: catch (JsonException) in GetRpcResultAsync.
        _server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "text/plain")
                .WithBody("not-valid-json"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetStatusAsync());
    }

    [Fact]
    public async Task GetRpcResultAsync_ThrowsCometBftRestException_OnNullEnvelope()
    {
        // Line 43: envelope is null (body is literal JSON "null").
        _server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("null"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetStatusAsync());
    }

    [Fact]
    public async Task GetRpcResultAsync_ThrowsCometBftRestException_OnNullResult()
    {
        // Line 55: envelope.Result is null (no "result" key in the response).
        _server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":1}"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetStatusAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetRpcResultNodeAsync — error branches (lines 72/74, 76/78, 83, 113/115)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRpcResultNodeAsync_ThrowsCometBftRestException_OnHttpRequestException()
    {
        // Lines 72 + 74: catch (HttpRequestException) in GetRpcResultNodeAsync.
        // Use GetNetInfoAsync which calls GetRpcResultNodeAsync("/net_info", …).
        _server.Stop();

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetNetInfoAsync());
    }

    [Fact]
    public async Task GetRpcResultNodeAsync_ThrowsCometBftRestException_OnInvalidJson()
    {
        // Lines 76 + 78: catch (JsonException) in GetRpcResultNodeAsync.
        _server
            .Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "text/plain")
                .WithBody("{bad json{{"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetNetInfoAsync());
    }

    [Fact]
    public async Task GetRpcResultNodeAsync_ThrowsCometBftRestException_OnNullRoot()
    {
        // Line 83: root is null (body is literal JSON "null").
        _server
            .Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("null"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetNetInfoAsync());
    }

    [Fact]
    public async Task GetRpcResultNodeAsync_ErrorWithoutMessage_UsesFallbackMessage()
    {
        _server
            .Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "error":{"code":5}
                }
                """));

        var ex = await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetNetInfoAsync());
        Assert.Equal("Unknown RPC error.", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PostRpcResultAsync<T> — error branches (lines 113/115)
    // ReadRpcResponseAsync — error branches (lines 133/135, 140, 145-147, 152)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostRpcResultAsync_ThrowsCometBftRestException_OnHttpRequestException()
    {
        // Lines 113 + 115: catch (HttpRequestException) in PostRpcResultAsync.
        // Stop the server so the POST call fails.
        _server.Stop();

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.BroadcastTxAsync("dHgx"));
    }

    [Fact]
    public async Task ReadRpcResponseAsync_ThrowsCometBftRestException_OnInvalidJson()
    {
        // Lines 133 + 135: catch (JsonException) in ReadRpcResponseAsync (called from PostRpcResultAsync).
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "text/plain")
                .WithBody("{bad json{{"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.BroadcastTxAsync("dHgx"));
    }

    [Fact]
    public async Task ReadRpcResponseAsync_ThrowsCometBftRestException_OnNullEnvelope()
    {
        // Line 140: envelope is null in ReadRpcResponseAsync.
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("null"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.BroadcastTxAsync("dHgx"));
    }

    [Fact]
    public async Task ReadRpcResponseAsync_ThrowsCometBftRestException_OnRpcError()
    {
        // Lines 145-147: envelope.Error is not null in ReadRpcResponseAsync.
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":-1,\"error\":{\"code\":-32600,\"message\":\"Invalid request\"}}"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.BroadcastTxAsync("dHgx"));
    }

    [Fact]
    public async Task ReadRpcResponseAsync_ThrowsCometBftRestException_OnNullResult()
    {
        // Line 152: envelope.Result is null in ReadRpcResponseAsync (no "result" key).
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":-1}"));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.BroadcastTxAsync("dHgx"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RestClientHelpers.ParseLongNode — lines 27 (null), 39 (int), 48 (string)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseLongNode_ReturnsZero_WhenNodeIsNull()
    {
        // Line 27: node is null → return 0.
        // GetRpcResultNodeAsync("/consensus_params") calls ParseLongNode on parameters[…]
        // which will be null when the nested key is absent.
        // We use GetConsensusParamsAsync with a minimal response that has no "max_bytes".
        _server
            .Given(Request.Create().WithPath("/consensus_params").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0","id":1,
                  "result":{
                    "block_height":"1",
                    "consensus_params":{
                      "block":{},
                      "evidence":{},
                      "validator":{"pub_key_types":[]},
                      "version":{}
                    }
                  }
                }
                """));

        var result = await _client.GetConsensusParamsAsync();

        // BlockMaxBytes should be 0 because the "max_bytes" node is absent (null).
        Assert.Equal(0L, result.BlockMaxBytes);
    }

    [Fact]
    public async Task ParseLongNode_ParsesNumericStringValue()
    {
        // Line 48: TryGetValue<string> branch — JSON sends a quoted number string.
        // GetGenesisChunkAsync calls ParseLongNode on result["chunk"] and result["total"].
        // If the JSON encoder produces them as strings, the string branch is taken.
        _server
            .Given(Request.Create().WithPath("/genesis_chunked").WithParam("chunk", "0").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0","id":1,
                  "result":{"chunk":"0","total":"5","data":"YWJj"}
                }
                """));

        var result = await _client.GetGenesisChunkAsync(0);

        Assert.Equal(0, result.Chunk);
        Assert.Equal(5, result.Total);
    }

    [Fact]
    public async Task GetRpcResultNodeAsync_ErrorWithStringCode_ThrowsCometBftRestException()
    {
        // Exercises ParseLongNode on error["code"] when code arrives as a string.
        // Also covers line 27 (null code) — only the message matters for the exception.
        _server
            .Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0","id":1,
                  "error":{"code":"-32601","message":"Method not found"}
                }
                """));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetNetInfoAsync());
    }

    [Fact]
    public async Task ParseLongNode_ReturnsZero_WhenNodeIsNonNumericNonString()
    {
        // Line 48: return 0 when none of TryGetValue<long/int/string> succeed.
        // A JSON boolean value causes all three TryGetValue to return false.
        // GetRpcResultNodeAsync error path: ParseLongNode(error["code"]) where code=true.
        _server
            .Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0","id":1,
                  "error":{"code":true,"message":"Boolean code error"}
                }
                """));

        // The cast to (int) of the ParseLongNode result (0) produces RpcErrorCode 0.
        var ex = await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetNetInfoAsync());
        // Verify we got the exception and not a parse crash — rpc code defaults to 0.
        Assert.Equal(0, ex.RpcErrorCode);
    }
}
