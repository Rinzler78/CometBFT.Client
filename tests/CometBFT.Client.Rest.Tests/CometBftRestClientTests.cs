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
/// Integration-style unit tests for <see cref="CometBftRestClient"/> using WireMock.Net.
/// </summary>
public sealed class CometBftRestClientTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly CometBftRestClient _client;
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a WireMock server and creates a client pointed at it.
    /// </summary>
    public CometBftRestClientTests()
    {
        _server = WireMockServer.Start();
        var options = new CometBftRestOptions { BaseUrl = _server.Url! };
        _http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _client = new CometBftRestClient(_http, Options.Create(options));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsTrue_WhenServerResponds200()
    {
        _server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}"));

        var result = await _client.GetHealthAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsFalse_WhenServerResponds500()
    {
        _server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        var result = await _client.GetHealthAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task GetHealthAsync_ThrowsCometBftRestException_WhenRequestFails()
    {
        _server.Stop();

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetHealthAsync());
    }

    [Fact]
    public async Task GetStatusAsync_ParsesNodeInfoAndSyncInfo()
    {
        _server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "result": {
                    "node_info": {
                      "id": "node123",
                      "listen_addr": "tcp://0.0.0.0:26656",
                      "network": "testnet",
                      "version": "0.38.9",
                      "channels": "40",
                      "moniker": "mynode",
                      "protocol_version": { "p2p": "8", "block": "11", "app": "0" }
                    },
                    "sync_info": {
                      "latest_block_hash": "AABBCC",
                      "latest_app_hash": "DDEEFF",
                      "latest_block_height": "100",
                      "latest_block_time": "2024-01-01T00:00:00Z",
                      "earliest_block_hash": "001122",
                      "earliest_app_hash": "334455",
                      "earliest_block_height": "1",
                      "earliest_block_time": "2023-01-01T00:00:00Z",
                      "catching_up": false
                    }
                  }
                }
                """));

        var (nodeInfo, syncInfo) = await _client.GetStatusAsync();

        Assert.Equal("node123", nodeInfo.Id);
        Assert.Equal("testnet", nodeInfo.Network);
        Assert.Equal("0.38.9", nodeInfo.Version);
        Assert.Equal(8UL, nodeInfo.ProtocolVersion.P2P);
        Assert.Equal(100L, syncInfo.LatestBlockHeight);
        Assert.Equal("AABBCC", syncInfo.LatestBlockHash);
        Assert.False(syncInfo.CatchingUp);
    }

    [Fact]
    public async Task GetBlockAsync_WithoutHeight_ReturnsBlock()
    {
        _server
            .Given(Request.Create().WithPath("/block").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "result": {
                    "block_id": { "hash": "BLOCKHASH" },
                    "block": {
                      "header": {
                        "version": { "block": "11" },
                        "chain_id": "testnet",
                        "height": "42",
                        "time": "2024-06-01T12:00:00Z",
                        "proposer_address": "PROPOSER"
                      },
                      "data": { "txs": ["dHgx", "dHgy"] }
                    }
                  }
                }
                """));

        var block = await _client.GetBlockAsync();

        Assert.Equal(42L, block.Height);
        Assert.Equal("BLOCKHASH", block.Hash);
        Assert.Equal("PROPOSER", block.Proposer);
        Assert.Equal(2, block.Txs.Count);
    }

    [Fact]
    public async Task GetBlockAsync_WithHeight_SendsHeightQueryParam()
    {
        _server
            .Given(Request.Create().WithPath("/block").WithParam("height", "10").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "result": {
                    "block_id": { "hash": "H10" },
                    "block": {
                      "header": {
                        "version": { "block": "11" },
                        "chain_id": "testnet",
                        "height": "10",
                        "time": "2024-01-01T00:00:00Z",
                        "proposer_address": "P"
                      },
                      "data": { "txs": [] }
                    }
                  }
                }
                """));

        var block = await _client.GetBlockAsync(10L);

        Assert.Equal(10L, block.Height);
        Assert.Equal("H10", block.Hash);
    }

    [Fact]
    public async Task GetValidatorsAsync_ReturnsValidatorList()
    {
        _server
            .Given(Request.Create().WithPath("/validators").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "result": {
                    "block_height": "100",
                    "validators": [
                      {
                        "address": "VAL1",
                        "pub_key": { "type": "ed25519", "value": "PUBKEY1" },
                        "voting_power": "1000",
                        "proposer_priority": "0"
                      }
                    ],
                    "count": "1",
                    "total": "1"
                  }
                }
                """));

        var validators = await _client.GetValidatorsAsync();

        Assert.Single(validators);
        Assert.Equal("VAL1", validators[0].Address);
        Assert.Equal(1000L, validators[0].VotingPower);
    }

    [Fact]
    public async Task BroadcastTxSyncAsync_ReturnsBroadcastResult()
    {
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc": "2.0",
                  "id": -1,
                  "result": {
                    "code": 0,
                    "data": "",
                    "log": "ok",
                    "codespace": "",
                    "hash": "TXHASH1"
                  }
                }
                """));

        var result = await _client.BroadcastTxSyncAsync("dHgx");

        Assert.Equal(0u, result.Code);
        Assert.Equal("TXHASH1", result.Hash);
    }

    [Fact]
    public async Task GetAbciInfoAsync_ReturnsDictionary()
    {
        _server
            .Given(Request.Create().WithPath("/abci_info").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "result": {
                    "response": {
                      "data": "myapp",
                      "version": "1.0.0",
                      "app_version": "1",
                      "last_block_height": "100",
                      "last_block_app_hash": "APPHASH"
                    }
                  }
                }
                """));

        var info = await _client.GetAbciInfoAsync();

        Assert.Equal("myapp", info["data"]);
        Assert.Equal("1.0.0", info["version"]);
    }

    [Fact]
    public async Task GetRpcResultAsync_ThrowsCometBftRestException_OnRpcError()
    {
        _server
            .Given(Request.Create().WithPath("/status").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "error": { "code": -32601, "message": "Method not found" }
                }
                """));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetStatusAsync());
    }

    [Fact]
    public async Task GetNetInfoAsync_ReturnsNormalizedNetworkInfo()
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
                    "listening":true,
                    "listeners":["Listener(@)tcp://0.0.0.0:26656"],
                    "peers":[
                      {
                        "remote_ip":"10.0.0.1",
                        "connection_status":{"duration":"1s"},
                        "node_info":{"id":"peer1","moniker":"node-a","network":"theta-testnet-001"}
                      }
                    ]
                  }
                }
                """));

        var info = await _client.GetNetInfoAsync();

        Assert.True(info.Listening);
        Assert.Single(info.Listeners);
        Assert.Single(info.Peers);
        Assert.Equal("peer1", info.Peers[0].NodeId);
    }

    [Fact]
    public async Task GetHeaderAsync_ReturnsHeader()
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
                  "result":{
                    "header":{
                      "version":{"block":"11"},
                      "chain_id":"testnet",
                      "height":"42",
                      "time":"2024-06-01T12:00:00Z",
                      "last_block_id":{"hash":"ABC"},
                      "last_commit_hash":"LC",
                      "data_hash":"DH",
                      "validators_hash":"VH",
                      "next_validators_hash":"NVH",
                      "consensus_hash":"CH",
                      "app_hash":"AH",
                      "last_results_hash":"LRH",
                      "evidence_hash":"EH",
                      "proposer_address":"PROPOSER"
                    }
                  }
                }
                """));

        var header = await _client.GetHeaderAsync();

        Assert.Equal(42L, header.Height);
        Assert.Equal("testnet", header.ChainId);
    }

    [Fact]
    public async Task GetHeaderByHashAsync_NormalizesHash()
    {
        _server
            .Given(Request.Create().WithPath("/header_by_hash").WithParam("hash", "0xABCD").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "header":{
                      "version":{"block":"11"},
                      "chain_id":"testnet",
                      "height":"7",
                      "time":"2024-06-01T12:00:00Z",
                      "last_block_id":{"hash":"PREV"},
                      "last_commit_hash":"LC",
                      "data_hash":"DH",
                      "validators_hash":"VH",
                      "next_validators_hash":"NVH",
                      "consensus_hash":"CH",
                      "app_hash":"AH",
                      "last_results_hash":"LRH",
                      "evidence_hash":"EH",
                      "proposer_address":"PROPOSER"
                    }
                  }
                }
                """));

        var header = await _client.GetHeaderByHashAsync("ABCD");

        Assert.Equal(7L, header.Height);
    }

    [Fact]
    public async Task GetBlockchainAsync_ReturnsHeaders()
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
                    "last_height":"99",
                    "block_metas":[
                      {
                        "header":{
                          "version":{"block":"11"},
                          "chain_id":"testnet",
                          "height":"2",
                          "time":"2024-06-01T12:00:00Z",
                          "last_block_id":{"hash":"PREV"},
                          "last_commit_hash":"LC",
                          "data_hash":"DH",
                          "validators_hash":"VH",
                          "next_validators_hash":"NVH",
                          "consensus_hash":"CH",
                          "app_hash":"AH",
                          "last_results_hash":"LRH",
                          "evidence_hash":"EH",
                          "proposer_address":"PROPOSER"
                        }
                      }
                    ]
                  }
                }
                """));

        var result = await _client.GetBlockchainAsync(1, 2);

        Assert.Equal(99L, result.LastHeight);
        Assert.Single(result.Headers);
    }

    [Fact]
    public async Task GetCommitAsync_ReturnsCommitDictionary()
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
                  "result":{
                    "canonical":true,
                    "signed_header":{
                      "header":{"height":"88"},
                      "commit":{"block_id":{"hash":"COMMITHASH"}}
                    }
                  }
                }
                """));

        var result = await _client.GetCommitAsync();

        Assert.Equal("88", result["height"]);
        Assert.Equal("COMMITHASH", result["hash"]);
    }

    [Fact]
    public async Task CheckTxAsync_ReturnsCheckResult()
    {
        _server
            .Given(Request.Create().WithPath("/check_tx").WithParam("tx", "dHgx").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"code":0,"data":"","log":"ok","codespace":""}
                }
                """));

        var result = await _client.CheckTxAsync("dHgx");

        Assert.Equal(0u, result.Code);
        Assert.Equal("ok", result.Log);
    }

    [Fact]
    public async Task GetConsensusParamsAsync_ReturnsNormalizedParams()
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
                    "block_height":"1",
                    "consensus_params":{
                      "block":{"max_bytes":"22020096","max_gas":"-1"},
                      "evidence":{"max_age_num_blocks":"100000","max_age_duration":"172800000000000"},
                      "validator":{"pub_key_types":["ed25519"]},
                      "version":{"app":"1"}
                    }
                  }
                }
                """));

        var result = await _client.GetConsensusParamsAsync();

        Assert.Equal(22020096L, result.BlockMaxBytes);
        Assert.Single(result.ValidatorPubKeyTypes);
    }

    [Fact]
    public async Task GetGenesisAsync_ReturnsSummaryDictionary()
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
                  "result":{
                    "genesis":{
                      "genesis_time":"2024-01-01T00:00:00Z",
                      "chain_id":"theta-testnet-001",
                      "initial_height":"1",
                      "validators":[{"address":"A"}],
                      "app_hash":"APPHASH"
                    }
                  }
                }
                """));

        var result = await _client.GetGenesisAsync();

        Assert.Equal("theta-testnet-001", result["chain_id"]);
        Assert.Equal("1", result["validators_count"]);
    }

    [Fact]
    public async Task GetGenesisChunkAsync_ReturnsChunk()
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
                  "result":{"chunk":1,"total":3,"data":"YWJj"}
                }
                """));

        var result = await _client.GetGenesisChunkAsync(1);

        Assert.Equal(1, result.Chunk);
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task GetConsensusStateAsync_ReturnsStateDictionary()
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
                  "result":{"round_state":{"height/round/step":"88/0/1"}}
                }
                """));

        var result = await _client.GetConsensusStateAsync();

        Assert.Contains("88/0/1", result["round_state"]);
    }

    [Fact]
    public async Task DumpConsensusStateAsync_ReturnsDumpDictionary()
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
                  "result":{"round_state":{"height/round/step":"88/0/1"},"peers":[]}
                }
                """));

        var result = await _client.DumpConsensusStateAsync();

        Assert.Equal("[]", result["peers"]);
    }

    [Fact]
    public async Task GetUnconfirmedTxsAsync_ReturnsMempoolSummary()
    {
        _server
            .Given(Request.Create().WithPath("/unconfirmed_txs").WithParam("limit", "1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"n_txs":"1","total":"2","total_bytes":"4","txs":["dHgx"]}
                }
                """));

        var result = await _client.GetUnconfirmedTxsAsync(1);

        Assert.Equal(1, result.Count);
        Assert.Single(result.Txs);
    }

    [Fact]
    public async Task GetNumUnconfirmedTxsAsync_ReturnsCountOnlySummary()
    {
        _server
            .Given(Request.Create().WithPath("/num_unconfirmed_txs").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"n_txs":"0","total":"0","total_bytes":"0","txs":[]}
                }
                """));

        var result = await _client.GetNumUnconfirmedTxsAsync();

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Txs);
    }

    [Fact]
    public async Task SearchBlocksAsync_ReturnsMatchingBlocks()
    {
        _server
            .Given(Request.Create().WithPath("/block_search").WithParam("query", "block.height>1").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "blocks":[
                      {
                        "block_id":{"hash":"BLOCKHASH"},
                        "block":{
                          "header":{"height":"12","time":"2024-06-01T12:00:00Z","proposer_address":"PROP"},
                          "data":{"txs":["dHgx"]}
                        }
                      }
                    ],
                    "total_count":1
                  }
                }
                """));

        var result = await _client.SearchBlocksAsync("block.height>1");

        Assert.Single(result);
        Assert.Equal(12L, result[0].Height);
    }

    [Fact]
    public async Task BroadcastEvidenceAsync_ReturnsHashDictionary()
    {
        _server
            .Given(Request.Create().WithPath("/broadcast_evidence").WithParam("evidence", "ZXZpZGVuY2U=").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{"hash":"EVIDENCEHASH"}
                }
                """));

        var result = await _client.BroadcastEvidenceAsync("ZXZpZGVuY2U=");

        Assert.Equal("EVIDENCEHASH", result["hash"]);
    }

    [Fact]
    public async Task GetBlockByHashAsync_NormalizesHashAndReturnsBlock()
    {
        _server
            .Given(Request.Create().WithPath("/block_by_hash").WithParam("hash", "0xABCD").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"jsonrpc":"2.0","id":1,"result":{"block_id":{"hash":"ABCD"},"block":{"header":{"version":{"block":"11"},"chain_id":"testnet","height":"5","time":"2024-06-01T12:00:00Z","proposer_address":"P"},"data":{"txs":[]}}}}
                """));

        var block = await _client.GetBlockByHashAsync("ABCD");

        Assert.Equal(5L, block.Height);
        Assert.Equal("ABCD", block.Hash);
    }

    [Fact]
    public async Task GetBlockResultsAsync_ReturnsTxResults()
    {
        _server
            .Given(Request.Create().WithPath("/block_results").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"jsonrpc":"2.0","id":1,"result":{"height":"7","txs_results":[{"code":0,"data":"","log":"ok","info":"","gas_wanted":"1","gas_used":"1","events":[],"codespace":""}]}}
                """));

        var results = await _client.GetBlockResultsAsync();

        Assert.Single(results);
        Assert.Equal(7L, results[0].Height);
    }

    [Fact]
    public async Task GetTxAsync_ReturnsTransaction()
    {
        _server
            .Given(Request.Create().WithPath("/tx").WithParam("hash", "0xTXHASH").WithParam("prove", "false").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"jsonrpc":"2.0","id":1,"result":{"hash":"TXHASH","height":"8","index":0,"tx":"dHgx","tx_result":{"code":0,"data":"","log":"ok","info":"","gas_wanted":"1","gas_used":"1","events":[],"codespace":""}}}
                """));

        var tx = await _client.GetTxAsync("TXHASH");

        Assert.Equal("TXHASH", tx.Hash);
        Assert.Equal(8L, tx.Height);
    }

    [Fact]
    public async Task SearchTxAsync_ReturnsTransactions()
    {
        _server
            .Given(Request.Create().WithPath("/tx_search").WithParam("query", "tx.height=8").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"jsonrpc":"2.0","id":1,"result":{"txs":[{"hash":"TXHASH","height":"8","index":0,"tx":"dHgx","tx_result":{"code":0,"data":"","log":"ok","info":"","gas_wanted":"1","gas_used":"1","events":[],"codespace":""}}],"total_count":"1"}}
                """));

        var txs = await _client.SearchTxAsync("tx.height=8");

        Assert.Single(txs);
        Assert.Equal("TXHASH", txs[0].Hash);
    }

    [Fact]
    public async Task BroadcastTxAsync_ReturnsBroadcastResult()
    {
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"jsonrpc":"2.0","id":1,"result":{"code":0,"data":"","log":"ok","codespace":"","hash":"HASHASYNC"}}
                """));

        var result = await _client.BroadcastTxAsync("dHgx");

        Assert.Equal("HASHASYNC", result.Hash);
    }

    [Fact]
    public async Task BroadcastTxCommitAsync_ReturnsBroadcastResult()
    {
        _server
            .Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"jsonrpc":"2.0","id":1,"result":{"code":0,"data":"","log":"ok","codespace":"","hash":"HASHCOMMIT"}}
                """));

        var result = await _client.BroadcastTxCommitAsync("dHgx");

        Assert.Equal("HASHCOMMIT", result.Hash);
    }

    [Fact]
    public async Task AbciQueryAsync_ReturnsDictionary()
    {
        _server
            .Given(Request.Create().WithPath("/abci_query").WithParam("path", "/store/key").WithParam("data", "YWJj").WithParam("prove", "false").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {"jsonrpc":"2.0","id":1,"result":{"response":{"code":0,"log":"ok","info":"","key":"key","value":"value","height":"7","codespace":""}}}
                """));

        var result = await _client.AbciQueryAsync("/store/key", "YWJj");

        Assert.Equal("key", result["key"]);
        Assert.Equal("value", result["value"]);
    }

    // ── IUnsafeService ───────────────────────────────────────────────────────

    [Fact]
    public async Task DialSeedsAsync_Succeeds_WithPeerList()
    {
        _server
            .Given(Request.Create().WithPath("/dial_seeds").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}"));

        var ex = await Record.ExceptionAsync(() =>
            _client.DialSeedsAsync(["abc123@10.0.0.1:26656", "def456@10.0.0.2:26656"]));

        Assert.Null(ex);
    }

    [Fact]
    public async Task DialSeedsAsync_ThrowsArgumentNullException_ForNullPeers()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _client.DialSeedsAsync(null!));
    }

    [Fact]
    public async Task DialSeedsAsync_ThrowsCometBftRestException_OnRpcError()
    {
        _server
            .Given(Request.Create().WithPath("/dial_seeds").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found\"}}"));

        await Assert.ThrowsAsync<CometBftRestException>(() =>
            _client.DialSeedsAsync(["abc123@10.0.0.1:26656"]));
    }

    [Fact]
    public async Task DialPeersAsync_Succeeds_WithDefaults()
    {
        _server
            .Given(Request.Create().WithPath("/dial_peers").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}"));

        var ex = await Record.ExceptionAsync(() =>
            _client.DialPeersAsync(["abc123@10.0.0.1:26656"]));

        Assert.Null(ex);
    }

    [Fact]
    public async Task DialPeersAsync_EncodesAllBooleanOptions()
    {
        // Verify all boolean flags are forwarded in the query string.
        _server
            .Given(Request.Create()
                .WithPath("/dial_peers")
                .WithParam("persistent", "true")
                .WithParam("unconditional", "true")
                .WithParam("private", "true")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{}}"));

        var ex = await Record.ExceptionAsync(() =>
            _client.DialPeersAsync(
                ["abc123@10.0.0.1:26656"],
                persistent: true,
                unconditional: true,
                isPrivate: true));

        Assert.Null(ex);
    }

    [Fact]
    public async Task DialPeersAsync_ThrowsArgumentNullException_ForNullPeers()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _client.DialPeersAsync(null!));
    }

    [Fact]
    public async Task DialPeersAsync_ThrowsCometBftRestException_OnRpcError()
    {
        _server
            .Given(Request.Create().WithPath("/dial_peers").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32601,\"message\":\"Method not found\"}}"));

        await Assert.ThrowsAsync<CometBftRestException>(() =>
            _client.DialPeersAsync(["abc123@10.0.0.1:26656"]));
    }

    // ── Crash-scenario regression tests ─────────────────────────────────────
    // These tests document the behaviors that caused demo crashes and verify
    // correct exception propagation from relay-disabled endpoints.

    [Fact]
    public async Task DumpConsensusStateAsync_PeersContainsObjectNodes_ValueIsRawJsonBlob()
    {
        // Verifies that "peers" is returned as a raw JSON string containing curly braces.
        // Callers must use Markup.Escape() before rendering this value in Spectre.Console.
        _server
            .Given(Request.Create().WithPath("/dump_consensus_state").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "jsonrpc":"2.0",
                  "id":1,
                  "result":{
                    "round_state":{"height/round/step":"100/0/1"},
                    "peers":[
                      {
                        "node_address":"abc123@10.0.0.1:26656",
                        "peer_state":{"round_state":{"height":"99","round":"0","step":"6"}}
                      }
                    ]
                  }
                }
                """));

        var result = await _client.DumpConsensusStateAsync();

        Assert.True(result.ContainsKey("peers"));
        Assert.Contains("{", result["peers"], StringComparison.Ordinal);
        Assert.Contains("node_address", result["peers"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetGenesisAsync_Returns500_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/genesis").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.GetGenesisAsync());
    }

    [Fact]
    public async Task SearchTxAsync_Returns500_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/tx_search").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.SearchTxAsync("tx.height=1"));
    }

    [Fact]
    public async Task AbciQueryAsync_Returns500_ThrowsCometBftRestException()
    {
        _server
            .Given(Request.Create().WithPath("/abci_query").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<CometBftRestException>(() => _client.AbciQueryAsync("/app/version", ""));
    }
}
