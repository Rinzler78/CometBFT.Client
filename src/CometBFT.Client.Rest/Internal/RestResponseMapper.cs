using System.Text.Json.Nodes;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Rest.Json;

namespace CometBFT.Client.Rest.Internal;

internal static class RestResponseMapper
{
    internal static NodeInfo MapNodeInfo(RpcNodeInfo raw)
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

    internal static SyncInfo MapSyncInfo(RpcSyncInfo raw) =>
        new(
            raw.LatestBlockHash,
            raw.LatestAppHash,
            RestClientHelpers.ParseLong(raw.LatestBlockHeight),
            raw.LatestBlockTime,
            raw.EarliestBlockHash,
            raw.EarliestAppHash,
            RestClientHelpers.ParseLong(raw.EarliestBlockHeight),
            raw.EarliestBlockTime,
            raw.CatchingUp);

    internal static Block MapBlock(RpcBlock raw, string hash)
    {
        if (raw.Header is null)
        {
            throw new CometBftRestException("Block response is missing required field: block.header.");
        }

        return new Block(
            Height: RestClientHelpers.ParseLong(raw.Header.Height),
            Hash: hash,
            Time: raw.Header.Time,
            Proposer: raw.Header.ProposerAddress,
            Txs: (raw.Data?.Txs ?? []).AsReadOnly());
    }

    internal static Block MapBlockNode(JsonNode? node)
    {
        var blockNode = node?["block"] ?? node;
        var headerNode = blockNode?["header"];
        if (headerNode is null)
        {
            throw new CometBftRestException("Block response is missing required field: block.header.");
        }

        var txs = blockNode?["data"]?["txs"]?.AsArray().Select(tx => tx?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        return new Block(
            Height: RestClientHelpers.ParseLongNode(headerNode["height"]),
            Hash: node?["block_id"]?["hash"]?.GetValue<string>() ?? string.Empty,
            Time: DateTimeOffset.TryParse(headerNode["time"]?.GetValue<string>(), out var time) ? time : DateTimeOffset.MinValue,
            Proposer: headerNode["proposer_address"]?.GetValue<string>() ?? string.Empty,
            Txs: txs.AsReadOnly());
    }

    internal static BlockHeader MapHeader(JsonNode? headerNode)
    {
        if (headerNode is null)
        {
            throw new CometBftRestException("Header response is missing required fields.");
        }

        return new BlockHeader(
            Version: headerNode["version"]?["block"]?.GetValue<string>() ?? string.Empty,
            ChainId: headerNode["chain_id"]?.GetValue<string>() ?? string.Empty,
            Height: RestClientHelpers.ParseLongNode(headerNode["height"]),
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

    internal static TxResult MapTxResult(RpcTxResult raw, string hash, long height, int index) =>
        new(
            Hash: hash,
            Height: height,
            Index: index,
            TxBytes: string.Empty,
            Code: raw.Code,
            Data: raw.Data,
            Log: raw.Log,
            Info: raw.Info,
            GasWanted: RestClientHelpers.ParseLong(raw.GasWanted),
            GasUsed: RestClientHelpers.ParseLong(raw.GasUsed),
            Events: (raw.Events ?? []).Select(MapEvent).ToList().AsReadOnly(),
            Codespace: raw.Codespace);

    internal static CometBftEvent MapEvent(RpcEvent raw) =>
        new(raw.Type, (raw.Attributes ?? []).Select(MapAttribute).ToList().AsReadOnly());

    internal static AbciEventEntry MapAttribute(RpcAttribute raw) =>
        new(raw.Key, raw.Value, raw.Index);

    internal static Validator MapValidator(RpcValidator raw) =>
        new(
            raw.Address,
            raw.PubKey?.Value ?? string.Empty,
            RestClientHelpers.ParseLong(raw.VotingPower),
            RestClientHelpers.ParseLong(raw.ProposerPriority));

    internal static NetworkPeer MapNetworkPeer(JsonNode? node) =>
        new(
            NodeId: node?["node_info"]?["id"]?.GetValue<string>() ?? string.Empty,
            Moniker: node?["node_info"]?["moniker"]?.GetValue<string>() ?? string.Empty,
            Network: node?["node_info"]?["network"]?.GetValue<string>() ?? string.Empty,
            RemoteIp: node?["remote_ip"]?.GetValue<string>() ?? string.Empty,
            ConnectionStatus: node?["connection_status"]?.ToJsonString() ?? string.Empty);

    internal static UnconfirmedTxsInfo MapUnconfirmedTxs(JsonNode? node)
    {
        var txs = node?["txs"]?.AsArray().Select(tx => tx?.GetValue<string>() ?? string.Empty).ToList() ?? [];
        return new UnconfirmedTxsInfo(
            Count: (int)RestClientHelpers.ParseLongNode(node?["n_txs"]),
            Total: (int)RestClientHelpers.ParseLongNode(node?["total"]),
            TotalBytes: (int)RestClientHelpers.ParseLongNode(node?["total_bytes"]),
            Txs: txs.AsReadOnly());
    }

    internal static BroadcastTxResult MapBroadcastResult(RpcBroadcastResult raw) =>
        new(raw.Code, raw.Data, raw.Log, raw.Codespace, raw.Hash);
}
