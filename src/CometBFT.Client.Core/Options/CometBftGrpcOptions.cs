namespace CometBFT.Client.Core.Options;

/// <summary>
/// Configuration options for the CometBFT gRPC client.
/// </summary>
public sealed class CometBftGrpcOptions
{
    /// <summary>
    /// Gets or sets the gRPC endpoint URL of the CometBFT node.
    /// Defaults to <c>http://localhost:9090</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:9090";

    /// <summary>
    /// Gets or sets the deadline timeout for individual gRPC calls. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the gRPC wire protocol to use.
    /// <see cref="GrpcProtocol.Auto"/> (default) probes the node on the first call
    /// and selects <see cref="GrpcProtocol.CometBft"/> or <see cref="GrpcProtocol.TendermintLegacy"/>
    /// automatically.
    /// </summary>
    public GrpcProtocol Protocol { get; set; } = GrpcProtocol.Auto;
}
