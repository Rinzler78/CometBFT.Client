namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents the response from an ABCI query against the application layer.
/// </summary>
/// <param name="Code">Response code. 0 = success; non-zero = error.</param>
/// <param name="Log">Human-readable debug log. May be empty on success.</param>
/// <param name="Info">Supplemental info string returned by the application.</param>
/// <param name="Index">Merkle proof index for the queried key.</param>
/// <param name="Key">The key that was queried (may be empty).</param>
/// <param name="Value">The raw value bytes returned by the application.</param>
/// <param name="ProofOps">Merkle proof operations (non-null only when prove=true was requested).</param>
/// <param name="Height">Block height at which the query was evaluated.</param>
/// <param name="Codespace">Registered error codespace (empty when Code=0).</param>
public sealed record AbciQueryResponse(
    uint Code,
    string Log,
    string Info,
    long Index,
    IReadOnlyList<byte> Key,
    IReadOnlyList<byte> Value,
    AbciProofOps? ProofOps,
    long Height,
    string Codespace);

/// <summary>
/// A sequence of Merkle proof operations returned by an ABCI query with prove=true.
/// </summary>
/// <param name="Ops">Individual proof operation steps.</param>
public sealed record AbciProofOps(IReadOnlyList<AbciProofOp> Ops);

/// <summary>
/// A single Merkle proof operation step.
/// </summary>
/// <param name="Type">Proof type identifier (e.g. "ics23:iavl").</param>
/// <param name="Key">Key covered by this proof step.</param>
/// <param name="Data">Serialized proof data.</param>
public sealed record AbciProofOp(string Type, IReadOnlyList<byte> Key, IReadOnlyList<byte> Data);
