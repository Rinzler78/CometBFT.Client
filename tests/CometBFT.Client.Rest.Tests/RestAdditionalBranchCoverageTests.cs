using System.Net;
using CometBFT.Client.Rest;
using CometBFT.Client.Rest.Internal;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

public sealed class RestAdditionalBranchCoverageTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly CometBftRestClient _client;

    public RestAdditionalBranchCoverageTests()
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
    public async Task GetConsensusParamsAsync_WithHeight_UsesHeightQuery()
    {
        _server.Given(Request.Create().WithPath("/consensus_params").WithParam("height", "5").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"consensus_params":{}}
            }
            """));

        var result = await _client.GetConsensusParamsAsync(5);
        Assert.Empty(result.ValidatorPubKeyTypes);
    }

    [Fact]
    public async Task GetGenesisAsync_WithValidatorsArray_UsesCount()
    {
        _server.Given(Request.Create().WithPath("/genesis").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"genesis":{"validators":[{},{}]}}
            }
            """));

        var result = await _client.GetGenesisAsync();
        Assert.Equal("2", result["validators_count"]);
    }

    [Fact]
    public async Task GetHeaderAsync_WhenResultMissingHeader_DefaultsToEmptyHeader()
    {
        _server.Given(Request.Create().WithPath("/header").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {"jsonrpc":"2.0","id":1,"result":{}}
            """));

        var header = await _client.GetHeaderAsync();
        Assert.Equal(0L, header.Height);
        Assert.Equal(string.Empty, header.ProposerAddress);
    }

    [Fact]
    public async Task GetNetInfoAsync_ListenerArrayWithNullEntry_DefaultsEntryToEmptyString()
    {
        _server.Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{"listening":true,"listeners":[null,"tcp://0.0.0.0:26656"]}
            }
            """));

        var result = await _client.GetNetInfoAsync();
        Assert.Equal(string.Empty, result.Listeners[0]);
        Assert.Equal("tcp://0.0.0.0:26656", result.Listeners[1]);
    }

    [Fact]
    public async Task GetUnconfirmedTxsAsync_WithoutLimit_UsesDefaultRoute()
    {
        _server.Given(Request.Create().WithPath("/unconfirmed_txs").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":1,
              "result":{}
            }
            """));

        var result = await _client.GetUnconfirmedTxsAsync();
        Assert.Empty(result.Txs);
    }

    [Fact]
    public void MapBlockNode_WrapperWithoutBlockIdHash_DefaultsHashToEmptyString()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse("""
        {
          "block": {
            "header": { "height": "9", "time": "2024-01-01T00:00:00Z", "proposer_address": "PROP" },
            "data": { "txs": [] }
          },
          "block_id": {}
        }
        """);

        var block = RestResponseMapper.MapBlockNode(node);
        Assert.Equal(string.Empty, block.Hash);
    }

    [Fact]
    public async Task ReadRpcResponseAsync_ErrorWithoutMessage_UsesFallbackMessage()
    {
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody("""
            {
              "jsonrpc":"2.0",
              "id":-1,
              "error":{"code":12}
            }
            """));

        var ex = await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.BroadcastTxAsync("dHgx"));
        Assert.Equal("Unknown RPC error.", ex.Message);
    }
}
