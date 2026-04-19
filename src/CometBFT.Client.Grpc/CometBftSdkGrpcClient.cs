using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using Grpc.Net.Client;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc.Proto.CosmosBase.Tendermint.V1beta1;
using Microsoft.Extensions.Options;
// Alias proto-generated names that clash with Core.Domain names.
using DomainBlock = CometBFT.Client.Core.Domain.Block;
using DomainValidator = CometBFT.Client.Core.Domain.Validator;
using ProtoBlock = CometBFT.Client.Grpc.Proto.CosmosBase.Tendermint.V1beta1.Block;
using ProtoValidator = CometBFT.Client.Grpc.Proto.CosmosBase.Tendermint.V1beta1.Validator;

namespace CometBFT.Client.Grpc;

/// <summary>
/// gRPC implementation of <see cref="ICometBftSdkGrpcClient"/> using the Cosmos SDK
/// <c>cosmos.base.tendermint.v1beta1.Service</c>.
/// This service is available on all Cosmos-ecosystem nodes that expose port 9090 and is
/// the recommended way to query chain state from public infrastructure.
/// </summary>
// Covered by integration tests (CometBFT.Client.Integration.Tests, Category=Integration).
// Live gRPC connectivity is required, so unit-test mocking adds no safety signal here.
[ExcludeFromCodeCoverage]
public sealed class CometBftSdkGrpcClient : ICometBftSdkGrpcClient
{
    private readonly GrpcChannel _channel;
    private readonly Service.ServiceClient _client;
    private readonly CometBftSdkGrpcOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftSdkGrpcClient"/>.
    /// </summary>
    /// <param name="options">The SDK gRPC client configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public CometBftSdkGrpcClient(IOptions<CometBftSdkGrpcOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _channel = GrpcChannel.ForAddress(NormalizeAddress(_options.BaseUrl));
        _client = new Service.ServiceClient(_channel);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CometBftSdkGrpcClient"/> with an injected channel.
    /// Used primarily for testing.
    /// </summary>
    internal CometBftSdkGrpcClient(GrpcChannel channel, CometBftSdkGrpcOptions options)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = new Service.ServiceClient(channel);
    }

    /// <inheritdoc />
    public async Task<(NodeInfo NodeInfo, SyncInfo SyncInfo)> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            var callOptions = new CallOptions(cancellationToken: cancellationToken, deadline: deadline);

            var nodeInfoTask = _client.GetNodeInfoAsync(new GetNodeInfoRequest(), callOptions).ResponseAsync;
            var syncingTask = _client.GetSyncingAsync(new GetSyncingRequest(), callOptions).ResponseAsync;

            await Task.WhenAll(nodeInfoTask, syncingTask).ConfigureAwait(false);

            var nodeInfoResp = await nodeInfoTask.ConfigureAwait(false);
            var syncingResp = await syncingTask.ConfigureAwait(false);

            var dni = nodeInfoResp.DefaultNodeInfo;
            var nodeInfo = new NodeInfo(
                Id: dni?.DefaultNodeId ?? string.Empty,
                ListenAddr: dni?.ListenAddr ?? string.Empty,
                Network: dni?.Network ?? string.Empty,
                Version: dni?.Version ?? string.Empty,
                Channels: dni?.Channels != null ? Convert.ToHexString(dni.Channels.ToByteArray()) : string.Empty,
                Moniker: dni?.Moniker ?? string.Empty,
                ProtocolVersion: dni?.ProtocolVersion is { } pv
                    ? new ProtocolVersion(pv.P2P, pv.Block, pv.App)
                    : new ProtocolVersion(0, 0, 0));

            // GetSyncing returns only the syncing bool; populate SyncInfo with what we have.
            // Height/hashes are not available from this call — use GetLatestBlock for those.
            var syncInfo = new SyncInfo(
                LatestBlockHash: string.Empty,
                LatestAppHash: string.Empty,
                LatestBlockHeight: 0,
                LatestBlockTime: DateTimeOffset.MinValue,
                EarliestBlockHash: string.Empty,
                EarliestAppHash: string.Empty,
                EarliestBlockHeight: 0,
                EarliestBlockTime: DateTimeOffset.MinValue,
                CatchingUp: syncingResp.Syncing);

            return (nodeInfo, syncInfo);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException($"SDK gRPC GetStatus failed: {ex.Status.Detail}", (int)ex.StatusCode, ex);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("SDK gRPC GetStatus failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<DomainBlock> GetLatestBlockAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            var resp = await _client
                .GetLatestBlockAsync(new GetLatestBlockRequest(),
                    new CallOptions(cancellationToken: cancellationToken, deadline: deadline))
                .ResponseAsync
                .ConfigureAwait(false);

            return MapBlock(resp.Block);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException($"SDK gRPC GetLatestBlock failed: {ex.Status.Detail}", (int)ex.StatusCode, ex);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("SDK gRPC GetLatestBlock failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainValidator>> GetLatestValidatorsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            var request = new GetLatestValidatorSetRequest
            {
                Pagination = new PageRequest { Limit = _options.ValidatorPageSize }
            };

            var resp = await _client
                .GetLatestValidatorSetAsync(request,
                    new CallOptions(cancellationToken: cancellationToken, deadline: deadline))
                .ResponseAsync
                .ConfigureAwait(false);

            return MapValidators(resp.Validators);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException($"SDK gRPC GetLatestValidatorSet failed: {ex.Status.Detail}", (int)ex.StatusCode, ex);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("SDK gRPC GetLatestValidatorSet failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> GetSyncingAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            var resp = await _client
                .GetSyncingAsync(new GetSyncingRequest(),
                    new CallOptions(cancellationToken: cancellationToken, deadline: deadline))
                .ResponseAsync
                .ConfigureAwait(false);

            return resp.Syncing;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException($"SDK gRPC GetSyncing failed: {ex.Status.Detail}", (int)ex.StatusCode, ex);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("SDK gRPC GetSyncing failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<DomainBlock> GetBlockByHeightAsync(long height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            var resp = await _client
                .GetBlockByHeightAsync(new GetBlockByHeightRequest { Height = height },
                    new CallOptions(cancellationToken: cancellationToken, deadline: deadline))
                .ResponseAsync
                .ConfigureAwait(false);

            return MapBlock(resp.Block);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException($"SDK gRPC GetBlockByHeight failed: {ex.Status.Detail}", (int)ex.StatusCode, ex);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("SDK gRPC GetBlockByHeight failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainValidator>> GetValidatorSetByHeightAsync(long height, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            var request = new GetValidatorSetByHeightRequest
            {
                Height = height,
                Pagination = new PageRequest { Limit = _options.ValidatorPageSize }
            };

            var resp = await _client
                .GetValidatorSetByHeightAsync(request,
                    new CallOptions(cancellationToken: cancellationToken, deadline: deadline))
                .ResponseAsync
                .ConfigureAwait(false);

            return MapValidators(resp.Validators);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException($"SDK gRPC GetValidatorSetByHeight failed: {ex.Status.Detail}", (int)ex.StatusCode, ex);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("SDK gRPC GetValidatorSetByHeight failed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<AbciQueryResponse> ABCIQueryAsync(string path, byte[] data, long height = 0, bool prove = false, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            var deadline = DateTime.UtcNow.Add(_options.Timeout);
            var request = new ABCIQueryRequest
            {
                Path = path ?? string.Empty,
                Data = Google.Protobuf.ByteString.CopyFrom(data),
                Height = height,
                Prove = prove
            };

            var resp = await _client
                .ABCIQueryAsync(request,
                    new CallOptions(cancellationToken: cancellationToken, deadline: deadline))
                .ResponseAsync
                .ConfigureAwait(false);

            AbciProofOps? proofOps = null;
            if (resp.ProofOps?.Ops?.Count > 0)
            {
                proofOps = new AbciProofOps(
                    resp.ProofOps.Ops
                        .Select(op => new AbciProofOp(op.Type, op.Key.ToByteArray(), op.Data.ToByteArray()))
                        .ToList()
                        .AsReadOnly());
            }

            return new AbciQueryResponse(
                Code: resp.Code,
                Log: resp.Log ?? string.Empty,
                Info: resp.Info ?? string.Empty,
                Index: resp.Index,
                Key: (IReadOnlyList<byte>)resp.Key.ToByteArray(),
                Value: (IReadOnlyList<byte>)resp.Value.ToByteArray(),
                ProofOps: proofOps,
                Height: resp.Height,
                Codespace: resp.Codespace ?? string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RpcException ex)
        {
            throw new CometBftGrpcException($"SDK gRPC ABCIQuery failed: {ex.Status.Detail}", (int)ex.StatusCode, ex);
        }
        catch (CometBftGrpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CometBftGrpcException("SDK gRPC ABCIQuery failed.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _channel.ShutdownAsync().ConfigureAwait(false);
        _channel.Dispose();
    }

    private static DomainBlock MapBlock(ProtoBlock? block)
    {
        var header = block?.Header;
        var data = block?.Data;

        var time = header?.Time != null
            ? DateTimeOffset.FromUnixTimeSeconds(header.Time.Seconds)
                  .AddTicks(header.Time.Nanos / 100)
            : DateTimeOffset.MinValue;

        var proposer = header?.ProposerAddress?.Length > 0
            ? Convert.ToHexString(header.ProposerAddress.ToByteArray())
            : string.Empty;

        var txs = data?.Txs
            .Select(t => Convert.ToBase64String(t.ToByteArray()))
            .ToList()
            ?? [];

        return new DomainBlock(
            Height: header?.Height ?? 0,
            Hash: string.Empty,   // not provided by this gRPC call
            Time: time,
            Proposer: proposer,
            Txs: txs);
    }

    private static ReadOnlyCollection<DomainValidator> MapValidators(
        IEnumerable<ProtoValidator> validators) =>
        validators
            .Select(v => new DomainValidator(
                Address: v.Address,
                PubKey: v.PubKey?.Value != null ? Convert.ToBase64String(v.PubKey.Value.ToByteArray()) : string.Empty,
                VotingPower: v.VotingPower,
                ProposerPriority: v.ProposerPriority))
            .ToList()
            .AsReadOnly();

    private static string NormalizeAddress(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return baseUrl.Contains("://", StringComparison.Ordinal) ? baseUrl : $"https://{baseUrl}";
    }
}
