using System.Net;
using System.Text.Json.Nodes;
using CometBFT.Client.Rest;
using CometBFT.Client.Rest.Internal;
using CometBFT.Client.Rest.Json;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

public sealed class RestBranchCoverageTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly CometBftRestClient _client;

    public RestBranchCoverageTests()
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
    public void MapNodeInfo_NullProtocolVersion_DefaultsToZeroes()
    {
        var raw = new RpcNodeInfo
        {
            Id = "node",
            ListenAddr = "tcp://0.0.0.0:26656",
            Network = "testnet",
            Version = "1",
            Channels = "40",
            Moniker = "node-a",
            ProtocolVersion = null,
        };

        var node = RestResponseMapper.MapNodeInfo(raw);

        Assert.Equal(0UL, node.ProtocolVersion.P2P);
        Assert.Equal(0UL, node.ProtocolVersion.Block);
        Assert.Equal(0UL, node.ProtocolVersion.App);
    }

    [Fact]
    public void MapNodeInfo_InvalidProtocolVersion_DefaultsToZeroes()
    {
        var raw = new RpcNodeInfo
        {
            Id = "node",
            ListenAddr = "tcp://0.0.0.0:26656",
            Network = "testnet",
            Version = "1",
            Channels = "40",
            Moniker = "node-a",
            ProtocolVersion = new RpcProtocolVersion { P2P = "x", Block = "y", App = "z" },
        };

        var node = RestResponseMapper.MapNodeInfo(raw);

        Assert.Equal(0UL, node.ProtocolVersion.P2P);
        Assert.Equal(0UL, node.ProtocolVersion.Block);
        Assert.Equal(0UL, node.ProtocolVersion.App);
    }

    [Fact]
    public void MapBlockNode_DirectNode_UsesDefaultsForMissingFields()
    {
        var node = JsonNode.Parse("""
        {
          "header": { "height": "not-a-number", "time": "not-a-date" },
          "data": {},
          "block_id": {}
        }
        """);

        var block = RestResponseMapper.MapBlockNode(node);

        Assert.Equal(0L, block.Height);
        Assert.Equal(string.Empty, block.Hash);
        Assert.Equal(string.Empty, block.Proposer);
        Assert.Equal(DateTimeOffset.MinValue, block.Time);
        Assert.Empty(block.Txs);
    }

    [Fact]
    public void MapHeader_InvalidValues_FallBackToDefaults()
    {
        var node = JsonNode.Parse("""
        {
          "height": "oops",
          "time": "still-not-a-date"
        }
        """);

        var header = RestResponseMapper.MapHeader(node);

        Assert.Equal(0L, header.Height);
        Assert.Equal(DateTimeOffset.MinValue, header.Time);
        Assert.Equal(string.Empty, header.Version);
        Assert.Equal(string.Empty, header.LastBlockId);
        Assert.Equal(string.Empty, header.ProposerAddress);
    }

    [Fact]
    public void MapBlock_DataNull_UsesEmptyTransactions()
    {
        var raw = new RpcBlock
        {
            Header = new RpcBlockHeader { Height = "oops", ProposerAddress = "P" },
            Data = null,
        };

        var block = RestResponseMapper.MapBlock(raw, "HASH");

        Assert.Equal(0L, block.Height);
        Assert.Equal("HASH", block.Hash);
        Assert.Empty(block.Txs);
    }

    [Fact]
    public void MapBlockNode_WrappedNode_WithNullEntries_UsesSafeDefaults()
    {
        var node = JsonNode.Parse("""
        {
          "block_id": { "hash": "HASH2" },
          "block": {
            "header": { "height": "5", "time": "2024-01-01T00:00:00Z" },
            "data": { "txs": [null, "AQ=="] }
          }
        }
        """);

        var block = RestResponseMapper.MapBlockNode(node);

        Assert.Equal(5L, block.Height);
        Assert.Equal("HASH2", block.Hash);
        Assert.Equal(string.Empty, block.Txs[0]);
        Assert.Equal("AQ==", block.Txs[1]);
    }

    [Fact]
    public void MapBlockNode_MissingHeader_ThrowsCometBftRestException()
    {
        var node = JsonNode.Parse("""
        {
          "block": { "data": { "txs": [] } }
        }
        """);

        Assert.Throws<Core.Exceptions.CometBftRestException>(() => RestResponseMapper.MapBlockNode(node));
    }

    [Fact]
    public void MapBlockNode_NullNode_ThrowsCometBftRestException()
    {
        Assert.Throws<Core.Exceptions.CometBftRestException>(() => RestResponseMapper.MapBlockNode(null));
    }

    [Fact]
    public void MapBlockNode_DataObjectWithoutTxs_DefaultsToEmptyList()
    {
        var node = JsonNode.Parse("""
        {
          "block_id": { "hash": "HASH3" },
          "block": {
            "header": { "height": "6", "time": "2024-01-01T00:00:00Z", "proposer_address": "PROP" },
            "data": {}
          }
        }
        """);

        var block = RestResponseMapper.MapBlockNode(node);

        Assert.Equal("PROP", block.Proposer);
        Assert.Empty(block.Txs);
    }

    [Fact]
    public void MapBlockNode_DirectNodeWithValidValues_ParsesWithoutWrapper()
    {
        var node = JsonNode.Parse("""
        {
          "header": { "height": "8", "time": "2024-01-01T00:00:00Z", "proposer_address": "PROP2" },
          "data": { "txs": ["AQ=="] }
        }
        """);

        var block = RestResponseMapper.MapBlockNode(node);

        Assert.Equal(8L, block.Height);
        Assert.Equal("PROP2", block.Proposer);
        Assert.Single(block.Txs);
    }

    [Fact]
    public void MapTxResult_NullEventsAndInvalidGas_DefaultToSafeValues()
    {
        var raw = new RpcTxResult
        {
            Code = 7,
            Data = null,
            Log = null,
            Info = null,
            GasWanted = "NaN",
            GasUsed = "NaN",
            Events = null,
            Codespace = null,
        };

        var tx = RestResponseMapper.MapTxResult(raw, "HASH", 12L, 2);

        Assert.Equal(0L, tx.GasWanted);
        Assert.Equal(0L, tx.GasUsed);
        Assert.Empty(tx.Events);
        Assert.Equal("HASH", tx.Hash);
    }

    [Fact]
    public void MapEvent_NullAttributes_ReturnsEmptyCollection()
    {
        var raw = new RpcEvent { Type = "transfer", Attributes = null };

        var evt = RestResponseMapper.MapEvent(raw);

        Assert.Empty(evt.Attributes);
    }

    [Fact]
    public void MapValidator_MissingPubKeyAndInvalidNumbers_DefaultsToSafeValues()
    {
        var raw = new RpcValidator
        {
            Address = "VAL1",
            PubKey = null,
            VotingPower = "invalid",
            ProposerPriority = "invalid",
        };

        var validator = RestResponseMapper.MapValidator(raw);

        Assert.Equal(string.Empty, validator.PubKey);
        Assert.Equal(0L, validator.VotingPower);
        Assert.Equal(0L, validator.ProposerPriority);
    }

    [Fact]
    public void MapNetworkPeer_NullNode_DefaultsToEmptyStrings()
    {
        var peer = RestResponseMapper.MapNetworkPeer(null);

        Assert.Equal(string.Empty, peer.NodeId);
        Assert.Equal(string.Empty, peer.Moniker);
        Assert.Equal(string.Empty, peer.Network);
        Assert.Equal(string.Empty, peer.RemoteIp);
        Assert.Equal(string.Empty, peer.ConnectionStatus);
    }

    [Fact]
    public void MapNetworkPeer_WithConnectionStatus_SerializesStatusJson()
    {
        var peer = RestResponseMapper.MapNetworkPeer(JsonNode.Parse("""
        {
          "remote_ip":"10.0.0.1",
          "connection_status":{"duration":"1s"},
          "node_info":{"id":"node1","moniker":"m1","network":"n1"}
        }
        """));

        Assert.Contains("duration", peer.ConnectionStatus, StringComparison.Ordinal);
        Assert.Equal("node1", peer.NodeId);
    }

    [Fact]
    public void MapNetworkPeer_WithMissingNodeInfo_DefaultsNodeFieldsOnly()
    {
        var peer = RestResponseMapper.MapNetworkPeer(JsonNode.Parse("""
        {
          "remote_ip":"10.0.0.2"
        }
        """));

        Assert.Equal(string.Empty, peer.NodeId);
        Assert.Equal(string.Empty, peer.Moniker);
        Assert.Equal(string.Empty, peer.Network);
        Assert.Equal("10.0.0.2", peer.RemoteIp);
        Assert.Equal(string.Empty, peer.ConnectionStatus);
    }

    [Fact]
    public void MapNetworkPeer_WithEmptyNodeInfoObject_DefaultsNestedFields()
    {
        var peer = RestResponseMapper.MapNetworkPeer(JsonNode.Parse("""
        {
          "node_info":{},
          "connection_status":null
        }
        """));

        Assert.Equal(string.Empty, peer.NodeId);
        Assert.Equal(string.Empty, peer.Moniker);
        Assert.Equal(string.Empty, peer.Network);
        Assert.Equal(string.Empty, peer.ConnectionStatus);
    }

    [Fact]
    public void MapUnconfirmedTxs_NullNode_DefaultsToZeroAndEmptyList()
    {
        var info = RestResponseMapper.MapUnconfirmedTxs(null);

        Assert.Equal(0, info.Count);
        Assert.Equal(0, info.Total);
        Assert.Equal(0, info.TotalBytes);
        Assert.Empty(info.Txs);
    }

    [Fact]
    public async Task GetNetInfoAsync_MinimalPayload_UsesDefaultBranches()
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
                  "result":{
                    "listening":false
                  }
                }
                """));

        var info = await _client.GetNetInfoAsync();

        Assert.False(info.Listening);
        Assert.Empty(info.Listeners);
        Assert.Empty(info.Peers);
        Assert.Equal(0, info.PeerCount);
    }

    [Fact]
    public async Task GetConsensusStateAsync_MissingRoundState_DefaultsToEmptyString()
    {
        _server
            .Given(Request.Create().WithPath("/consensus_state").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.GetConsensusStateAsync();
        Assert.Equal(string.Empty, result["round_state"]);
    }

    [Fact]
    public async Task DumpConsensusStateAsync_MissingPeers_DefaultsToEmptyArrayJson()
    {
        _server
            .Given(Request.Create().WithPath("/dump_consensus_state").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.DumpConsensusStateAsync();
        Assert.Equal(string.Empty, result["round_state"]);
        Assert.Equal("[]", result["peers"]);
    }

    [Fact]
    public async Task GetConsensusParamsAsync_MinimalPayload_DefaultsMissingFields()
    {
        _server
            .Given(Request.Create().WithPath("/consensus_params").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "consensus_params":{
                      "validator":{}
                    }
                  }
                }
                """));

        var result = await _client.GetConsensusParamsAsync();

        Assert.Equal(0L, result.BlockMaxBytes);
        Assert.Equal(0L, result.BlockMaxGas);
        Assert.Equal(0L, result.EvidenceMaxAgeNumBlocks);
        Assert.Equal(string.Empty, result.EvidenceMaxAgeDuration);
        Assert.Empty(result.ValidatorPubKeyTypes);
        Assert.Equal(0L, result.VersionApp);
    }

    [Fact]
    public async Task GetConsensusParamsAsync_PubKeyTypesWithNullEntry_DefaultsNullElementToEmptyString()
    {
        _server
            .Given(Request.Create().WithPath("/consensus_params").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "consensus_params":{
                      "validator":{"pub_key_types":[null,"ed25519"]}
                    }
                  }
                }
                """));

        var result = await _client.GetConsensusParamsAsync();

        Assert.Equal(2, result.ValidatorPubKeyTypes.Count);
        Assert.Equal(string.Empty, result.ValidatorPubKeyTypes[0]);
        Assert.Equal("ed25519", result.ValidatorPubKeyTypes[1]);
    }

    [Fact]
    public async Task GetBlockchainAsync_EmptyMetas_ReturnsEmptyHeaders()
    {
        _server
            .Given(Request.Create().WithPath("/blockchain").WithParam("minHeight", "1").WithParam("maxHeight", "2").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "last_height":"22",
                    "block_metas":[]
                  }
                }
                """));

        var result = await _client.GetBlockchainAsync(1, 2);

        Assert.Equal(22L, result.LastHeight);
        Assert.Empty(result.Headers);
    }

    [Fact]
    public async Task GetBlockchainAsync_WithSparseHeader_UsesMapperFallbacks()
    {
        _server
            .Given(Request.Create().WithPath("/blockchain").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "last_height":"3",
                    "block_metas":[
                      { "header": { "height": "oops", "time": "bad-time" } }
                    ]
                  }
                }
                """));

        var result = await _client.GetBlockchainAsync();

        Assert.Single(result.Headers);
        Assert.Equal(0L, result.Headers[0].Height);
        Assert.Equal(DateTimeOffset.MinValue, result.Headers[0].Time);
    }

    [Fact]
    public async Task GetBlockchainAsync_NullMeta_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/blockchain").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "last_height":"3",
                    "block_metas":[null]
                  }
                }
                """));

        await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetBlockchainAsync());
    }

    [Fact]
    public async Task SearchBlocksAsync_NullBlocks_ReturnsEmptyList()
    {
        _server
            .Given(Request.Create().WithPath("/block_search").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "blocks":null
                  }
                }
                """));

        var result = await _client.SearchBlocksAsync("tm.event='NewBlock'");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetValidatorsAsync_NullValidators_ReturnsEmptyList()
    {
        _server
            .Given(Request.Create().WithPath("/validators").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"validators":null}
                }
                """));

        var validators = await _client.GetValidatorsAsync();

        Assert.Empty(validators);
    }

    [Fact]
    public async Task GetBlockResultsAsync_NullTxsResults_ReturnsEmptyList()
    {
        _server
            .Given(Request.Create().WithPath("/block_results").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"height":"1","txs_results":null}
                }
                """));

        var results = await _client.GetBlockResultsAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchTxAsync_NullTxs_ReturnsEmptyList()
    {
        _server
            .Given(Request.Create().WithPath("/tx_search").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"txs":null}
                }
                """));

        var txs = await _client.SearchTxAsync("tx.height=1");

        Assert.Empty(txs);
    }

    [Fact]
    public async Task SearchTxAsync_WithOptionalParameters_UsesQueryString()
    {
        _server
            .Given(Request.Create().WithPath("/tx_search").WithParam("prove", "true").WithParam("page", "2").WithParam("per_page", "10").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"txs":[]}
                }
                """));

        var txs = await _client.SearchTxAsync("tx.height=1", prove: true, page: 2, perPage: 10);
        Assert.Empty(txs);
    }

    [Fact]
    public async Task GetValidatorsAsync_WithOptionalParameters_UsesQueryString()
    {
        _server
            .Given(Request.Create().WithPath("/validators").WithParam("height", "7").WithParam("page", "2").WithParam("per_page", "5").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"validators":[]}
                }
                """));

        var validators = await _client.GetValidatorsAsync(height: 7, page: 2, perPage: 5);
        Assert.Empty(validators);
    }

    [Fact]
    public async Task SearchBlocksAsync_WithOptionalParameters_UsesQueryString()
    {
        _server
            .Given(Request.Create().WithPath("/block_search").WithParam("page", "2").WithParam("per_page", "10").WithParam("order_by", "desc").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"blocks":[]}
                }
                """));

        var blocks = await _client.SearchBlocksAsync("tm.event='NewBlock'", page: 2, perPage: 10, orderBy: "desc");
        Assert.Empty(blocks);
    }

    [Fact]
    public async Task GetGenesisAsync_MissingGenesisNode_DefaultsAllFields()
    {
        _server
            .Given(Request.Create().WithPath("/genesis").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.GetGenesisAsync();

        Assert.Equal(string.Empty, result["genesis_time"]);
        Assert.Equal("0", result["validators_count"]);
    }

    [Fact]
    public async Task GetCommitAsync_MissingFields_DefaultsToEmptyStrings()
    {
        _server
            .Given(Request.Create().WithPath("/commit").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.GetCommitAsync();

        Assert.Equal(string.Empty, result["height"]);
        Assert.Equal(string.Empty, result["hash"]);
        Assert.Equal(bool.FalseString, result["canonical"]);
    }

    [Fact]
    public async Task CheckTxAsync_MissingOptionalFields_DefaultsToNullsAndZeroes()
    {
        _server
            .Given(Request.Create().WithPath("/check_tx").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.CheckTxAsync("dHgx");

        Assert.Equal(0u, result.Code);
        Assert.Null(result.Data);
        Assert.Null(result.Log);
        Assert.Null(result.Codespace);
    }

    [Fact]
    public async Task BroadcastEvidenceAsync_MissingHash_DefaultsToEmptyString()
    {
        _server
            .Given(Request.Create().WithPath("/broadcast_evidence").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.BroadcastEvidenceAsync("abc");

        Assert.Equal(string.Empty, result["hash"]);
    }

    [Fact]
    public async Task GetAbciInfoAsync_MissingResponse_DefaultsAllFields()
    {
        _server
            .Given(Request.Create().WithPath("/abci_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.GetAbciInfoAsync();

        Assert.Equal(string.Empty, result["data"]);
        Assert.Equal(string.Empty, result["version"]);
    }

    [Fact]
    public async Task AbciQueryAsync_MissingResponse_DefaultsAllFields()
    {
        _server
            .Given(Request.Create().WithPath("/abci_query").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.AbciQueryAsync("/path", "data");

        Assert.Equal("0", result["code"]);
        Assert.Equal(string.Empty, result["value"]);
        Assert.Equal(string.Empty, result["codespace"]);
    }

    [Fact]
    public async Task AbciQueryAsync_WithHeightAndProve_UsesQueryString()
    {
        _server
            .Given(Request.Create().WithPath("/abci_query").WithParam("height", "5").WithParam("prove", "true").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.AbciQueryAsync("/path", "data", height: 5, prove: true);
        Assert.Equal("0", result["code"]);
    }

    [Fact]
    public async Task GuardClauses_NullArguments_ThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.GetBlockByHashAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.GetHeaderByHashAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.GetTxAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.SearchTxAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.BroadcastTxAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.BroadcastTxSyncAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.BroadcastTxCommitAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.CheckTxAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.BroadcastEvidenceAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.DialSeedsAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.DialPeersAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.AbciQueryAsync(null!, "data"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _client.AbciQueryAsync("/path", null!));
    }

    [Fact]
    public async Task GetStatusAsync_MissingRequiredFields_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetStatusAsync());
    }

    [Fact]
    public async Task GetBlockAsync_MissingBlock_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/block").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"block_id":{"hash":"H"}}
                }
                """));

        await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetBlockAsync());
    }

    [Fact]
    public async Task GetTxAsync_MissingTxResult_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/tx").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"hash":"0xAA","height":"1","index":0}
                }
                """));

        await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetTxAsync("AA"));
    }

    [Fact]
    public async Task GetHealthAsync_WithCanceledToken_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _client.GetHealthAsync(cts.Token));
    }

    [Fact]
    public async Task GetHeaderAsync_WhenResultIsDirectHeader_ReturnsHeader()
    {
        _server
            .Given(Request.Create().WithPath("/header").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"height":"9","time":"2024-01-01T00:00:00Z","proposer_address":"P"}
                }
                """));

        var header = await _client.GetHeaderAsync();
        Assert.Equal(9L, header.Height);
    }

    [Fact]
    public async Task GetHeaderByHash_WhenResultIsDirectHeader_ReturnsHeader()
    {
        _server
            .Given(Request.Create().WithPath("/header_by_hash").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"height":"10","time":"2024-01-01T00:00:00Z","proposer_address":"P2"}
                }
                """));

        var header = await _client.GetHeaderByHashAsync("AABB");
        Assert.Equal(10L, header.Height);
    }

    [Fact]
    public async Task GetBlockByHashAsync_MissingBlock_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/block_by_hash").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"block_id":{"hash":"X"}}
                }
                """));

        await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetBlockByHashAsync("ABCD"));
    }

    [Fact]
    public async Task GetBlockAsync_MissingBlockIdHash_DefaultsToEmptyHash()
    {
        _server
            .Given(Request.Create().WithPath("/block").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "block":{
                      "header":{"height":"1","time":"2024-01-01T00:00:00Z","proposer_address":"P"},
                      "data":{"txs":[]}
                    }
                  }
                }
                """));

        var block = await _client.GetBlockAsync();
        Assert.Equal(string.Empty, block.Hash);
    }

    [Fact]
    public async Task GetBlockByHashAsync_MissingBlockIdHash_FallsBackToInputHash()
    {
        _server
            .Given(Request.Create().WithPath("/block_by_hash").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "block":{
                      "header":{"height":"1","time":"2024-01-01T00:00:00Z","proposer_address":"P"},
                      "data":{"txs":[]}
                    }
                  }
                }
                """));

        var block = await _client.GetBlockByHashAsync("ABCD");
        Assert.Equal("ABCD", block.Hash);
    }

    [Fact]
    public async Task GetGenesisChunkAsync_MissingData_DefaultsToEmptyString()
    {
        _server
            .Given(Request.Create().WithPath("/genesis_chunked").WithParam("chunk", "1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"chunk":1,"total":2}
                }
                """));

        var result = await _client.GetGenesisChunkAsync(1);
        Assert.Equal(string.Empty, result.Data);
    }

    [Fact]
    public async Task GetCommitAsync_WithHeight_UsesHeightQuery()
    {
        _server
            .Given(Request.Create().WithPath("/commit").WithParam("height", "9").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{}
                }
                """));

        var result = await _client.GetCommitAsync(9);
        Assert.Equal(bool.FalseString, result["canonical"]);
    }

    [Fact]
    public async Task GetBlockResultsAsync_WithHeight_UsesHeightQuery()
    {
        _server
            .Given(Request.Create().WithPath("/block_results").WithParam("height", "8").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"height":"8","txs_results":[]}
                }
                """));

        var result = await _client.GetBlockResultsAsync(8);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRpcResultAsync_Http500_PreservesStatusCode()
    {
        _server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetStatusAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task GetRpcResultNodeAsync_Http500_PreservesStatusCode()
    {
        _server
            .Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetNetInfoAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task GetRpcResultNodeAsync_NullResult_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/net_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1
                }
                """));

        await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.GetNetInfoAsync());
    }

    [Fact]
    public async Task PostRpcResultAsync_Http500_PreservesStatusCode()
    {
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<Core.Exceptions.CometBftRestException>(() => _client.BroadcastTxAsync("dHgx"));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }
}
