using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.WebSocket.Internal;

/// <summary>
/// Parses raw CometBFT WebSocket JSON messages into typed domain events.
/// Extracted for testability; used exclusively by <see cref="CometBftWebSocketClient"/>.
/// </summary>
internal static class WebSocketMessageParser
{
    internal static Block? ParseNewBlock(JsonNode json)
    {
        var blockNode = json["result"]?["data"]?["value"]?["block"];
        if (blockNode is null)
        {
            return null;
        }

        var header = blockNode["header"];
        var height = long.TryParse(header?["height"]?.GetValue<string>(), out var h) ? h : 0;
        var time = header?["time"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue;
        var proposer = header?["proposer_address"]?.GetValue<string>() ?? string.Empty;
        var txsNode = blockNode["data"]?["txs"]?.AsArray();
        var txs = txsNode?.Select(t => t?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        var hash = json["result"]?["data"]?["value"]?["block_id"]?["hash"]?.GetValue<string>() ?? string.Empty;

        return new Block(height, hash, time, proposer, txs.AsReadOnly());
    }

    internal static BlockHeader? ParseNewBlockHeader(JsonNode json)
    {
        var header = json["result"]?["data"]?["value"]?["header"];
        if (header is null)
        {
            return null;
        }

        var version = header["version"]?["block"]?.GetValue<string>() ?? string.Empty;
        var chainId = header["chain_id"]?.GetValue<string>() ?? string.Empty;
        var height = long.TryParse(header["height"]?.GetValue<string>(), out var h) ? h : 0;
        var time = DateTimeOffset.TryParse(header["time"]?.GetValue<string>(), out var t)
            ? t
            : DateTimeOffset.MinValue;
        var lastBlockId = header["last_block_id"]?["hash"]?.GetValue<string>() ?? string.Empty;

        return new BlockHeader(
            Version: version,
            ChainId: chainId,
            Height: height,
            Time: time,
            LastBlockId: lastBlockId,
            LastCommitHash: header["last_commit_hash"]?.GetValue<string>() ?? string.Empty,
            DataHash: header["data_hash"]?.GetValue<string>() ?? string.Empty,
            ValidatorsHash: header["validators_hash"]?.GetValue<string>() ?? string.Empty,
            NextValidatorsHash: header["next_validators_hash"]?.GetValue<string>() ?? string.Empty,
            ConsensusHash: header["consensus_hash"]?.GetValue<string>() ?? string.Empty,
            AppHash: header["app_hash"]?.GetValue<string>() ?? string.Empty,
            LastResultsHash: header["last_results_hash"]?.GetValue<string>() ?? string.Empty,
            EvidenceHash: header["evidence_hash"]?.GetValue<string>() ?? string.Empty,
            ProposerAddress: header["proposer_address"]?.GetValue<string>() ?? string.Empty);
    }

    internal static TxResult? ParseTxResult(JsonNode json)
    {
        var txNode = json["result"]?["data"]?["value"]?["TxResult"];
        if (txNode is null)
        {
            return null;
        }

        var height = long.TryParse(txNode["height"]?.GetValue<string>(), out var h) ? h : 0;
        var index = txNode["index"]?.GetValue<int>() ?? 0;
        var resultNode = txNode["result"];
        var code = resultNode?["code"]?.GetValue<uint>() ?? 0;
        var log = resultNode?["log"]?.GetValue<string>();
        var gasWanted = long.TryParse(resultNode?["gas_wanted"]?.GetValue<string>(), out var gw) ? gw : 0;
        var gasUsed = long.TryParse(resultNode?["gas_used"]?.GetValue<string>(), out var gu) ? gu : 0;

        // Hash from the events map (tx.hash is the canonical source).
        var hash = json["result"]?["events"]?["tx.hash"]?[0]?.GetValue<string>() ?? string.Empty;

        // Events from TxResult.result.events.
        var eventsNode = resultNode?["events"]?.AsArray();
        var events = eventsNode?
            .Select(e => new CometBftEvent(
                e?["type"]?.GetValue<string>() ?? string.Empty,
                (e?["attributes"]?.AsArray() ?? new JsonArray())
                    .Select(a => new AbciEventEntry(
                        a?["key"]?.GetValue<string>() ?? string.Empty,
                        a?["value"]?.GetValue<string>(),
                        a?["index"]?.GetValue<bool>() ?? false))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly()
            ?? (IReadOnlyList<CometBftEvent>)Array.Empty<CometBftEvent>();

        return new TxResult(hash, height, index, string.Empty, code, null, log, null,
            gasWanted, gasUsed, events, null);
    }

    internal static Vote? ParseVote(JsonNode json)
    {
        var voteNode = json["result"]?["data"]?["value"]?["Vote"];
        if (voteNode is null)
        {
            return null;
        }

        var type = voteNode["type"]?.GetValue<int>() ?? 0;
        var height = long.TryParse(voteNode["height"]?.GetValue<string>(), out var h) ? h : 0;
        var round = voteNode["round"]?.GetValue<int>() ?? 0;
        var validatorAddress = voteNode["validator_address"]?.GetValue<string>() ?? string.Empty;
        var timestamp = DateTimeOffset.TryParse(voteNode["timestamp"]?.GetValue<string>(), out var ts)
            ? ts
            : DateTimeOffset.MinValue;

        return new Vote(type, height, round, validatorAddress, timestamp);
    }

    internal static ReadOnlyCollection<Validator>? ParseValidatorSetUpdates(JsonNode json)
    {
        var updatesNode = json["result"]?["data"]?["value"]?["validator_updates"]?.AsArray();
        if (updatesNode is null)
        {
            return null;
        }

        return updatesNode
            .Select(v => new Validator(
                v?["address"]?.GetValue<string>() ?? string.Empty,
                v?["pub_key"]?["data"]?.GetValue<string>() ?? string.Empty,
                long.TryParse(v?["power"]?.GetValue<string>(), out var vp) ? vp : 0,
                0))
            .ToList()
            .AsReadOnly();
    }
}
