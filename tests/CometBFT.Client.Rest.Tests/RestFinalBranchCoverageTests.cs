using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using CometBFT.Client.Rest;
using CometBFT.Client.Rest.Internal;

namespace CometBFT.Client.Rest.Tests;

public sealed class RestFinalBranchCoverageTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly CometBftRestClient _client;

    public RestFinalBranchCoverageTests()
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
    public async Task GetBlockchainAsync_WhenBlockMetasMissing_ReturnsEmptyHeaders()
    {
        _server.Given(Request.Create().WithPath("/blockchain").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {"jsonrpc":"2.0","id":1,"result":{"last_height":"1"}}
            """));

        var result = await _client.GetBlockchainAsync();
        Assert.Empty(result.Headers);
    }

    [Fact]
    public async Task GetCommitAsync_WithHashButNoHeader_DefaultsHeightOnly()
    {
        _server.Given(Request.Create().WithPath("/commit").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"signed_header":{"commit":{"block_id":{"hash":"HASHONLY"}}}}
            }
            """));

        var result = await _client.GetCommitAsync();
        Assert.Equal(string.Empty, result["height"]);
        Assert.Equal("HASHONLY", result["hash"]);
    }

    [Fact]
    public async Task GetConsensusParamsAsync_WhenConsensusParamsMissing_DefaultsEverything()
    {
        _server.Given(Request.Create().WithPath("/consensus_params").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {"jsonrpc":"2.0","id":1,"result":{}}
            """));

        var result = await _client.GetConsensusParamsAsync();
        Assert.Equal(0L, result.BlockMaxBytes);
        Assert.Empty(result.ValidatorPubKeyTypes);
    }

    [Fact]
    public async Task GetHeaderAsync_WhenWrappedHeaderPresent_ReturnsWrappedHeader()
    {
        _server.Given(Request.Create().WithPath("/header").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"header":{"height":"12","time":"2024-01-01T00:00:00Z","proposer_address":"P"}}
            }
            """));

        var header = await _client.GetHeaderAsync();
        Assert.Equal(12L, header.Height);
    }

    [Fact]
    public async Task GetNetInfoAsync_WithPeersArrayContainingNullEntry_DefaultsPeer()
    {
        _server.Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"listening":true,"peers":[null]}
            }
            """));

        var result = await _client.GetNetInfoAsync();
        Assert.Single(result.Peers);
        Assert.Equal(string.Empty, result.Peers[0].NodeId);
    }

    [Fact]
    public void MapBlockNode_MissingTimeField_DefaultsTimeToMinValue()
    {
        var node = JsonNode.Parse("""
        {
          "block": {
            "header": { "height": "13", "proposer_address": "P" },
            "data": { "txs": [] }
          }
        }
        """);

        var block = RestResponseMapper.MapBlockNode(node);
        Assert.Equal(DateTimeOffset.MinValue, block.Time);
    }
}
