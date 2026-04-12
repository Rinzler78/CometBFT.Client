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
}
