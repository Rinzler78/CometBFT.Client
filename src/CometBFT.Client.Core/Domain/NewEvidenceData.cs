namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Payload for the <c>NewEvidence</c> WebSocket event (tm.event='NewEvidence').
/// </summary>
/// <param name="Height">The block height at which the evidence was submitted.</param>
/// <param name="EvidenceType">The type identifier of the evidence.</param>
/// <param name="Validator">The validator address associated with the evidence.</param>
public sealed record NewEvidenceData(long Height, string EvidenceType, string Validator);
