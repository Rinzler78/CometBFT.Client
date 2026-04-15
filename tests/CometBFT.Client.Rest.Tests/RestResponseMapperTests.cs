using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Rest.Internal;
using CometBFT.Client.Rest.Json;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

/// <summary>
/// Fixture-based tests for <see cref="RestResponseMapper"/> using real Cosmos Hub payloads
/// captured at block 30674661 via <c>https://cosmoshub.tendermintrpc.lava.build:443</c>.
/// </summary>
public sealed class RestResponseMapperTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static T LoadResult<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var json = File.ReadAllText(path, Encoding.UTF8);
        var response = JsonSerializer.Deserialize<JsonRpcResponse<T>>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return response!.Result!;
    }

    private static JsonNode LoadNode(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var json = File.ReadAllText(path, Encoding.UTF8);
        var response = JsonSerializer.Deserialize<JsonRpcResponse<JsonNode>>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return response!.Result!;
    }

    // ── /status → NodeInfo ───────────────────────────────────────────────────

    [Fact]
    public void MapNodeInfo_RealFixture_NetworkIsCosmosHub()
    {
        var result = LoadResult<RpcStatusResult>("rest_status.json");
        var nodeInfo = RestResponseMapper.MapNodeInfo(result.NodeInfo!);

        Assert.Equal("cosmoshub-4", nodeInfo.Network);
    }

    [Fact]
    public void MapNodeInfo_RealFixture_IdIsPopulated()
    {
        var result = LoadResult<RpcStatusResult>("rest_status.json");
        var nodeInfo = RestResponseMapper.MapNodeInfo(result.NodeInfo!);

        Assert.Equal("5834c06fa88f755e77af848040837792d429976a", nodeInfo.Id); // pragma: allowlist secret
    }

    [Fact]
    public void MapNodeInfo_RealFixture_ProtocolVersionBlock()
    {
        var result = LoadResult<RpcStatusResult>("rest_status.json");
        var nodeInfo = RestResponseMapper.MapNodeInfo(result.NodeInfo!);

        Assert.Equal(11UL, nodeInfo.ProtocolVersion.Block);
    }

    // ── /status → SyncInfo ───────────────────────────────────────────────────

    [Fact]
    public void MapSyncInfo_RealFixture_LatestBlockHeightIsPositive()
    {
        var result = LoadResult<RpcStatusResult>("rest_status.json");
        var syncInfo = RestResponseMapper.MapSyncInfo(result.SyncInfo!);

        Assert.True(syncInfo.LatestBlockHeight > 0);
    }

    [Fact]
    public void MapSyncInfo_RealFixture_LatestBlockHashIsPopulated()
    {
        var result = LoadResult<RpcStatusResult>("rest_status.json");
        var syncInfo = RestResponseMapper.MapSyncInfo(result.SyncInfo!);

        Assert.Equal("11AFA09FB10AED6B06800F8321A480685FD8B10F262A214DB81BE3186320BE7A", syncInfo.LatestBlockHash); // pragma: allowlist secret
    }

    [Fact]
    public void MapSyncInfo_RealFixture_CatchingUpIsFalse()
    {
        var result = LoadResult<RpcStatusResult>("rest_status.json");
        var syncInfo = RestResponseMapper.MapSyncInfo(result.SyncInfo!);

        Assert.False(syncInfo.CatchingUp);
    }

    // ── /block → Block ───────────────────────────────────────────────────────

    [Fact]
    public void MapBlock_RealFixture_HeightEquals30674661()
    {
        var result = LoadResult<RpcBlockIdResult>("rest_block.json");
        var block = RestResponseMapper.MapBlock(result.Block!, result.BlockId!.Hash);

        Assert.Equal(30_674_661L, block.Height);
    }

    [Fact]
    public void MapBlock_RealFixture_HashIsPopulated()
    {
        var result = LoadResult<RpcBlockIdResult>("rest_block.json");
        var block = RestResponseMapper.MapBlock(result.Block!, result.BlockId!.Hash);

        Assert.Equal("7656AFA79A263C93CC20A1F0775DE3F48193FA4BF25D8B8C7CCB4ED8C83C1DC2", block.Hash); // pragma: allowlist secret
    }

    [Fact]
    public void MapBlock_RealFixture_ProposerIsPopulated()
    {
        var result = LoadResult<RpcBlockIdResult>("rest_block.json");
        var block = RestResponseMapper.MapBlock(result.Block!, result.BlockId!.Hash);

        Assert.Equal("638C11545DF20961BDE0373D1602ECECB3BC6CD0", block.Proposer); // pragma: allowlist secret
    }

    [Fact]
    public void MapBlock_RealFixture_HasOneTx()
    {
        var result = LoadResult<RpcBlockIdResult>("rest_block.json");
        var block = RestResponseMapper.MapBlock(result.Block!, result.BlockId!.Hash);

        Assert.Single(block.Txs);
    }

    // ── /block_results → TxResult[] ─────────────────────────────────────────

    [Fact]
    public void MapTxResult_RealFixture_ReturnsNonEmptyList()
    {
        var result = LoadResult<RpcBlockResultsResult>("rest_block_results.json");

        Assert.NotNull(result.TxsResults);
        Assert.NotEmpty(result.TxsResults);
    }

    [Fact]
    public void MapTxResult_RealFixture_FirstTxHasHeight30674661()
    {
        var result = LoadResult<RpcBlockResultsResult>("rest_block_results.json");
        var height = RestClientHelpers.ParseLong(result.Height);
        var txResult = RestResponseMapper.MapTxResult(result.TxsResults![0], hash: string.Empty, height, index: 0);

        Assert.Equal(30_674_661L, txResult.Height);
    }

    [Fact]
    public void MapTxResult_RealFixture_FirstTxCodeIsZero()
    {
        var result = LoadResult<RpcBlockResultsResult>("rest_block_results.json");
        var height = RestClientHelpers.ParseLong(result.Height);
        var txResult = RestResponseMapper.MapTxResult(result.TxsResults![0], hash: string.Empty, height, index: 0);

        Assert.Equal(0u, txResult.Code);
    }

    [Fact]
    public void MapTxResult_RealFixture_FirstTxGasFieldsArePositive()
    {
        var result = LoadResult<RpcBlockResultsResult>("rest_block_results.json");
        var height = RestClientHelpers.ParseLong(result.Height);
        var txResult = RestResponseMapper.MapTxResult(result.TxsResults![0], hash: string.Empty, height, index: 0);

        Assert.True(txResult.GasWanted > 0);
        Assert.True(txResult.GasUsed > 0);
    }

    // ── /header → BlockHeader ────────────────────────────────────────────────

    [Fact]
    public void MapHeader_RealFixture_HeightIsPositive()
    {
        var node = LoadNode("rest_header.json");
        var header = RestResponseMapper.MapHeader(node["header"]);

        Assert.True(header.Height > 0);
    }

    [Fact]
    public void MapHeader_RealFixture_ChainIdIsCosmosHub()
    {
        var node = LoadNode("rest_header.json");
        var header = RestResponseMapper.MapHeader(node["header"]);

        Assert.Equal("cosmoshub-4", header.ChainId);
    }

    [Fact]
    public void MapHeader_RealFixture_ProposerAddressIsPopulated()
    {
        var node = LoadNode("rest_header.json");
        var header = RestResponseMapper.MapHeader(node["header"]);

        Assert.Equal("638C11545DF20961BDE0373D1602ECECB3BC6CD0", header.ProposerAddress); // pragma: allowlist secret
    }

    [Fact]
    public void MapHeader_RealFixture_ValidatorsHashIsPopulated()
    {
        var node = LoadNode("rest_header.json");
        var header = RestResponseMapper.MapHeader(node["header"]);

        Assert.Equal("CE04BC83CA81696E44D060803309FD49B52C88BB0A960B1B7C0AEE76DC9CAC39", header.ValidatorsHash); // pragma: allowlist secret
    }

    // ── /validators → Validator[] ────────────────────────────────────────────

    [Fact]
    public void MapValidator_RealFixture_ReturnsValidators()
    {
        var result = LoadResult<RpcValidatorsResult>("rest_validators.json");

        Assert.NotNull(result.Validators);
        Assert.NotEmpty(result.Validators);
    }

    [Fact]
    public void MapValidator_RealFixture_FirstValidatorAddressIsPopulated()
    {
        var result = LoadResult<RpcValidatorsResult>("rest_validators.json");
        var validator = RestResponseMapper.MapValidator(result.Validators![0]);

        Assert.Equal("56B2F053AD136642D3FC9098FB2DD01454F396D5", validator.Address); // pragma: allowlist secret
    }

    [Fact]
    public void MapValidator_RealFixture_FirstValidatorVotingPowerIsPositive()
    {
        var result = LoadResult<RpcValidatorsResult>("rest_validators.json");
        var validator = RestResponseMapper.MapValidator(result.Validators![0]);

        Assert.Equal(60_157_642L, validator.VotingPower);
    }

    // ── /blockchain → BlockchainInfo (via JsonNode) ──────────────────────────

    [Fact]
    public void MapBlockchainInfo_RealFixture_LastHeightIsPositive()
    {
        var node = LoadNode("rest_blockchain.json");

        var lastHeight = node["last_height"]?.GetValue<string>();
        Assert.NotNull(lastHeight);

        var parsed = long.TryParse(lastHeight, out var h);
        Assert.True(parsed);
        Assert.True(h > 0);
    }

    [Fact]
    public void MapBlockchainInfo_RealFixture_HasTwoBlockMetas()
    {
        var node = LoadNode("rest_blockchain.json");
        var metas = node["block_metas"]?.AsArray();

        Assert.NotNull(metas);
        Assert.Equal(2, metas.Count);
    }

    // ── /unconfirmed_txs → UnconfirmedTxsInfo ────────────────────────────────

    [Fact]
    public void MapUnconfirmedTxs_RealFixture_TotalIsNonNegative()
    {
        var node = LoadNode("rest_unconfirmed_txs.json");
        var info = RestResponseMapper.MapUnconfirmedTxs(node);

        Assert.True(info.Total >= 0);
    }

    [Fact]
    public void MapUnconfirmedTxs_RealFixture_TotalBytesIsNonNegative()
    {
        var node = LoadNode("rest_unconfirmed_txs.json");
        var info = RestResponseMapper.MapUnconfirmedTxs(node);

        Assert.True(info.TotalBytes >= 0);
    }

    // ── Error cases ──────────────────────────────────────────────────────────

    [Fact]
    public void MapBlock_NullHeader_ThrowsCometBftRestException()
    {
        var raw = new RpcBlock { Header = null, Data = null };
        Assert.Throws<CometBftRestException>(() => RestResponseMapper.MapBlock(raw, "HASH"));
    }

    [Fact]
    public void MapHeader_NullNode_ThrowsCometBftRestException()
    {
        Assert.Throws<CometBftRestException>(() => RestResponseMapper.MapHeader(null));
    }

    // ── RestClientHelpers ────────────────────────────────────────────────────

    [Theory]
    [InlineData("AABB", "0xAABB")]
    [InlineData("0xAABB", "0xAABB")] // already prefixed
    [InlineData("0XAABB", "0XAABB")] // uppercase prefix already present
    public void RestClientHelpers_NormalizeHash_PrependsPrefixWhenMissing(string input, string expected)
    {
        Assert.Equal(expected, RestClientHelpers.NormalizeHash(input));
    }

    [Theory]
    [InlineData("42", 42L)]
    [InlineData("0", 0L)]
    [InlineData(null, 0L)]
    [InlineData("", 0L)]
    [InlineData("not-a-number", 0L)]
    public void RestClientHelpers_ParseLong_ReturnsExpected(string? input, long expected)
    {
        Assert.Equal(expected, RestClientHelpers.ParseLong(input));
    }

    [Fact]
    public void RestClientHelpers_BuildQueryString_FiltersNullAndEmptyValues()
    {
        var qs = RestClientHelpers.BuildQueryString(
            ("key1", "val1"),
            ("key2", null),
            ("", "val3"),
            ("key4", ""));

        // Only key1=val1 survives filtering
        Assert.Equal("?key1=val1", qs);
    }

    [Fact]
    public void RestClientHelpers_BuildQueryString_AllEmpty_ReturnsEmptyString()
    {
        var qs = RestClientHelpers.BuildQueryString(("", null), (null, ""));
        Assert.Equal(string.Empty, qs);
    }

    [Fact]
    public void RestClientHelpers_BuildQueryString_MultipleParams_JoinsWithAmpersand()
    {
        var qs = RestClientHelpers.BuildQueryString(("a", "1"), ("b", "2"));
        Assert.Equal("?a=1&b=2", qs);
    }

    [Fact]
    public void RestClientHelpers_BuildQueryString_SpecialChars_AreUrlEncoded()
    {
        var qs = RestClientHelpers.BuildQueryString(("q", "hello world"));
        Assert.Equal("?q=hello%20world", qs);
    }
}
