using System.Text.Json.Serialization;

namespace CometBFT.Client.WebSocket.Json;

// ── Outgoing request types ───────────────────────────────────────────────────

internal sealed class WsSubscribeRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; init; } = "subscribe";

    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("params")]
    public WsSubscribeParams Params { get; init; } = new();
}

internal sealed class WsSubscribeParams
{
    [JsonPropertyName("query")]
    public string Query { get; init; } = string.Empty;
}

internal sealed class WsUnsubscribeAllRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; init; } = "unsubscribe_all";

    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("params")]
    public WsEmptyParams Params { get; init; } = new();
}

internal sealed class WsEmptyParams { }

// ── Incoming envelope ────────────────────────────────────────────────────────

/// <summary>
/// Top-level JSON-RPC envelope received from the CometBFT WebSocket endpoint.
/// Covers both subscribe acknowledgments and event notifications.
/// </summary>
internal sealed class WsEnvelope
{
    /// <summary>
    /// JSON-RPC request id echoed by the server.
    /// Positive for subscribe acks; zero for event notifications.
    /// Some proxy nodes (e.g. Lava) encode the id as a JSON string — AllowReadingFromString handles both.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; init; }

    [JsonPropertyName("result")]
    public WsResult? Result { get; init; }
}

internal sealed class WsResult
{
    /// <summary>
    /// Null for subscribe acks (result is an empty object); populated for events.
    /// </summary>
    [JsonPropertyName("data")]
    public WsEventData? Data { get; init; }

    /// <summary>
    /// Top-level event index map, e.g. <c>{"tx.hash":["…"]}</c>.
    /// Present only for Tx events.
    /// </summary>
    [JsonPropertyName("events")]
    public Dictionary<string, List<string>>? Events { get; init; }
}

// ── Polymorphic event data ───────────────────────────────────────────────────

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WsNewBlockData), "tendermint/event/NewBlock")]
[JsonDerivedType(typeof(WsNewBlockHeaderData), "tendermint/event/NewBlockHeader")]
[JsonDerivedType(typeof(WsTxData), "tendermint/event/Tx")]
[JsonDerivedType(typeof(WsVoteData), "tendermint/event/Vote")]
[JsonDerivedType(typeof(WsValidatorSetUpdatesData), "tendermint/event/ValidatorSetUpdates")]
internal abstract class WsEventData { }

// ── NewBlock ─────────────────────────────────────────────────────────────────

internal sealed class WsNewBlockData : WsEventData
{
    [JsonPropertyName("value")]
    public WsNewBlockValue? Value { get; init; }
}

internal sealed class WsNewBlockValue
{
    [JsonPropertyName("block_id")]
    public WsBlockId? BlockId { get; init; }

    [JsonPropertyName("block")]
    public WsBlock? Block { get; init; }
}

internal sealed class WsBlockId
{
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;
}

internal sealed class WsBlock
{
    [JsonPropertyName("header")]
    public WsBlockHeader? Header { get; init; }

    [JsonPropertyName("data")]
    public WsBlockTxData? Data { get; init; }
}

internal sealed class WsBlockHeader
{
    [JsonPropertyName("height")]
    public string Height { get; init; } = "0";

    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; init; }

    [JsonPropertyName("proposer_address")]
    public string ProposerAddress { get; init; } = string.Empty;
}

internal sealed class WsBlockTxData
{
    [JsonPropertyName("txs")]
    public List<string>? Txs { get; init; }
}

// ── NewBlockHeader ───────────────────────────────────────────────────────────

internal sealed class WsNewBlockHeaderData : WsEventData
{
    [JsonPropertyName("value")]
    public WsNewBlockHeaderValue? Value { get; init; }
}

internal sealed class WsNewBlockHeaderValue
{
    [JsonPropertyName("header")]
    public WsFullBlockHeader? Header { get; init; }
}

internal sealed class WsFullBlockHeader
{
    [JsonPropertyName("version")]
    public WsBlockVersion? Version { get; init; }

    [JsonPropertyName("chain_id")]
    public string ChainId { get; init; } = string.Empty;

    [JsonPropertyName("height")]
    public string Height { get; init; } = "0";

    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;

    [JsonPropertyName("last_block_id")]
    public WsBlockId? LastBlockId { get; init; }

    [JsonPropertyName("last_commit_hash")]
    public string LastCommitHash { get; init; } = string.Empty;

    [JsonPropertyName("data_hash")]
    public string DataHash { get; init; } = string.Empty;

    [JsonPropertyName("validators_hash")]
    public string ValidatorsHash { get; init; } = string.Empty;

    [JsonPropertyName("next_validators_hash")]
    public string NextValidatorsHash { get; init; } = string.Empty;

    [JsonPropertyName("consensus_hash")]
    public string ConsensusHash { get; init; } = string.Empty;

    [JsonPropertyName("app_hash")]
    public string AppHash { get; init; } = string.Empty;

    [JsonPropertyName("last_results_hash")]
    public string LastResultsHash { get; init; } = string.Empty;

    [JsonPropertyName("evidence_hash")]
    public string EvidenceHash { get; init; } = string.Empty;

    [JsonPropertyName("proposer_address")]
    public string ProposerAddress { get; init; } = string.Empty;
}

internal sealed class WsBlockVersion
{
    [JsonPropertyName("block")]
    public string Block { get; init; } = string.Empty;
}

// ── Tx ───────────────────────────────────────────────────────────────────────

internal sealed class WsTxData : WsEventData
{
    [JsonPropertyName("value")]
    public WsTxValue? Value { get; init; }
}

internal sealed class WsTxValue
{
    [JsonPropertyName("TxResult")]
    public WsTxResult? TxResult { get; init; }
}

internal sealed class WsTxResult
{
    [JsonPropertyName("height")]
    public string Height { get; init; } = "0";

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("result")]
    public WsExecResult? Result { get; init; }
}

internal sealed class WsExecResult
{
    [JsonPropertyName("code")]
    public uint Code { get; init; }

    [JsonPropertyName("log")]
    public string? Log { get; init; }

    [JsonPropertyName("gas_wanted")]
    public string GasWanted { get; init; } = "0";

    [JsonPropertyName("gas_used")]
    public string GasUsed { get; init; } = "0";

    [JsonPropertyName("events")]
    public List<WsAbciEvent>? Events { get; init; }
}

internal sealed class WsAbciEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public List<WsAbciAttribute>? Attributes { get; init; }
}

internal sealed class WsAbciAttribute
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("index")]
    public bool Index { get; init; }
}

// ── Vote ─────────────────────────────────────────────────────────────────────

internal sealed class WsVoteData : WsEventData
{
    [JsonPropertyName("value")]
    public WsVoteValue? Value { get; init; }
}

internal sealed class WsVoteValue
{
    [JsonPropertyName("Vote")]
    public WsVote? Vote { get; init; }
}

internal sealed class WsVote
{
    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("height")]
    public string Height { get; init; } = "0";

    [JsonPropertyName("round")]
    public int Round { get; init; }

    [JsonPropertyName("validator_address")]
    public string ValidatorAddress { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;
}

// ── ValidatorSetUpdates ──────────────────────────────────────────────────────

internal sealed class WsValidatorSetUpdatesData : WsEventData
{
    [JsonPropertyName("value")]
    public WsValidatorSetUpdatesValue? Value { get; init; }
}

internal sealed class WsValidatorSetUpdatesValue
{
    [JsonPropertyName("validator_updates")]
    public List<WsValidator>? ValidatorUpdates { get; init; }
}

internal sealed class WsValidator
{
    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    [JsonPropertyName("pub_key")]
    public WsPubKey? PubKey { get; init; }

    [JsonPropertyName("power")]
    public string Power { get; init; } = "0";
}

internal sealed class WsPubKey
{
    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;
}
