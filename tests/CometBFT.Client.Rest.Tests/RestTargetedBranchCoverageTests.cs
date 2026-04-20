using System.Net;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using CometBFT.Client.Rest;
using CometBFT.Client.Rest.Internal;

namespace CometBFT.Client.Rest.Tests;

public sealed class RestTargetedBranchCoverageTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly CometBftRestClient _client;

    public RestTargetedBranchCoverageTests()
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
    public async Task GetBlockchainAsync_WithOnlyMinHeight_UsesQueryString()
    {
        _server.Given(Request.Create().WithPath("/blockchain").WithParam("minHeight", "5").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {"jsonrpc":"2.0","id":1,"result":{"last_height":"5","block_metas":[]}}
            """));

        var result = await _client.GetBlockchainAsync(minHeight: 5);
        Assert.Empty(result.Headers);
    }

    [Fact]
    public async Task GetBlockchainAsync_WithOnlyMaxHeight_UsesQueryString()
    {
        _server.Given(Request.Create().WithPath("/blockchain").WithParam("maxHeight", "9").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {"jsonrpc":"2.0","id":1,"result":{"last_height":"9","block_metas":[]}}
            """));

        var result = await _client.GetBlockchainAsync(maxHeight: 9);
        Assert.Empty(result.Headers);
    }

    [Fact]
    public async Task GetCommitAsync_FullPayload_MapsAllFields()
    {
        _server.Given(Request.Create().WithPath("/commit").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{
                "signed_header":{"header":{"height":"7"},"commit":{"block_id":{"hash":"HASH7"}}},
                "canonical":true
              }
            }
            """));

        var result = await _client.GetCommitAsync();
        Assert.Equal("7", result["height"]);
        Assert.Equal("HASH7", result["hash"]);
        Assert.Equal(bool.TrueString, result["canonical"]);
    }

    [Fact]
    public async Task GetCommitAsync_PartialSignedHeader_DefaultsMissingHash()
    {
        _server.Given(Request.Create().WithPath("/commit").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"signed_header":{"header":{"height":"8"}},"canonical":false}
            }
            """));

        var result = await _client.GetCommitAsync();
        Assert.Equal("8", result["height"]);
        Assert.Equal(string.Empty, result["hash"]);
        Assert.Equal(bool.FalseString, result["canonical"]);
    }

    [Fact]
    public async Task GetConsensusParamsAsync_WithConcreteValues_MapsAllFields()
    {
        _server.Given(Request.Create().WithPath("/consensus_params").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{
                "consensus_params":{
                  "block":{"max_bytes":"10","max_gas":"20"},
                  "evidence":{"max_age_num_blocks":"30","max_age_duration":"40s"},
                  "validator":{"pub_key_types":["ed25519"]},
                  "version":{"app":"50"}
                }
              }
            }
            """));

        var result = await _client.GetConsensusParamsAsync();
        Assert.Equal(10L, result.BlockMaxBytes);
        Assert.Equal(20L, result.BlockMaxGas);
        Assert.Equal(30L, result.EvidenceMaxAgeNumBlocks);
        Assert.Equal("40s", result.EvidenceMaxAgeDuration);
        Assert.Equal("ed25519", result.ValidatorPubKeyTypes[0]);
        Assert.Equal(50L, result.VersionApp);
    }

    [Fact]
    public async Task GetHeaderAsync_WithNullHeaderNode_FallsBackToResultNode()
    {
        _server.Given(Request.Create().WithPath("/header").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"header":null,"height":"11","time":"2024-01-01T00:00:00Z"}
            }
            """));

        var header = await _client.GetHeaderAsync();
        Assert.Equal(11L, header.Height);
    }

    [Fact]
    public async Task GetNetInfoAsync_WithPeersNull_DefaultsToEmptyPeers()
    {
        _server.Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"listening":true,"listeners":[],"peers":null}
            }
            """));

        var result = await _client.GetNetInfoAsync();
        Assert.Empty(result.Peers);
    }

    [Fact]
    public void MapBlockNode_WithExplicitNullTxArray_DefaultsToEmptyList()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse("""
        {
          "block": {
            "header": { "height": "12", "time": "2024-01-01T00:00:00Z" },
            "data": { "txs": null }
          }
        }
        """);

        var block = RestResponseMapper.MapBlockNode(node);
        Assert.Empty(block.Txs);
    }
}
