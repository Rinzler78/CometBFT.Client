using System.Globalization;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Rest.Internal;
using CometBFT.Client.Rest.Json;
using Microsoft.Extensions.Options;

namespace CometBFT.Client.Rest;

/// <summary>
/// HTTP/JSON-RPC 2.0 implementation of <see cref="ICometBftRestClient"/> targeting CometBFT v0.38.
/// </summary>
public sealed class CometBftRestClient : ICometBftRestClient
{
    private readonly RpcHttpPipeline _pipeline;

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftRestClient"/>.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> configured with the CometBFT base address.</param>
    /// <param name="options">The REST client configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <c>null</c>.</exception>
    public CometBftRestClient(HttpClient httpClient, IOptions<CometBftRestOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _pipeline = new RpcHttpPipeline(httpClient);
    }

    // ── IHealthService ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _pipeline.GetHttpClient().GetAsync("/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new CometBftRestException("Health check request failed.", ex);
        }
    }

    // ── IStatusService ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<(NodeInfo NodeInfo, SyncInfo SyncInfo)> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultAsync<RpcStatusResult>("/status", cancellationToken).ConfigureAwait(false);

        if (result.NodeInfo is null || result.SyncInfo is null)
        {
            throw new CometBftRestException("Status response is missing required fields (node_info or sync_info).");
        }

        return (RestResponseMapper.MapNodeInfo(result.NodeInfo), RestResponseMapper.MapSyncInfo(result.SyncInfo));
    }

    /// <inheritdoc />
    public async Task<NetworkInfo> GetNetInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultNodeAsync("/net_info", cancellationToken).ConfigureAwait(false);
        var listeners = result["listeners"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        var peers = result["peers"]?.AsArray().Select(RestResponseMapper.MapNetworkPeer).ToList() ?? [];

        return new NetworkInfo(
            Listening: result["listening"]?.GetValue<bool>() ?? false,
            Listeners: listeners.AsReadOnly(),
            PeerCount: peers.Count,
            Peers: peers.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetConsensusStateAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultNodeAsync("/consensus_state", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["round_state"] = result["round_state"]?.ToJsonString() ?? string.Empty,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> DumpConsensusStateAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultNodeAsync("/dump_consensus_state", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["round_state"] = result["round_state"]?.ToJsonString() ?? string.Empty,
            ["peers"] = result["peers"]?.ToJsonString() ?? "[]",
        };
    }

    /// <inheritdoc />
    public async Task<ConsensusParamsInfo> GetConsensusParamsAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/consensus_params?height={height.Value}" : "/consensus_params";
        var result = await _pipeline.GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
        var parameters = result["consensus_params"];
        var validatorTypes = parameters?["validator"]?["pub_key_types"]?.AsArray()
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToList() ?? [];

        return new ConsensusParamsInfo(
            BlockMaxBytes: RestClientHelpers.ParseLongNode(parameters?["block"]?["max_bytes"]),
            BlockMaxGas: RestClientHelpers.ParseLongNode(parameters?["block"]?["max_gas"]),
            EvidenceMaxAgeNumBlocks: RestClientHelpers.ParseLongNode(parameters?["evidence"]?["max_age_num_blocks"]),
            EvidenceMaxAgeDuration: parameters?["evidence"]?["max_age_duration"]?.GetValue<string>() ?? string.Empty,
            ValidatorPubKeyTypes: validatorTypes.AsReadOnly(),
            VersionApp: RestClientHelpers.ParseLongNode(parameters?["version"]?["app"]));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetGenesisAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultNodeAsync("/genesis", cancellationToken).ConfigureAwait(false);
        var genesis = result["genesis"];
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["genesis_time"] = genesis?["genesis_time"]?.GetValue<string>() ?? string.Empty,
            ["chain_id"] = genesis?["chain_id"]?.GetValue<string>() ?? string.Empty,
            ["initial_height"] = genesis?["initial_height"]?.GetValue<string>() ?? string.Empty,
            ["validators_count"] = genesis?["validators"]?.AsArray().Count.ToString(CultureInfo.InvariantCulture) ?? "0",
            ["app_hash"] = genesis?["app_hash"]?.GetValue<string>() ?? string.Empty,
        };
    }

    /// <inheritdoc />
    public async Task<GenesisChunk> GetGenesisChunkAsync(int chunk, CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultNodeAsync($"/genesis_chunked?chunk={chunk}", cancellationToken).ConfigureAwait(false);
        return new GenesisChunk(
            Chunk: (int)RestClientHelpers.ParseLongNode(result["chunk"]),
            Total: (int)RestClientHelpers.ParseLongNode(result["total"]),
            Data: result["data"]?.GetValue<string>() ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<UnconfirmedTxsInfo> GetUnconfirmedTxsAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = limit.HasValue ? $"/unconfirmed_txs?limit={limit.Value}" : "/unconfirmed_txs";
        var result = await _pipeline.GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
        return RestResponseMapper.MapUnconfirmedTxs(result);
    }

    /// <inheritdoc />
    public async Task<UnconfirmedTxsInfo> GetNumUnconfirmedTxsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultNodeAsync("/num_unconfirmed_txs", cancellationToken).ConfigureAwait(false);
        return RestResponseMapper.MapUnconfirmedTxs(result);
    }

    // ── IBlockService ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Block> GetBlockAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/block?height={height.Value}" : "/block";
        var result = await _pipeline.GetRpcResultAsync<RpcBlockIdResult>(query, cancellationToken).ConfigureAwait(false);

        if (result.Block is null)
        {
            throw new CometBftRestException("Block response is missing required fields.");
        }

        return RestResponseMapper.MapBlock(result.Block, result.BlockId?.Hash ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<Block> GetBlockByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var result = await _pipeline.GetRpcResultAsync<RpcBlockIdResult>(
            $"/block_by_hash?hash={RestClientHelpers.NormalizeHash(hash)}", cancellationToken).ConfigureAwait(false);

        if (result.Block is null)
        {
            throw new CometBftRestException("Block response is missing required fields.");
        }

        return RestResponseMapper.MapBlock(result.Block, result.BlockId?.Hash ?? hash);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TxResult>> GetBlockResultsAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/block_results?height={height.Value}" : "/block_results";
        var result = await _pipeline.GetRpcResultAsync<RpcBlockResultsResult>(query, cancellationToken).ConfigureAwait(false);
        var blockHeight = RestClientHelpers.ParseLong(result.Height);
        return (result.TxsResults ?? [])
            .Select((r, i) => RestResponseMapper.MapTxResult(r, string.Empty, blockHeight, i))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<BlockHeader> GetHeaderAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/header?height={height.Value}" : "/header";
        var result = await _pipeline.GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
        return RestResponseMapper.MapHeader(result["header"] ?? result);
    }

    /// <inheritdoc />
    public async Task<BlockHeader> GetHeaderByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var result = await _pipeline.GetRpcResultNodeAsync(
            $"/header_by_hash?hash={RestClientHelpers.NormalizeHash(hash)}", cancellationToken).ConfigureAwait(false);
        return RestResponseMapper.MapHeader(result["header"] ?? result);
    }

    /// <inheritdoc />
    public async Task<BlockchainInfo> GetBlockchainAsync(long? minHeight = null, long? maxHeight = null, CancellationToken cancellationToken = default)
    {
        var qs = RestClientHelpers.BuildQueryString(
            minHeight.HasValue ? ("minHeight", minHeight.Value.ToString(CultureInfo.InvariantCulture)) : default,
            maxHeight.HasValue ? ("maxHeight", maxHeight.Value.ToString(CultureInfo.InvariantCulture)) : default);
        var result = await _pipeline.GetRpcResultNodeAsync($"/blockchain{qs}", cancellationToken).ConfigureAwait(false);
        var headers = result["block_metas"]?.AsArray()
            .Select(meta => RestResponseMapper.MapHeader(meta?["header"]))
            .ToList() ?? [];
        return new BlockchainInfo(RestClientHelpers.ParseLongNode(result["last_height"]), headers.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetCommitAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/commit?height={height.Value}" : "/commit";
        var result = await _pipeline.GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["height"] = result["signed_header"]?["header"]?["height"]?.GetValue<string>() ?? string.Empty,
            ["hash"] = result["signed_header"]?["commit"]?["block_id"]?["hash"]?.GetValue<string>() ?? string.Empty,
            ["canonical"] = result["canonical"]?.GetValue<bool>().ToString() ?? bool.FalseString,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Block>> SearchBlocksAsync(
        string query,
        int? page = null,
        int? perPage = null,
        string? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var qs = RestClientHelpers.BuildQueryString(
            ("query", query),
            page.HasValue ? ("page", page.Value.ToString(CultureInfo.InvariantCulture)) : default,
            perPage.HasValue ? ("per_page", perPage.Value.ToString(CultureInfo.InvariantCulture)) : default,
            !string.IsNullOrWhiteSpace(orderBy) ? ("order_by", orderBy) : default);
        var result = await _pipeline.GetRpcResultNodeAsync($"/block_search{qs}", cancellationToken).ConfigureAwait(false);
        var blocks = result["blocks"]?.AsArray()
            .Select(block => RestResponseMapper.MapBlockNode(block))
            .ToList();
        return blocks is not null ? blocks.AsReadOnly() : Array.Empty<Block>();
    }

    // ── IValidatorService ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<Validator>> GetValidatorsAsync(
        long? height = null,
        int? page = null,
        int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var qs = RestClientHelpers.BuildQueryString(
            height.HasValue ? ("height", height.Value.ToString(CultureInfo.InvariantCulture)) : default,
            page.HasValue ? ("page", page.Value.ToString(CultureInfo.InvariantCulture)) : default,
            perPage.HasValue ? ("per_page", perPage.Value.ToString(CultureInfo.InvariantCulture)) : default);
        var result = await _pipeline.GetRpcResultAsync<RpcValidatorsResult>($"/validators{qs}", cancellationToken).ConfigureAwait(false);
        return (result.Validators ?? [])
            .Select(RestResponseMapper.MapValidator)
            .ToList()
            .AsReadOnly();
    }

    // ── ITxService ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TxResult> GetTxAsync(string hash, bool prove = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var result = await _pipeline.GetRpcResultAsync<RpcTx>(
            $"/tx?hash={RestClientHelpers.NormalizeHash(hash)}&prove={prove.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}",
            cancellationToken).ConfigureAwait(false);

        if (result.TxResult is null)
        {
            throw new CometBftRestException("Tx response is missing required fields.");
        }

        return RestResponseMapper.MapTxResult(result.TxResult, result.Hash, RestClientHelpers.ParseLong(result.Height), result.Index);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TxResult>> SearchTxAsync(
        string query,
        bool? prove = null,
        int? page = null,
        int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var qs = RestClientHelpers.BuildQueryString(
            ("query", query),
            prove.HasValue ? ("prove", prove.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()) : default,
            page.HasValue ? ("page", page.Value.ToString(CultureInfo.InvariantCulture)) : default,
            perPage.HasValue ? ("per_page", perPage.Value.ToString(CultureInfo.InvariantCulture)) : default);
        var result = await _pipeline.GetRpcResultAsync<RpcTxSearchResult>($"/tx_search{qs}", cancellationToken).ConfigureAwait(false);
        return (result.Txs ?? [])
            .Select(tx => RestResponseMapper.MapTxResult(tx.TxResult!, tx.Hash, RestClientHelpers.ParseLong(tx.Height), tx.Index))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> BroadcastTxAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await _pipeline.PostRpcResultAsync<RpcBroadcastResult>(
            "broadcast_tx_async", txBytes, cancellationToken).ConfigureAwait(false);
        return RestResponseMapper.MapBroadcastResult(result);
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> BroadcastTxSyncAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await _pipeline.PostRpcResultAsync<RpcBroadcastResult>(
            "broadcast_tx_sync", txBytes, cancellationToken).ConfigureAwait(false);
        return RestResponseMapper.MapBroadcastResult(result);
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> BroadcastTxCommitAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await _pipeline.PostRpcResultAsync<RpcBroadcastResult>(
            "broadcast_tx_commit", txBytes, cancellationToken).ConfigureAwait(false);
        return RestResponseMapper.MapBroadcastResult(result);
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> CheckTxAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await _pipeline.GetRpcResultNodeAsync($"/check_tx?tx={Uri.EscapeDataString(txBytes)}", cancellationToken).ConfigureAwait(false);
        return new BroadcastTxResult(
            Code: (uint)RestClientHelpers.ParseLongNode(result["code"]),
            Data: result["data"]?.GetValue<string>(),
            Log: result["log"]?.GetValue<string>(),
            Codespace: result["codespace"]?.GetValue<string>(),
            Hash: string.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> BroadcastEvidenceAsync(string evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var result = await _pipeline.GetRpcResultNodeAsync($"/broadcast_evidence?evidence={Uri.EscapeDataString(evidence)}", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hash"] = result["hash"]?.GetValue<string>() ?? string.Empty,
        };
    }

    // ── IUnsafeService ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task DialSeedsAsync(
        IReadOnlyList<string> peers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peers);
        var peersJson = Uri.EscapeDataString(
            "[" + string.Join(",", peers.Select(p => $"\"{p}\"")) + "]");
        await _pipeline.GetRpcResultNodeAsync($"/dial_seeds?peers={peersJson}", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DialPeersAsync(
        IReadOnlyList<string> peers,
        bool persistent = false,
        bool unconditional = false,
        bool isPrivate = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peers);
        var peersJson = Uri.EscapeDataString(
            "[" + string.Join(",", peers.Select(p => $"\"{p}\"")) + "]");
        var url = $"/dial_peers?peers={peersJson}" +
                  $"&persistent={persistent.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}" +
                  $"&unconditional={unconditional.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}" +
                  $"&private={isPrivate.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}";
        await _pipeline.GetRpcResultNodeAsync(url, cancellationToken).ConfigureAwait(false);
    }

    // ── IAbciService ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetAbciInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.GetRpcResultAsync<RpcAbciInfoResult>("/abci_info", cancellationToken).ConfigureAwait(false);
        var response = result.Response;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data"] = response?.Data ?? string.Empty,
            ["version"] = response?.Version ?? string.Empty,
            ["app_version"] = response?.AppVersion ?? string.Empty,
            ["last_block_height"] = response?.LastBlockHeight ?? string.Empty,
            ["last_block_app_hash"] = response?.LastBlockAppHash ?? string.Empty,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> AbciQueryAsync(
        string path,
        string data,
        long? height = null,
        bool prove = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(data);
        var qs = RestClientHelpers.BuildQueryString(
            ("path", path),
            ("data", data),
            height.HasValue ? ("height", height.Value.ToString(CultureInfo.InvariantCulture)) : default,
            ("prove", prove.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()));
        var result = await _pipeline.GetRpcResultAsync<RpcAbciQueryResult>($"/abci_query{qs}", cancellationToken).ConfigureAwait(false);
        var response = result.Response;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["code"] = (response?.Code ?? 0).ToString(CultureInfo.InvariantCulture),
            ["log"] = response?.Log ?? string.Empty,
            ["info"] = response?.Info ?? string.Empty,
            ["key"] = response?.Key ?? string.Empty,
            ["value"] = response?.Value ?? string.Empty,
            ["height"] = response?.Height ?? string.Empty,
            ["codespace"] = response?.Codespace ?? string.Empty,
        };
    }
}
