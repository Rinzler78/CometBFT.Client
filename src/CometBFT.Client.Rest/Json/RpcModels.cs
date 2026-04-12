using System.Text.Json.Serialization;

namespace CometBFT.Client.Rest.Json;

// ── Raw RPC model types used only for JSON deserialization ──────────────────
// These mirror the CometBFT v0.38 JSON-RPC wire format. They are internal
// and mapped to clean domain types before leaving the assembly boundary.

internal sealed class RpcStatusResult
{
    [JsonPropertyName("node_info")]
    public RpcNodeInfo? NodeInfo { get; init; }

    [JsonPropertyName("sync_info")]
    public RpcSyncInfo? SyncInfo { get; init; }
}

internal sealed class RpcNodeInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("listen_addr")]
    public string ListenAddr { get; init; } = string.Empty;

    [JsonPropertyName("network")]
    public string Network { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("channels")]
    public string Channels { get; init; } = string.Empty;

    [JsonPropertyName("moniker")]
    public string Moniker { get; init; } = string.Empty;

    [JsonPropertyName("protocol_version")]
    public RpcProtocolVersion? ProtocolVersion { get; init; }
}

internal sealed class RpcProtocolVersion
{
    [JsonPropertyName("p2p")]
    public string P2P { get; init; } = "0";

    [JsonPropertyName("block")]
    public string Block { get; init; } = "0";

    [JsonPropertyName("app")]
    public string App { get; init; } = "0";
}

internal sealed class RpcSyncInfo
{
    [JsonPropertyName("latest_block_hash")]
    public string LatestBlockHash { get; init; } = string.Empty;

    [JsonPropertyName("latest_app_hash")]
    public string LatestAppHash { get; init; } = string.Empty;

    [JsonPropertyName("latest_block_height")]
    public string LatestBlockHeight { get; init; } = "0";

    [JsonPropertyName("latest_block_time")]
    public DateTimeOffset LatestBlockTime { get; init; }

    [JsonPropertyName("earliest_block_hash")]
    public string EarliestBlockHash { get; init; } = string.Empty;

    [JsonPropertyName("earliest_app_hash")]
    public string EarliestAppHash { get; init; } = string.Empty;

    [JsonPropertyName("earliest_block_height")]
    public string EarliestBlockHeight { get; init; } = "0";

    [JsonPropertyName("earliest_block_time")]
    public DateTimeOffset EarliestBlockTime { get; init; }

    [JsonPropertyName("catching_up")]
    public bool CatchingUp { get; init; }
}

internal sealed class RpcBlockResult
{
    [JsonPropertyName("block")]
    public RpcBlock? Block { get; init; }
}

internal sealed class RpcBlock
{
    [JsonPropertyName("header")]
    public RpcBlockHeader? Header { get; init; }

    [JsonPropertyName("data")]
    public RpcBlockData? Data { get; init; }
}

internal sealed class RpcBlockHeader
{
    [JsonPropertyName("version")]
    public RpcBlockVersion? Version { get; init; }

    [JsonPropertyName("chain_id")]
    public string ChainId { get; init; } = string.Empty;

    [JsonPropertyName("height")]
    public string Height { get; init; } = "0";

    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; init; }

    [JsonPropertyName("proposer_address")]
    public string ProposerAddress { get; init; } = string.Empty;
}

internal sealed class RpcBlockVersion
{
    [JsonPropertyName("block")]
    public string Block { get; init; } = "0";
}

internal sealed class RpcBlockData
{
    [JsonPropertyName("txs")]
    public List<string>? Txs { get; init; }
}

internal sealed class RpcBlockIdResult
{
    [JsonPropertyName("block_id")]
    public RpcBlockId? BlockId { get; init; }

    [JsonPropertyName("block")]
    public RpcBlock? Block { get; init; }
}

internal sealed class RpcBlockId
{
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;
}

internal sealed class RpcBlockResultsResult
{
    [JsonPropertyName("height")]
    public string Height { get; init; } = "0";

    [JsonPropertyName("txs_results")]
    public List<RpcTxResult>? TxsResults { get; init; }
}

internal sealed class RpcTxResult
{
    [JsonPropertyName("code")]
    public uint Code { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("log")]
    public string? Log { get; init; }

    [JsonPropertyName("info")]
    public string? Info { get; init; }

    [JsonPropertyName("gas_wanted")]
    public string GasWanted { get; init; } = "0";

    [JsonPropertyName("gas_used")]
    public string GasUsed { get; init; } = "0";

    [JsonPropertyName("events")]
    public List<RpcEvent>? Events { get; init; }

    [JsonPropertyName("codespace")]
    public string? Codespace { get; init; }
}

internal sealed class RpcEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public List<RpcAttribute>? Attributes { get; init; }
}

internal sealed class RpcAttribute
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("index")]
    public bool Index { get; init; }
}

internal sealed class RpcValidatorsResult
{
    [JsonPropertyName("block_height")]
    public string BlockHeight { get; init; } = "0";

    [JsonPropertyName("validators")]
    public List<RpcValidator>? Validators { get; init; }

    [JsonPropertyName("count")]
    public string Count { get; init; } = "0";

    [JsonPropertyName("total")]
    public string Total { get; init; } = "0";
}

internal sealed class RpcValidator
{
    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    [JsonPropertyName("pub_key")]
    public RpcPubKey? PubKey { get; init; }

    [JsonPropertyName("voting_power")]
    public string VotingPower { get; init; } = "0";

    [JsonPropertyName("proposer_priority")]
    public string ProposerPriority { get; init; } = "0";
}

internal sealed class RpcPubKey
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}

internal sealed class RpcTxSearchResult
{
    [JsonPropertyName("txs")]
    public List<RpcTx>? Txs { get; init; }

    [JsonPropertyName("total_count")]
    public string TotalCount { get; init; } = "0";
}

internal sealed class RpcTx
{
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;

    [JsonPropertyName("height")]
    public string Height { get; init; } = "0";

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("tx")]
    public string TxBytes { get; init; } = string.Empty;

    [JsonPropertyName("tx_result")]
    public RpcTxResult? TxResult { get; init; }
}

internal sealed class RpcBroadcastResult
{
    [JsonPropertyName("code")]
    public uint Code { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("log")]
    public string? Log { get; init; }

    [JsonPropertyName("codespace")]
    public string? Codespace { get; init; }

    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;
}

internal sealed class RpcAbciInfoResult
{
    [JsonPropertyName("response")]
    public RpcAbciResponse? Response { get; init; }
}

internal sealed class RpcAbciResponse
{
    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("app_version")]
    public string? AppVersion { get; init; }

    [JsonPropertyName("last_block_height")]
    public string? LastBlockHeight { get; init; }

    [JsonPropertyName("last_block_app_hash")]
    public string? LastBlockAppHash { get; init; }
}

internal sealed class RpcAbciQueryResult
{
    [JsonPropertyName("response")]
    public RpcAbciQueryResponse? Response { get; init; }
}

internal sealed class RpcAbciQueryResponse
{
    [JsonPropertyName("code")]
    public uint Code { get; init; }

    [JsonPropertyName("log")]
    public string? Log { get; init; }

    [JsonPropertyName("info")]
    public string? Info { get; init; }

    [JsonPropertyName("index")]
    public string? Index { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("height")]
    public string? Height { get; init; }

    [JsonPropertyName("codespace")]
    public string? Codespace { get; init; }
}
