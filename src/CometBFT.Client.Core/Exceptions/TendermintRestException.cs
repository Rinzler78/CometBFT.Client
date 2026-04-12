using System.Net;

namespace CometBFT.Client.Core.Exceptions;

/// <summary>
/// Exception thrown when a CometBFT REST/JSON-RPC request fails.
/// </summary>
public sealed class CometBftRestException : CometBftClientException
{
    /// <summary>
    /// Gets the HTTP status code returned by the server, if available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the JSON-RPC error code returned by the server, if available.
    /// </summary>
    public int? RpcErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftRestException"/>.
    /// </summary>
    public CometBftRestException()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftRestException"/> with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CometBftRestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftRestException"/> with HTTP context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    public CometBftRestException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftRestException"/> with RPC error context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="rpcErrorCode">The JSON-RPC error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public CometBftRestException(string message, int rpcErrorCode, Exception? innerException = null)
        : base(message, innerException!)
    {
        RpcErrorCode = rpcErrorCode;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftRestException"/> with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CometBftRestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
