namespace CometBFT.Client.Core.Options;

/// <summary>
/// Configuration options for the CometBFT WebSocket subscription client.
/// </summary>
public sealed class CometBftWebSocketOptions
{
    /// <summary>
    /// Gets or sets the WebSocket endpoint URL of the CometBFT node.
    /// Defaults to <c>ws://localhost:26657/websocket</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "ws://localhost:26657/websocket";

    /// <summary>
    /// Gets or sets the reconnect timeout after a connection loss. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan ReconnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the error reconnect timeout. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan ErrorReconnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

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

        if (ReconnectTimeout <= TimeSpan.Zero)
            errors.Add($"{nameof(ReconnectTimeout)} must be positive.");

        if (ErrorReconnectTimeout <= TimeSpan.Zero)
            errors.Add($"{nameof(ErrorReconnectTimeout)} must be positive.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"{nameof(CometBftWebSocketOptions)} validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }
}
