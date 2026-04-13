using Grpc.Core;
using Grpc.Net.Client;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc.Internal;
using Microsoft.Extensions.Options;

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
    private readonly Func<CancellationToken, Task<IBroadcastApiClient>>? _clientFactory;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IBroadcastApiClient? _apiClient;
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
    public CometBftGrpcClient(IOptions<CometBftGrpcOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var resolvedOptions = options.Value;
        _channel = GrpcChannel.ForAddress(NormalizeGrpcAddress(resolvedOptions.BaseUrl));
        var protocol = resolvedOptions.Protocol;
        if (protocol != GrpcProtocol.Auto)
        {
            _detectedProtocol = protocol;
        }

        _clientFactory = ct => CreateAndDetectAsync(_channel, protocol, ct);
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
        _apiClient = apiClient;
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
        _clientFactory = _ => apiClientFactory();
    }

    /// <inheritdoc />
    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var client = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
            return await client.PingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
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
            var client = await GetOrCreateClientAsync(cancellationToken).ConfigureAwait(false);
            var (code, data, log, gasWanted, gasUsed, codespace, hash) =
                await client.BroadcastTxAsync(txBytes, cancellationToken).ConfigureAwait(false);
            return new BroadcastTxResult(code, data, log, codespace, hash, gasWanted, gasUsed);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        _initLock.Dispose();
        await _channel.ShutdownAsync().ConfigureAwait(false);
        _channel.Dispose();
    }

    private async Task<IBroadcastApiClient> GetOrCreateClientAsync(CancellationToken cancellationToken)
    {
        if (_apiClient is not null)
        {
            return _apiClient;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_apiClient is not null)
            {
                return _apiClient;
            }

            _apiClient = _clientFactory is not null
                ? await _clientFactory(cancellationToken).ConfigureAwait(false)
                : throw new InvalidOperationException("No API client or factory configured.");

            return _apiClient;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<IBroadcastApiClient> CreateAndDetectAsync(GrpcChannel channel, GrpcProtocol protocol, CancellationToken cancellationToken)
    {
        var client = await BroadcastApiClientFactory.CreateAsync(channel, protocol, cancellationToken).ConfigureAwait(false);
        _detectedProtocol ??= client is LegacyBroadcastApiClient
            ? GrpcProtocol.TendermintLegacy
            : GrpcProtocol.CometBft;
        return client;
    }

    private static string NormalizeGrpcAddress(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return baseUrl.Contains("://", StringComparison.Ordinal) ? baseUrl : $"https://{baseUrl}";
    }
}
