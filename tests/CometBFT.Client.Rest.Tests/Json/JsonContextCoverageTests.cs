using System.Text.Json;
using CometBFT.Client.Rest.Json;
using Xunit;

namespace CometBFT.Client.Rest.Tests.Json;

/// <summary>
/// Exercises each type registered in <see cref="CometBftJsonContext"/> so that
/// the STJ source-generated TypeInfo paths are measured by coverage.
/// </summary>
public sealed class JsonContextCoverageTests
{
    [Fact]
    public void JsonRpcBroadcastRequest_RoundTrip()
    {
        var req = new JsonRpcBroadcastRequest
        {
            Id = 1,
            Method = "broadcast_tx_sync",
            Params = new JsonRpcBroadcastParams { Tx = "AABB==" }
        };

        var json = JsonSerializer.Serialize(req, CometBftJsonContext.Default.JsonRpcBroadcastRequest);
        var roundtrip = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcBroadcastRequest);

        Assert.NotNull(roundtrip);
        Assert.Equal(req.Method, roundtrip!.Method);
        Assert.Equal(req.Params.Tx, roundtrip.Params.Tx);
    }

    [Fact]
    public void JsonRpcResponse_RpcStatusResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"node_info":{"id":"abc"},"sync_info":{"catching_up":false,"latest_block_height":"100","latest_block_time":"2024-01-01T00:00:00Z","earliest_block_height":"1","earliest_block_time":"2024-01-01T00:00:00Z"}}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcStatusResult);

        Assert.NotNull(result);
        Assert.NotNull(result!.Result?.NodeInfo);
        Assert.Equal("abc", result.Result!.NodeInfo!.Id);
    }

    [Fact]
    public void JsonRpcResponse_RpcBlockIdResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"block_id":{"hash":"DEADBEEF"},"block":{"header":{"height":"10","time":"2024-01-01T00:00:00Z","proposer_address":"AA"},"data":{}}}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcBlockIdResult);

        Assert.NotNull(result);
        Assert.Equal("DEADBEEF", result!.Result?.BlockId?.Hash);
    }

    [Fact]
    public void JsonRpcResponse_RpcBlockResultsResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"height":"5","txs_results":[{"code":0,"gas_wanted":"100","gas_used":"80"}]}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcBlockResultsResult);

        Assert.NotNull(result);
        Assert.Equal("5", result!.Result?.Height);
    }

    [Fact]
    public void JsonRpcResponse_RpcValidatorsResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"block_height":"10","validators":[],"count":"0","total":"0"}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcValidatorsResult);

        Assert.NotNull(result);
        Assert.Equal("10", result!.Result?.BlockHeight);
    }

    [Fact]
    public void JsonRpcResponse_RpcTx_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"hash":"AABB","height":"50","index":0,"tx":"AABB=="}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcTx);

        Assert.NotNull(result);
        Assert.Equal("AABB", result!.Result?.Hash);
    }

    [Fact]
    public void JsonRpcResponse_RpcTxSearchResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"txs":[],"total_count":"0"}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcTxSearchResult);

        Assert.NotNull(result);
        Assert.Equal("0", result!.Result?.TotalCount);
    }

    [Fact]
    public void JsonRpcResponse_RpcBroadcastResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"code":0,"hash":"TXHASH"}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcBroadcastResult);

        Assert.NotNull(result);
        Assert.Equal("TXHASH", result!.Result?.Hash);
    }

    [Fact]
    public void JsonRpcResponse_RpcAbciInfoResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"response":{"data":"myapp","version":"1.0"}}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcAbciInfoResult);

        Assert.NotNull(result);
        Assert.Equal("myapp", result!.Result?.Response?.Data);
    }

    [Fact]
    public void JsonRpcResponse_RpcAbciQueryResult_RoundTrip()
    {
        const string json = """{"jsonrpc":"2.0","id":1,"result":{"response":{"code":0,"log":"ok","height":"10"}}}""";
        var result = JsonSerializer.Deserialize(json, CometBftJsonContext.Default.JsonRpcResponseRpcAbciQueryResult);

        Assert.NotNull(result);
        Assert.Equal("10", result!.Result?.Response?.Height);
    }
}
