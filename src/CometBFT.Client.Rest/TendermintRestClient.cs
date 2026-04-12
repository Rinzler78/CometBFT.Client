using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Rest.Json;

namespace CometBFT.Client.Rest;

/// <summary>
/// HTTP/JSON-RPC 2.0 implementation of <see cref="ICometBftRestClient"/> targeting CometBFT v0.38.
/// </summary>
public sealed class CometBftRestClient : ICometBftRestClient
{
    private readonly HttpClient _http;
    private readonly CometBftRestOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftRestClient"/>.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> configured with the CometBFT base address.</param>
    /// <param name="options">The REST client configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <c>null</c>.</exception>
    public CometBftRestClient(HttpClient httpClient, CometBftRestOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    // ── IHealthService ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync("/health", cancellationToken).ConfigureAwait(false);
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
        var result = await GetRpcResultAsync<RpcStatusResult>("/status", cancellationToken).ConfigureAwait(false);

        if (result.NodeInfo is null || result.SyncInfo is null)
        {
            throw new CometBftRestException("Status response is missing required fields (node_info or sync_info).");
        }

        return (MapNodeInfo(result.NodeInfo), MapSyncInfo(result.SyncInfo));
    }

    /// <inheritdoc />
    public async Task<NetworkInfo> GetNetInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetRpcResultNodeAsync("/net_info", cancellationToken).ConfigureAwait(false);
        var listeners = result["listeners"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        var peers = result["peers"]?.AsArray().Select(MapNetworkPeer).ToList() ?? [];

        return new NetworkInfo(
            Listening: result["listening"]?.GetValue<bool>() ?? false,
            Listeners: listeners.AsReadOnly(),
            PeerCount: peers.Count,
            Peers: peers.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetConsensusStateAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetRpcResultNodeAsync("/consensus_state", cancellationToken).ConfigureAwait(false);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["round_state"] = result["round_state"]?.ToJsonString() ?? string.Empty,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> DumpConsensusStateAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetRpcResultNodeAsync("/dump_consensus_state", cancellationToken).ConfigureAwait(false);
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
        var result = await GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
        var parameters = result["consensus_params"];
        var validatorTypes = parameters?["validator"]?["pub_key_types"]?.AsArray()
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToList() ?? [];

        return new ConsensusParamsInfo(
            BlockMaxBytes: ParseLongNode(parameters?["block"]?["max_bytes"]),
            BlockMaxGas: ParseLongNode(parameters?["block"]?["max_gas"]),
            EvidenceMaxAgeNumBlocks: ParseLongNode(parameters?["evidence"]?["max_age_num_blocks"]),
            EvidenceMaxAgeDuration: parameters?["evidence"]?["max_age_duration"]?.GetValue<string>() ?? string.Empty,
            ValidatorPubKeyTypes: validatorTypes.AsReadOnly(),
            VersionApp: ParseLongNode(parameters?["version"]?["app"]));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetGenesisAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetRpcResultNodeAsync("/genesis", cancellationToken).ConfigureAwait(false);
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
        var result = await GetRpcResultNodeAsync($"/genesis_chunked?chunk={chunk}", cancellationToken).ConfigureAwait(false);
        return new GenesisChunk(
            Chunk: (int)ParseLongNode(result["chunk"]),
            Total: (int)ParseLongNode(result["total"]),
            Data: result["data"]?.GetValue<string>() ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<UnconfirmedTxsInfo> GetUnconfirmedTxsAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = limit.HasValue ? $"/unconfirmed_txs?limit={limit.Value}" : "/unconfirmed_txs";
        var result = await GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
        return MapUnconfirmedTxs(result);
    }

    /// <inheritdoc />
    public async Task<UnconfirmedTxsInfo> GetNumUnconfirmedTxsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetRpcResultNodeAsync("/num_unconfirmed_txs", cancellationToken).ConfigureAwait(false);
        return MapUnconfirmedTxs(result);
    }

    // ── IBlockService ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Block> GetBlockAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/block?height={height.Value}" : "/block";
        var result = await GetRpcResultAsync<RpcBlockIdResult>(query, cancellationToken).ConfigureAwait(false);

        if (result.Block is null)
        {
            throw new CometBftRestException("Block response is missing required fields.");
        }

        return MapBlock(result.Block, result.BlockId?.Hash ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<Block> GetBlockByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var normalizedHash = hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hash : "0x" + hash;
        var result = await GetRpcResultAsync<RpcBlockIdResult>(
            $"/block_by_hash?hash={normalizedHash}", cancellationToken).ConfigureAwait(false);

        if (result.Block is null)
        {
            throw new CometBftRestException("Block response is missing required fields.");
        }

        return MapBlock(result.Block, result.BlockId?.Hash ?? hash);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TxResult>> GetBlockResultsAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/block_results?height={height.Value}" : "/block_results";
        var result = await GetRpcResultAsync<RpcBlockResultsResult>(query, cancellationToken).ConfigureAwait(false);
        var blockHeight = ParseLong(result.Height);
        return (result.TxsResults ?? [])
            .Select((r, i) => MapTxResult(r, string.Empty, blockHeight, i))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<BlockHeader> GetHeaderAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/header?height={height.Value}" : "/header";
        var result = await GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
        return MapHeader(result["header"] ?? result);
    }

    /// <inheritdoc />
    public async Task<BlockHeader> GetHeaderByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var normalizedHash = hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hash : "0x" + hash;
        var result = await GetRpcResultNodeAsync($"/header_by_hash?hash={normalizedHash}", cancellationToken).ConfigureAwait(false);
        return MapHeader(result["header"] ?? result);
    }

    /// <inheritdoc />
    public async Task<BlockchainInfo> GetBlockchainAsync(long? minHeight = null, long? maxHeight = null, CancellationToken cancellationToken = default)
    {
        var qs = BuildQueryString(
            minHeight.HasValue ? ("minHeight", minHeight.Value.ToString(CultureInfo.InvariantCulture)) : default,
            maxHeight.HasValue ? ("maxHeight", maxHeight.Value.ToString(CultureInfo.InvariantCulture)) : default);
        var result = await GetRpcResultNodeAsync($"/blockchain{qs}", cancellationToken).ConfigureAwait(false);
        var headers = result["block_metas"]?.AsArray()
            .Select(meta => MapHeader(meta?["header"]))
            .ToList() ?? [];
        return new BlockchainInfo(ParseLongNode(result["last_height"]), headers.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetCommitAsync(long? height = null, CancellationToken cancellationToken = default)
    {
        var query = height.HasValue ? $"/commit?height={height.Value}" : "/commit";
        var result = await GetRpcResultNodeAsync(query, cancellationToken).ConfigureAwait(false);
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
        var qs = BuildQueryString(
            ("query", query),
            page.HasValue ? ("page", page.Value.ToString(CultureInfo.InvariantCulture)) : default,
            perPage.HasValue ? ("per_page", perPage.Value.ToString(CultureInfo.InvariantCulture)) : default,
            !string.IsNullOrWhiteSpace(orderBy) ? ("order_by", orderBy) : default);
        var result = await GetRpcResultNodeAsync($"/block_search{qs}", cancellationToken).ConfigureAwait(false);
        var blocks = result["blocks"]?.AsArray()
            .Select(block => MapBlockNode(block))
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
        var qs = BuildQueryString(
            height.HasValue ? ("height", height.Value.ToString(CultureInfo.InvariantCulture)) : default,
            page.HasValue ? ("page", page.Value.ToString(CultureInfo.InvariantCulture)) : default,
            perPage.HasValue ? ("per_page", perPage.Value.ToString(CultureInfo.InvariantCulture)) : default);
        var result = await GetRpcResultAsync<RpcValidatorsResult>($"/validators{qs}", cancellationToken).ConfigureAwait(false);
        return (result.Validators ?? [])
            .Select(MapValidator)
            .ToList()
            .AsReadOnly();
    }

    // ── ITxService ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TxResult> GetTxAsync(string hash, bool prove = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var normalizedHash = hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hash : "0x" + hash;
        var result = await GetRpcResultAsync<RpcTx>(
            $"/tx?hash={normalizedHash}&prove={prove.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}",
            cancellationToken).ConfigureAwait(false);

        if (result.TxResult is null)
        {
            throw new CometBftRestException("Tx response is missing required fields.");
        }

        return MapTxResult(result.TxResult, result.Hash, ParseLong(result.Height), result.Index);
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
        var qs = BuildQueryString(
            ("query", query),
            prove.HasValue ? ("prove", prove.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()) : default,
            page.HasValue ? ("page", page.Value.ToString(CultureInfo.InvariantCulture)) : default,
            perPage.HasValue ? ("per_page", perPage.Value.ToString(CultureInfo.InvariantCulture)) : default);
        var result = await GetRpcResultAsync<RpcTxSearchResult>($"/tx_search{qs}", cancellationToken).ConfigureAwait(false);
        return (result.Txs ?? [])
            .Select(tx => MapTxResult(tx.TxResult!, tx.Hash, ParseLong(tx.Height), tx.Index))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> BroadcastTxAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await PostRpcResultAsync<RpcBroadcastResult>(
            "broadcast_tx_async", txBytes, cancellationToken).ConfigureAwait(false);
        return MapBroadcastResult(result);
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> BroadcastTxSyncAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await PostRpcResultAsync<RpcBroadcastResult>(
            "broadcast_tx_sync", txBytes, cancellationToken).ConfigureAwait(false);
        return MapBroadcastResult(result);
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> BroadcastTxCommitAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await PostRpcResultAsync<RpcBroadcastResult>(
            "broadcast_tx_commit", txBytes, cancellationToken).ConfigureAwait(false);
        return MapBroadcastResult(result);
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> CheckTxAsync(string txBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(txBytes);
        var result = await GetRpcResultNodeAsync($"/check_tx?tx={Uri.EscapeDataString(txBytes)}", cancellationToken).ConfigureAwait(false);
        return new BroadcastTxResult(
            Code: (uint)ParseLongNode(result["code"]),
            Data: result["data"]?.GetValue<string>(),
            Log: result["log"]?.GetValue<string>(),
            Codespace: result["codespace"]?.GetValue<string>(),
            Hash: string.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> BroadcastEvidenceAsync(string evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var result = await GetRpcResultNodeAsync($"/broadcast_evidence?evidence={Uri.EscapeDataString(evidence)}", cancellationToken).ConfigureAwait(false);
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
        await GetRpcResultNodeAsync($"/dial_seeds?peers={peersJson}", cancellationToken).ConfigureAwait(false);
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
        await GetRpcResultNodeAsync(url, cancellationToken).ConfigureAwait(false);
    }

    // ── IAbciService ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetAbciInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetRpcResultAsync<RpcAbciInfoResult>("/abci_info", cancellationToken).ConfigureAwait(false);
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
        var qs = BuildQueryString(
            ("path", path),
            ("data", data),
            height.HasValue ? ("height", height.Value.ToString(CultureInfo.InvariantCulture)) : default,
            ("prove", prove.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()));
        var result = await GetRpcResultAsync<RpcAbciQueryResult>($"/abci_query{qs}", cancellationToken).ConfigureAwait(false);
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<T> GetRpcResultAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        JsonRpcResponse<T>? envelope;
        try
        {
            using var response = await _http.GetAsync(
                relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            envelope = await JsonSerializer.DeserializeAsync<JsonRpcResponse<T>>(
                stream, CometBftJsonContext.Default.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new CometBftRestException($"HTTP request to '{relativeUrl}' failed.", ex);
        }
        catch (JsonException ex)
        {
            throw new CometBftRestException($"Failed to deserialize response from '{relativeUrl}'.", ex);
        }

        if (envelope is null)
        {
            throw new CometBftRestException($"Received null response from '{relativeUrl}'.");
        }

        if (envelope.Error is not null)
        {
            throw new CometBftRestException(
                envelope.Error.Message ?? "Unknown RPC error.",
                envelope.Error.Code);
        }

        if (envelope.Result is null)
        {
            throw new CometBftRestException($"RPC result was null for '{relativeUrl}'.");
        }

        return envelope.Result;
    }

    private async Task<JsonNode> GetRpcResultNodeAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        JsonNode? root;
        try
        {
            using var response = await _http.GetAsync(
                relativeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new CometBftRestException($"HTTP request to '{relativeUrl}' failed.", ex);
        }
        catch (JsonException ex)
        {
            throw new CometBftRestException($"Failed to deserialize response from '{relativeUrl}'.", ex);
        }

        if (root is null)
        {
            throw new CometBftRestException($"Received null response from '{relativeUrl}'.");
        }

        var error = root["error"];
        if (error is not null)
        {
            throw new CometBftRestException(
                error["message"]?.GetValue<string>() ?? "Unknown RPC error.",
                (int)ParseLongNode(error["code"]));
        }

        return root["result"] ?? throw new CometBftRestException($"RPC result was null for '{relativeUrl}'.");
    }

    private async Task<T> PostRpcResultAsync<T>(string method, string txBytes, CancellationToken cancellationToken)
    {
        var request = new JsonRpcBroadcastRequest
        {
            Method = method,
            Params = new JsonRpcBroadcastParams { Tx = txBytes },
        };

        HttpResponseMessage response;
        try
        {
            var json = JsonSerializer.Serialize(request, CometBftJsonContext.Default.JsonRpcBroadcastRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            response = await _http.PostAsync("/", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new CometBftRestException($"JSON-RPC call '{method}' failed.", ex);
        }

        return await ReadRpcResponseAsync<T>(response, method, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadRpcResponseAsync<T>(
        HttpResponseMessage response,
        string context,
        CancellationToken cancellationToken)
    {
        JsonRpcResponse<T>? envelope;
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            envelope = await JsonSerializer.DeserializeAsync<JsonRpcResponse<T>>(
                stream, CometBftJsonContext.Default.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new CometBftRestException($"Failed to deserialize response for '{context}'.", ex);
        }

        if (envelope is null)
        {
            throw new CometBftRestException($"Received null response for '{context}'.");
        }

        if (envelope.Error is not null)
        {
            throw new CometBftRestException(
                envelope.Error.Message ?? "Unknown RPC error.",
                envelope.Error.Code);
        }

        if (envelope.Result is null)
        {
            throw new CometBftRestException($"RPC result was null for '{context}'.");
        }

        return envelope.Result;
    }

    private static string BuildQueryString(params (string? Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrEmpty(p.Key) && !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key!)}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? "?" + qs : string.Empty;
    }

    private static long ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static long ParseLongNode(JsonNode? node)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<string>(out var stringValue))
            {
                return ParseLong(stringValue);
            }
        }

        return 0;
    }

    private static NodeInfo MapNodeInfo(RpcNodeInfo raw)
    {
        var pv = raw.ProtocolVersion;
        return new NodeInfo(
            raw.Id,
            raw.ListenAddr,
            raw.Network,
            raw.Version,
            raw.Channels,
            raw.Moniker,
            new Core.Domain.ProtocolVersion(
                ulong.TryParse(pv?.P2P, out var p2p) ? p2p : 0,
                ulong.TryParse(pv?.Block, out var block) ? block : 0,
                ulong.TryParse(pv?.App, out var app) ? app : 0));
    }

    private static SyncInfo MapSyncInfo(RpcSyncInfo raw) =>
        new(
            raw.LatestBlockHash,
            raw.LatestAppHash,
            ParseLong(raw.LatestBlockHeight),
            raw.LatestBlockTime,
            raw.EarliestBlockHash,
            raw.EarliestAppHash,
            ParseLong(raw.EarliestBlockHeight),
            raw.EarliestBlockTime,
            raw.CatchingUp);

    private static Block MapBlock(RpcBlock raw, string hash)
    {
        if (raw.Header is null)
        {
            throw new CometBftRestException("Block response is missing required field: block.header.");
        }

        return new Block(
            Height: ParseLong(raw.Header.Height),
            Hash: hash,
            Time: raw.Header.Time,
            Proposer: raw.Header.ProposerAddress,
            Txs: (raw.Data?.Txs ?? []).AsReadOnly());
    }

    private static Block MapBlockNode(JsonNode? node)
    {
        var blockNode = node?["block"] ?? node;
        var headerNode = blockNode?["header"];
        if (headerNode is null)
        {
            throw new CometBftRestException("Block response is missing required field: block.header.");
        }

        var txs = blockNode?["data"]?["txs"]?.AsArray().Select(tx => tx?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        return new Block(
            Height: ParseLongNode(headerNode["height"]),
            Hash: node?["block_id"]?["hash"]?.GetValue<string>() ?? string.Empty,
            Time: DateTimeOffset.TryParse(headerNode["time"]?.GetValue<string>(), out var time) ? time : DateTimeOffset.MinValue,
            Proposer: headerNode["proposer_address"]?.GetValue<string>() ?? string.Empty,
            Txs: txs.AsReadOnly());
    }

    private static BlockHeader MapHeader(JsonNode? headerNode)
    {
        if (headerNode is null)
        {
            throw new CometBftRestException("Header response is missing required fields.");
        }

        return new BlockHeader(
            Version: headerNode["version"]?["block"]?.GetValue<string>() ?? string.Empty,
            ChainId: headerNode["chain_id"]?.GetValue<string>() ?? string.Empty,
            Height: ParseLongNode(headerNode["height"]),
            Time: DateTimeOffset.TryParse(headerNode["time"]?.GetValue<string>(), out var time) ? time : DateTimeOffset.MinValue,
            LastBlockId: headerNode["last_block_id"]?["hash"]?.GetValue<string>() ?? string.Empty,
            LastCommitHash: headerNode["last_commit_hash"]?.GetValue<string>() ?? string.Empty,
            DataHash: headerNode["data_hash"]?.GetValue<string>() ?? string.Empty,
            ValidatorsHash: headerNode["validators_hash"]?.GetValue<string>() ?? string.Empty,
            NextValidatorsHash: headerNode["next_validators_hash"]?.GetValue<string>() ?? string.Empty,
            ConsensusHash: headerNode["consensus_hash"]?.GetValue<string>() ?? string.Empty,
            AppHash: headerNode["app_hash"]?.GetValue<string>() ?? string.Empty,
            LastResultsHash: headerNode["last_results_hash"]?.GetValue<string>() ?? string.Empty,
            EvidenceHash: headerNode["evidence_hash"]?.GetValue<string>() ?? string.Empty,
            ProposerAddress: headerNode["proposer_address"]?.GetValue<string>() ?? string.Empty);
    }

    private static TxResult MapTxResult(RpcTxResult raw, string hash, long height, int index) =>
        new(
            Hash: hash,
            Height: height,
            Index: index,
            TxBytes: string.Empty,
            Code: raw.Code,
            Data: raw.Data,
            Log: raw.Log,
            Info: raw.Info,
            GasWanted: ParseLong(raw.GasWanted),
            GasUsed: ParseLong(raw.GasUsed),
            Events: (raw.Events ?? []).Select(MapEvent).ToList().AsReadOnly(),
            Codespace: raw.Codespace);

    private static TendermintEvent MapEvent(RpcEvent raw) =>
        new(raw.Type, (raw.Attributes ?? []).Select(MapAttribute).ToList().AsReadOnly());

    private static AbciEventEntry MapAttribute(RpcAttribute raw) =>
        new(raw.Key, raw.Value, raw.Index);

    private static Validator MapValidator(RpcValidator raw) =>
        new(
            raw.Address,
            raw.PubKey?.Value ?? string.Empty,
            ParseLong(raw.VotingPower),
            ParseLong(raw.ProposerPriority));

    private static NetworkPeer MapNetworkPeer(JsonNode? node) =>
        new(
            NodeId: node?["node_info"]?["id"]?.GetValue<string>() ?? string.Empty,
            Moniker: node?["node_info"]?["moniker"]?.GetValue<string>() ?? string.Empty,
            Network: node?["node_info"]?["network"]?.GetValue<string>() ?? string.Empty,
            RemoteIp: node?["remote_ip"]?.GetValue<string>() ?? string.Empty,
            ConnectionStatus: node?["connection_status"]?.ToJsonString() ?? string.Empty);

    private static UnconfirmedTxsInfo MapUnconfirmedTxs(JsonNode? node)
    {
        var txs = node?["txs"]?.AsArray().Select(tx => tx?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        return new UnconfirmedTxsInfo(
            Count: (int)ParseLongNode(node?["n_txs"]),
            Total: (int)ParseLongNode(node?["total"]),
            TotalBytes: (int)ParseLongNode(node?["total_bytes"]),
            Txs: txs.AsReadOnly());
    }

    private static BroadcastTxResult MapBroadcastResult(RpcBroadcastResult raw) =>
        new(raw.Code, raw.Data, raw.Log, raw.Codespace, raw.Hash);
}
