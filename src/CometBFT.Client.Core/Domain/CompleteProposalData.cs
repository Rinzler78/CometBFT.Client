namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Payload for the <c>CompleteProposal</c> consensus WebSocket event
/// (tm.event='CompleteProposal').
/// </summary>
/// <param name="Height">The block height at which the proposal was completed.</param>
/// <param name="Round">The consensus round.</param>
/// <param name="BlockId">The block identifier hash.</param>
public sealed record CompleteProposalData(long Height, int Round, string BlockId);
