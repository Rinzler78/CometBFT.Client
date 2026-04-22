using System.Collections.ObjectModel;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.WebSocket.Json;

namespace CometBFT.Client.WebSocket.Internal;


/// <summary>
/// Maps CometBFT WebSocket wire types to typed domain objects.
/// Extracted for testability; used exclusively by <see cref="CometBftWebSocketClient"/>.
/// </summary>
internal static class WebSocketMessageParser
{
    internal static Block? ParseNewBlock(WsNewBlockData data)
    {
        var value = data.Value;
        if (value?.Block is null)
        {
            return null;
        }

        var header = value.Block.Header;
        var height = long.TryParse(header?.Height, out var h) ? h : 0;
        var time = header?.Time ?? DateTimeOffset.MinValue;
        var proposer = header?.ProposerAddress ?? string.Empty;
        var txs = value.Block.Data?.Txs ?? [];
        var hash = value.BlockId?.Hash ?? string.Empty;

        return new Block(height, hash, time, proposer, txs.AsReadOnly());
    }

    internal static BlockHeader? ParseNewBlockHeader(WsNewBlockHeaderData data)
    {
        var header = data.Value?.Header;
        if (header is null)
        {
            return null;
        }

        var height = long.TryParse(header.Height, out var h) ? h : 0;
        var time = DateTimeOffset.TryParse(header.Time, out var t) ? t : DateTimeOffset.MinValue;

        return new BlockHeader(
            Version: header.Version?.Block ?? string.Empty,
            ChainId: header.ChainId,
            Height: height,
            Time: time,
            LastBlockId: header.LastBlockId?.Hash ?? string.Empty,
            LastCommitHash: header.LastCommitHash,
            DataHash: header.DataHash,
            ValidatorsHash: header.ValidatorsHash,
            NextValidatorsHash: header.NextValidatorsHash,
            ConsensusHash: header.ConsensusHash,
            AppHash: header.AppHash,
            LastResultsHash: header.LastResultsHash,
            EvidenceHash: header.EvidenceHash,
            ProposerAddress: header.ProposerAddress);
    }

    internal static TxResult? ParseTxResult(WsTxData data, Dictionary<string, List<string>>? resultEvents)
    {
        var txResult = data.Value?.TxResult;
        if (txResult is null)
        {
            return null;
        }

        var height = long.TryParse(txResult.Height, out var h) ? h : 0;
        var execResult = txResult.Result;
        var gasWanted = long.TryParse(execResult?.GasWanted, out var gw) ? gw : 0;
        var gasUsed = long.TryParse(execResult?.GasUsed, out var gu) ? gu : 0;

        var hash = resultEvents?.GetValueOrDefault("tx.hash") is { Count: > 0 } hashes
            ? hashes[0]
            : string.Empty;

        var events = (execResult?.Events ?? [])
            .Select(e => new CometBftEvent(
                e.Type,
                (e.Attributes ?? [])
                    .Select(a => new AbciEventEntry(a.Key, a.Value, a.Index))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly();

        return new TxResult(hash, height, txResult.Index, string.Empty,
            execResult?.Code ?? 0, null, execResult?.Log, null,
            gasWanted, gasUsed, events, null);
    }

    internal static Vote? ParseVote(WsVoteData data)
    {
        var vote = data.Value?.Vote;
        if (vote is null)
        {
            return null;
        }

        var height = long.TryParse(vote.Height, out var h) ? h : 0;
        var timestamp = DateTimeOffset.TryParse(vote.Timestamp, out var ts) ? ts : DateTimeOffset.MinValue;

        return new Vote(vote.Type, height, vote.Round, vote.ValidatorAddress, timestamp);
    }

    internal static ReadOnlyCollection<Validator>? ParseValidatorSetUpdates(WsValidatorSetUpdatesData data)
    {
        var updates = data.Value?.ValidatorUpdates;
        if (updates is null)
        {
            return null;
        }

        return updates
            .Select(v => new Validator(
                v.Address,
                v.PubKey?.Data ?? string.Empty,
                long.TryParse(v.Power, out var vp) ? vp : 0,
                0))
            .ToList()
            .AsReadOnly();
    }

    internal static ValidatorSetUpdatesData? ParseValidatorSetUpdatesData(WsValidatorSetUpdatesData data)
    {
        var validators = ParseValidatorSetUpdates(data);
        return validators is null ? null : new ValidatorSetUpdatesData(validators);
    }

    internal static NewBlockEventsData? ParseNewBlockEvents(WsNewBlockEventsData data)
    {
        var value = data.Value;
        if (value is null)
        {
            return null;
        }

        var header = value.Header;
        var height = long.TryParse(value.Height, out var h) ? h : 0;
        var time = DateTimeOffset.TryParse(header?.Time, out var t) ? t : DateTimeOffset.MinValue;

        var blockHeader = new BlockHeader(
            Version: header?.Version?.Block ?? string.Empty,
            ChainId: header?.ChainId ?? string.Empty,
            Height: height,
            Time: time,
            LastBlockId: header?.LastBlockId?.Hash ?? string.Empty,
            LastCommitHash: header?.LastCommitHash ?? string.Empty,
            DataHash: header?.DataHash ?? string.Empty,
            ValidatorsHash: header?.ValidatorsHash ?? string.Empty,
            NextValidatorsHash: header?.NextValidatorsHash ?? string.Empty,
            ConsensusHash: header?.ConsensusHash ?? string.Empty,
            AppHash: header?.AppHash ?? string.Empty,
            LastResultsHash: header?.LastResultsHash ?? string.Empty,
            EvidenceHash: header?.EvidenceHash ?? string.Empty,
            ProposerAddress: header?.ProposerAddress ?? string.Empty);

        var events = (value.Events ?? [])
            .Select(e => new CometBftEvent(
                e.Type,
                (e.Attributes ?? [])
                    .Select(a => new AbciEventEntry(a.Key, a.Value, a.Index))
                    .ToList()
                    .AsReadOnly()))
            .ToList()
            .AsReadOnly();

        return new NewBlockEventsData(blockHeader, height, events);
    }

    internal static CompleteProposalData? ParseCompleteProposal(WsCompleteProposalData data)
    {
        var value = data.Value;
        if (value is null)
        {
            return null;
        }

        var height = long.TryParse(value.Height, out var h) ? h : 0;
        return new CompleteProposalData(height, value.Round, value.BlockId);
    }

    internal static NewEvidenceData? ParseNewEvidence(WsNewEvidenceData data)
    {
        var value = data.Value;
        if (value is null)
        {
            return null;
        }

        var height = long.TryParse(value.Height, out var h) ? h : 0;
        return new NewEvidenceData(height, value.EvidenceType, value.Validator);
    }
}
