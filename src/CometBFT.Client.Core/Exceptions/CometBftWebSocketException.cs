namespace CometBFT.Client.Core.Exceptions;

/// <summary>
/// Exception thrown when a CometBFT WebSocket operation fails.
/// </summary>
public sealed class CometBftWebSocketException : CometBftClientException
{
    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketException"/>.
    /// </summary>
    public CometBftWebSocketException()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketException"/> with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CometBftWebSocketException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftWebSocketException"/> with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CometBftWebSocketException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
