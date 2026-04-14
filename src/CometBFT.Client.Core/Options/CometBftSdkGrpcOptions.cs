namespace CometBFT.Client.Core.Options;

/// <summary>
/// Configuration options for the Cosmos SDK gRPC client
/// (<c>cosmos.base.tendermint.v1beta1.Service</c>).
/// </summary>
public sealed class CometBftSdkGrpcOptions
{
    /// <summary>
    /// Gets or sets the gRPC endpoint URL.
    /// Defaults to <c>http://localhost:9090</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:9090";

    /// <summary>
    /// Gets or sets the deadline timeout for individual gRPC calls. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of validators to fetch per page. Defaults to 100.
    /// </summary>
    public uint ValidatorPageSize { get; set; } = 100;

    /// <summary>
    /// Validates the options and throws <see cref="InvalidOperationException"/> if any value is invalid.
    /// </summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(BaseUrl))
            errors.Add($"{nameof(BaseUrl)} must not be empty.");
        else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            errors.Add($"{nameof(BaseUrl)} '{BaseUrl}' is not a valid absolute URI.");

        if (Timeout <= TimeSpan.Zero)
            errors.Add($"{nameof(Timeout)} must be positive.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"{nameof(CometBftSdkGrpcOptions)} validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }
}
