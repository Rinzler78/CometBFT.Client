namespace CometBFT.Client.Core.Options;

/// <summary>
/// Configuration options for the CometBFT REST/JSON-RPC 2.0 client.
/// </summary>
public sealed class CometBftRestOptions
{
    /// <summary>
    /// Gets or sets the base URL of the CometBFT RPC endpoint.
    /// Defaults to <c>http://localhost:26657</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:26657";

    /// <summary>
    /// Gets or sets the HTTP request timeout. Defaults to 30 seconds.
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
