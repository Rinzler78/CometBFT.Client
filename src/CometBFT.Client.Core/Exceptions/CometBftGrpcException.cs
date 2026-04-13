namespace CometBFT.Client.Core.Exceptions;

/// <summary>
/// Exception thrown when a CometBFT gRPC operation fails.
/// </summary>
public sealed class CometBftGrpcException : CometBftClientException
{
    /// <summary>
    /// Gets the numeric gRPC status code associated with this error, if available.
    /// See <see href="https://grpc.github.io/grpc/core/md_doc_statuscodes.html"/> for values.
    /// </summary>
    public int? GrpcStatusCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcException"/>.
    /// </summary>
    public CometBftGrpcException()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcException"/> with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CometBftGrpcException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcException"/> with gRPC status context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="grpcStatusCode">The numeric gRPC status code.</param>
    public CometBftGrpcException(string message, int grpcStatusCode)
        : base(message)
    {
        GrpcStatusCode = grpcStatusCode;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcException"/> with status code and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="grpcStatusCode">The numeric gRPC status code.</param>
    /// <param name="innerException">The inner exception.</param>
    public CometBftGrpcException(string message, int grpcStatusCode, Exception innerException)
        : base(message, innerException)
    {
        GrpcStatusCode = grpcStatusCode;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcException"/> with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CometBftGrpcException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
