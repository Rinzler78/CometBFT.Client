namespace CometBFT.Client.Core.Options;

/// <summary>
/// Aggregated configuration options for the full CometBFT client stack
/// (REST, gRPC, Cosmos SDK gRPC, and WebSocket).
/// All URL defaults target the same public Cosmos Hub mainnet operator (Lava Network).
/// </summary>
public sealed class CometBftClientOptions
{
    /// <summary>
    /// Gets or sets the CometBFT JSON-RPC / REST base URL.
    /// Defaults to the Lava Network public Cosmos Hub relay.
    /// </summary>
    public string RestBaseUrl { get; set; } = "https://cosmoshub.tendermintrpc.lava.build:443";

    /// <summary>
    /// Gets or sets the gRPC endpoint (serves both CometBFT raw gRPC and Cosmos SDK gRPC).
    /// Defaults to the Lava Network public Cosmos Hub relay.
    /// </summary>
    public string GrpcBaseUrl { get; set; } = "https://cosmoshub.grpc.lava.build:443";

    /// <summary>
    /// Gets or sets the CometBFT WebSocket subscription endpoint.
    /// Defaults to the Lava Network public Cosmos Hub relay.
    /// </summary>
    public string WebSocketBaseUrl { get; set; } = "wss://cosmoshub.tendermintrpc.lava.build:443/websocket";

    /// <summary>
    /// Gets or sets the HTTP/gRPC request timeout. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures. Defaults to 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retry attempts for exponential back-off. Defaults to 1 second.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
