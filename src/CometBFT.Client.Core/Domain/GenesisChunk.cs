namespace CometBFT.Client.Core.Domain;

/// <summary>
/// Represents a chunk returned by the <c>/genesis_chunked</c> RPC endpoint.
/// </summary>
/// <param name="Chunk">The current chunk index.</param>
/// <param name="Total">The total number of chunks.</param>
/// <param name="Data">The base64-encoded chunk payload.</param>
public sealed record GenesisChunk(
    int Chunk,
    int Total,
    string Data);
