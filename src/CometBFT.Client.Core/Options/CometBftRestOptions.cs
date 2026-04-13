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
        else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            errors.Add($"{nameof(BaseUrl)} '{BaseUrl}' is not a valid absolute URI.");

        if (Timeout <= TimeSpan.Zero)
            errors.Add($"{nameof(Timeout)} must be positive.");

        if (MaxRetryAttempts < 0)
            errors.Add($"{nameof(MaxRetryAttempts)} must be >= 0.");

        if (RetryDelay < TimeSpan.Zero)
            errors.Add($"{nameof(RetryDelay)} must be non-negative.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"{nameof(CometBftRestOptions)} validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }
}
