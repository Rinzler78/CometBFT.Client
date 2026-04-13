namespace CometBFT.Client.Core.Exceptions;

/// <summary>
/// Base exception for all CometBFT/Tendermint client errors.
/// </summary>
public class CometBftClientException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="CometBftClientException"/>.
    /// </summary>
    public CometBftClientException()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftClientException"/> with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CometBftClientException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftClientException"/> with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public CometBftClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
