using Grpc.Core;
using Grpc.Net.Client;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc.Internal;

namespace CometBFT.Client.Grpc;

/// <summary>
/// gRPC-based implementation of <see cref="ICometBftGrpcClient"/> targeting CometBFT v0.38
/// and legacy Tendermint Core nodes.
/// </summary>
/// <remarks>
/// When <see cref="CometBftGrpcOptions.Protocol"/> is <see cref="GrpcProtocol.Auto"/>,
/// the client probes the node on the first call and selects the appropriate protocol
/// automatically. Use <see cref="DetectedProtocol"/> after the first call to inspect
/// the resolved protocol.
/// </remarks>
public sealed class CometBftGrpcClient : ICometBftGrpcClient
{
    private readonly GrpcChannel _channel;
    private readonly Lazy<Task<IBroadcastApiClient>> _apiClientTask;
    private bool _disposed;
    private GrpcProtocol? _detectedProtocol;

    /// <summary>
    /// Gets the gRPC protocol that was resolved for this client.
    /// <c>null</c> until the first call has been made when <see cref="CometBftGrpcOptions.Protocol"/>
    /// is <see cref="GrpcProtocol.Auto"/>.
    /// </summary>
    public GrpcProtocol? DetectedProtocol => _detectedProtocol;

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcClient"/> with default channel management.
    /// </summary>
    /// <param name="options">The gRPC client configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public CometBftGrpcClient(CometBftGrpcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _channel = GrpcChannel.ForAddress(NormalizeGrpcAddress(options.BaseUrl));
        var protocol = options.Protocol;
        if (protocol != GrpcProtocol.Auto)
        {
            _detectedProtocol = protocol;
        }

        _apiClientTask = new Lazy<Task<IBroadcastApiClient>>(
            () => BroadcastApiClientFactory.CreateAsync(_channel, protocol, CancellationToken.None)
                  .ContinueWith(t =>
                  {
                      if (t.IsCompletedSuccessfully)
                      {
                          _detectedProtocol ??= t.Result is LegacyBroadcastApiClient
                              ? GrpcProtocol.TendermintLegacy
                              : GrpcProtocol.CometBft;
                      }

                      return t.Result;
                  }, TaskScheduler.Default));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcClient"/> with an injected API client.
    /// Used primarily for testing.
    /// </summary>
    /// <param name="channel">The gRPC channel.</param>
    /// <param name="apiClient">The broadcast API client stub.</param>
    internal CometBftGrpcClient(GrpcChannel channel, IBroadcastApiClient apiClient)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClientTask = new Lazy<Task<IBroadcastApiClient>>(Task.FromResult(apiClient));
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftGrpcClient"/> with an injected factory.
    /// Used for testing the auto-detection flow.
    /// </summary>
    /// <param name="channel">The gRPC channel.</param>
    /// <param name="apiClientFactory">Async factory that resolves the API client.</param>
    internal CometBftGrpcClient(GrpcChannel channel, Func<Task<IBroadcastApiClient>> apiClientFactory)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        ArgumentNullException.ThrowIfNull(apiClientFactory);
        _apiClientTask = new Lazy<Task<IBroadcastApiClient>>(apiClientFactory);
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var client = await _apiClientTask.Value.ConfigureAwait(false);
            return await client.PingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("gRPC ping failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<BroadcastTxResult> BroadcastTxAsync(byte[] txBytes, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(txBytes);

        try
        {
            var client = await _apiClientTask.Value.ConfigureAwait(false);
            var (code, data, log, gasWanted, gasUsed, codespace, hash) =
                await client.BroadcastTxAsync(txBytes, cancellationToken).ConfigureAwait(false);
            return new BroadcastTxResult(code, data, log, codespace, hash, gasWanted, gasUsed);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException(
                $"gRPC BroadcastTx failed: {ex.Status.Detail}",
                (int)ex.StatusCode,
                ex);
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("gRPC BroadcastTx failed.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _channel.ShutdownAsync().ConfigureAwait(false);
        _channel.Dispose();
    }

    private static string NormalizeGrpcAddress(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return baseUrl.Contains("://", StringComparison.Ordinal) ? baseUrl : $"https://{baseUrl}";
    }
}
