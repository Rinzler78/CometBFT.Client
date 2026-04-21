using System.Net;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using CometBFT.Client.Rest;

namespace CometBFT.Client.Rest.Tests;

public sealed class RestLastBranchCoverageTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly CometBftRestClient _client;

    public RestLastBranchCoverageTests()
    {
        _server = WireMockServer.Start();
        _http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _client = new CometBftRestClient(_http, Options.Create(new Core.Options.CometBftRestOptions { BaseUrl = _server.Url! }));
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task GetCommitAsync_WithEmptySignedHeader_DefaultsEverything()
    {
        _server.Given(Request.Create().WithPath("/commit").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"signed_header":{},"canonical":true}
            }
            """));

        var result = await _client.GetCommitAsync();
        Assert.Equal(string.Empty, result["height"]);
        Assert.Equal(string.Empty, result["hash"]);
        Assert.Equal(bool.TrueString, result["canonical"]);
    }

    [Fact]
    public async Task GetCommitAsync_WithHeaderAndCommitObjectsButMissingInnerValues_DefaultsToEmptyStrings()
    {
        _server.Given(Request.Create().WithPath("/commit").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"signed_header":{"header":{},"commit":{"block_id":{}}},"canonical":false}
            }
            """));

        var result = await _client.GetCommitAsync();
        Assert.Equal(string.Empty, result["height"]);
        Assert.Equal(string.Empty, result["hash"]);
        Assert.Equal(bool.FalseString, result["canonical"]);
    }

    [Fact]
    public async Task GetHeaderAsync_WithHeight_UsesHeightQuery()
    {
        _server.Given(Request.Create().WithPath("/header").WithParam("height", "15").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"height":"15","time":"2024-01-01T00:00:00Z"}
            }
            """));

        var header = await _client.GetHeaderAsync(15);
        Assert.Equal(15L, header.Height);
    }

    [Fact]
    public async Task GetNetInfoAsync_WithPeersArrayContainingRealPeer_UsesPeerMapper()
    {
        _server.Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{
                "listening":true,
                "listeners":[],
                "peers":[{"node_info":{"id":"peer1"}}]
              }
            }
            """));

        var result = await _client.GetNetInfoAsync();
        Assert.Equal("peer1", result.Peers[0].NodeId);
    }

    [Fact]
    public async Task GetNetInfoAsync_WithPeersArrayContainingNullEntry_DefaultsPeer()
    {
        _server.Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{
                "listening":true,
                "listeners":[],
                "peers":[null]
              }
            }
            """));

        var result = await _client.GetNetInfoAsync();
        Assert.Equal(string.Empty, result.Peers[0].NodeId);
    }

    [Fact]
    public async Task GetNetInfoAsync_WhenListeningFieldMissing_DefaultsToFalse()
    {
        _server.Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"listeners":[],"peers":[]}
            }
            """));

        var result = await _client.GetNetInfoAsync();
        Assert.False(result.Listening);
    }
}
