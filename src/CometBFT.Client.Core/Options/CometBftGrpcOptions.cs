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

    /// <summary>
    /// Validates the current options and throws <see cref="InvalidOperationException"/> if any value is invalid.
    /// Called automatically during DI registration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when one or more option values are invalid.</exception>
    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(BaseUrl))
            errors.Add($"{nameof(BaseUrl)} must not be empty.");
        else if (!IsValidGrpcAddress(BaseUrl))
            errors.Add($"{nameof(BaseUrl)} '{BaseUrl}' is not a valid gRPC address. " +
                       "Expected an absolute URI (e.g. 'https://host:9090') or a bare host[:port] (e.g. 'host:9090').");

        if (Timeout <= TimeSpan.Zero)
            errors.Add($"{nameof(Timeout)} must be positive.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"{nameof(CometBftGrpcOptions)} validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="url"/> is either a valid absolute URI
    /// (e.g. <c>https://host:9090</c>) or a bare host[:port] accepted by gRPC channels
    /// (e.g. <c>host:9090</c> or <c>host</c>).
    /// </summary>
    private static bool IsValidGrpcAddress(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return true;

        // Bare host or host:port — no scheme, no path segment containing '/'
        if (url.Contains('/', StringComparison.Ordinal))
            return false;

        return Uri.TryCreate($"https://{url}", UriKind.Absolute, out _);
    }
}
