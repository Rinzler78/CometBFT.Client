using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc;
using CometBFT.Client.Grpc.Proto.CosmosBase.Tendermint.V1beta1;
using Xunit;
using ProtoBlock = CometBFT.Client.Grpc.Proto.CosmosBase.Tendermint.V1beta1.Block;

namespace CometBFT.Client.Grpc.Tests;

/// <summary>
/// Tests for <see cref="CometBftSdkGrpcClient"/> mapping logic using a <see cref="FakeCallInvoker"/>
/// that returns pre-configured proto responses without a live gRPC server.
/// </summary>
public sealed class CometBftSdkGrpcClientMappingTests
{
    private static CometBftSdkGrpcOptions DefaultOptions() => new()
    {
        BaseUrl = "https://localhost:9090",
        Timeout = TimeSpan.FromSeconds(10),
        ValidatorPageSize = 100,
    };

    private static CometBftSdkGrpcClient CreateClient(FakeCallInvoker invoker) =>
        new(new Service.ServiceClient(invoker), DefaultOptions());

    // ── FakeCallInvoker ──────────────────────────────────────────────────────

    private sealed class FakeCallInvoker : CallInvoker
    {
        private readonly Dictionary<string, object> _responses = new(StringComparer.Ordinal);

        public FakeCallInvoker With<TResponse>(string methodName, TResponse response)
        {
            _responses[methodName] = response!;
            return this;
        }

        public FakeCallInvoker WithRpcException(string methodName, StatusCode code)
        {
            _responses[methodName] = new RpcException(new Status(code, "fake"));
            return this;
        }

        public FakeCallInvoker WithException(string methodName, Exception exception)
        {
            _responses[methodName] = exception;
            return this;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            if (_responses.TryGetValue(method.Name, out var obj))
            {
                if (obj is Exception ex) throw ex;
                return new AsyncUnaryCall<TResponse>(
                    Task.FromResult((TResponse)obj),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });
            }

            throw new RpcException(new Status(StatusCode.Unimplemented, "not configured: " + method.Name));
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
            throw new NotImplementedException();

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotImplementedException();

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
            throw new NotImplementedException();

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotImplementedException();
    }

    // ── GetStatusAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ReturnsNodeInfoAndSyncInfo()
    {
        var invoker = new FakeCallInvoker()
            .With("GetNodeInfo", new GetNodeInfoResponse
            {
                DefaultNodeInfo = new DefaultNodeInfo
                {
                    DefaultNodeId = "nodeid123",
                    Network = "cosmoshub-4",
                    Moniker = "mynode",
                    Version = "0.38.9",
                    ListenAddr = "tcp://0.0.0.0:26656",
                }
            })
            .With("GetSyncing", new GetSyncingResponse { Syncing = false });

        await using var client = CreateClient(invoker);
        var (nodeInfo, syncInfo) = await client.GetStatusAsync();

        Assert.Equal("nodeid123", nodeInfo.Id);
        Assert.Equal("cosmoshub-4", nodeInfo.Network);
        Assert.Equal("mynode", nodeInfo.Moniker);
        Assert.False(syncInfo.CatchingUp);
    }

    [Fact]
    public async Task GetStatusAsync_RpcException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithRpcException("GetNodeInfo", StatusCode.Unavailable)
            .With("GetSyncing", new GetSyncingResponse { Syncing = false });

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetStatusAsync());
    }

    // ── GetSyncingAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSyncingAsync_ReturnsTrue_WhenSyncing()
    {
        var invoker = new FakeCallInvoker()
            .With("GetSyncing", new GetSyncingResponse { Syncing = true });

        await using var client = CreateClient(invoker);
        var result = await client.GetSyncingAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task GetSyncingAsync_ReturnsFalse_WhenSynced()
    {
        var invoker = new FakeCallInvoker()
            .With("GetSyncing", new GetSyncingResponse { Syncing = false });

        await using var client = CreateClient(invoker);
        var result = await client.GetSyncingAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task GetSyncingAsync_RpcException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithRpcException("GetSyncing", StatusCode.Unavailable);

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetSyncingAsync());
    }

    // ── GetLatestBlockAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestBlockAsync_ReturnsBlock()
    {
        var invoker = new FakeCallInvoker()
            .With("GetLatestBlock", new GetLatestBlockResponse
            {
                Block = new ProtoBlock
                {
                    Header = new Header
                    {
                        Height = 42,
                        Time = Timestamp.FromDateTimeOffset(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                        ProposerAddress = ByteString.CopyFrom([0xAA, 0xBB]),
                    },
                    Data = new Data()
                }
            });

        await using var client = CreateClient(invoker);
        var block = await client.GetLatestBlockAsync();

        Assert.Equal(42, block.Height);
        Assert.Equal("AABB", block.Proposer);
    }

    [Fact]
    public async Task GetLatestBlockAsync_RpcException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithRpcException("GetLatestBlock", StatusCode.Unavailable);

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetLatestBlockAsync());
    }

    // ── GetBlockByHeightAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetBlockByHeightAsync_ReturnsBlock_AtHeight()
    {
        var invoker = new FakeCallInvoker()
            .With("GetBlockByHeight", new GetBlockByHeightResponse
            {
                Block = new ProtoBlock
                {
                    Header = new Header { Height = 100 },
                    Data = new Data()
                }
            });

        await using var client = CreateClient(invoker);
        var block = await client.GetBlockByHeightAsync(100);

        Assert.Equal(100, block.Height);
    }

    // ── GetLatestValidatorsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetLatestValidatorsAsync_ReturnsValidators()
    {
        var response = new GetLatestValidatorSetResponse();
        response.Validators.Add(new Validator { Address = "val1", VotingPower = 500 });

        var invoker = new FakeCallInvoker()
            .With("GetLatestValidatorSet", response);

        await using var client = CreateClient(invoker);
        var validators = await client.GetLatestValidatorsAsync();

        Assert.Single(validators);
        Assert.Equal("val1", validators[0].Address);
        Assert.Equal(500, validators[0].VotingPower);
    }

    [Fact]
    public async Task GetLatestValidatorsAsync_RpcException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithRpcException("GetLatestValidatorSet", StatusCode.Unavailable);

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetLatestValidatorsAsync());
    }

    // ── GetBlockByHeightAsync RpcException ──────────────────────────────────

    [Fact]
    public async Task GetBlockByHeightAsync_RpcException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithRpcException("GetBlockByHeight", StatusCode.Unavailable);

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetBlockByHeightAsync(100));
    }

    // ── GetValidatorSetByHeightAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetValidatorSetByHeightAsync_ReturnsValidators()
    {
        var response = new GetValidatorSetByHeightResponse();
        response.Validators.Add(new Validator { Address = "val2", VotingPower = 1000 });

        var invoker = new FakeCallInvoker()
            .With("GetValidatorSetByHeight", response);

        await using var client = CreateClient(invoker);
        var validators = await client.GetValidatorSetByHeightAsync(50);

        Assert.Single(validators);
        Assert.Equal("val2", validators[0].Address);
    }

    [Fact]
    public async Task GetValidatorSetByHeightAsync_RpcException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithRpcException("GetValidatorSetByHeight", StatusCode.Unavailable);

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetValidatorSetByHeightAsync(50));
    }

    // ── ABCIQueryAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ABCIQueryAsync_ReturnsResponse_WithoutProof()
    {
        var invoker = new FakeCallInvoker()
            .With("ABCIQuery", new ABCIQueryResponse
            {
                Code = 0,
                Log = "ok",
                Value = ByteString.CopyFrom([0xDE, 0xAD]),
                Height = 55,
            });

        await using var client = CreateClient(invoker);
        var response = await client.ABCIQueryAsync("/store/acc/key", [0x01]);

        Assert.Equal(0u, response.Code);
        Assert.Equal("ok", response.Log);
        Assert.Equal(55L, response.Height);
        Assert.Null(response.ProofOps);
    }

    [Fact]
    public async Task ABCIQueryAsync_WithProofOps_MapsProofOps()
    {
        var proofOps = new global::CometBFT.Client.Grpc.Proto.CosmosBase.Tendermint.V1beta1.ProofOps();
        proofOps.Ops.Add(new ProofOp
        {
            Type = "ics23:iavl",
            Key = ByteString.CopyFrom([0x01]),
            Data = ByteString.CopyFrom([0x02, 0x03])
        });

        var invoker = new FakeCallInvoker()
            .With("ABCIQuery", new ABCIQueryResponse
            {
                Code = 0,
                Height = 10,
                ProofOps = proofOps,
            });

        await using var client = CreateClient(invoker);
        var response = await client.ABCIQueryAsync("/store/acc/key", [0x01], prove: true);

        Assert.NotNull(response.ProofOps);
        Assert.Single(response.ProofOps.Ops);
        Assert.Equal("ics23:iavl", response.ProofOps.Ops[0].Type);
    }

    [Fact]
    public async Task ABCIQueryAsync_RpcException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithRpcException("ABCIQuery", StatusCode.Unavailable);

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.ABCIQueryAsync("/", []));
    }

    // ── MapBlock with transactions ───────────────────────────────────────────

    [Fact]
    public async Task GetLatestBlockAsync_BlockWithTxs_MapsTxs()
    {
        var data = new Data();
        data.Txs.Add(Google.Protobuf.ByteString.CopyFrom([0xDE, 0xAD]));

        var invoker = new FakeCallInvoker()
            .With("GetLatestBlock", new GetLatestBlockResponse
            {
                Block = new ProtoBlock
                {
                    Header = new Header { Height = 5 },
                    Data = data
                }
            });

        await using var client = CreateClient(invoker);
        var block = await client.GetLatestBlockAsync();

        Assert.Equal(5, block.Height);
        Assert.Single(block.Txs);
    }

    // ── catch (Exception ex) paths — generic non-RPC exceptions ─────────────

    [Fact]
    public async Task GetStatusAsync_GenericException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetNodeInfo", new InvalidOperationException("boom"))
            .With("GetSyncing", new GetSyncingResponse { Syncing = false });

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetStatusAsync());
    }

    [Fact]
    public async Task GetLatestBlockAsync_GenericException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetLatestBlock", new InvalidOperationException("boom"));

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetLatestBlockAsync());
    }

    [Fact]
    public async Task GetLatestValidatorsAsync_GenericException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetLatestValidatorSet", new InvalidOperationException("boom"));

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetLatestValidatorsAsync());
    }

    [Fact]
    public async Task GetBlockByHeightAsync_GenericException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetBlockByHeight", new InvalidOperationException("boom"));

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetBlockByHeightAsync(1));
    }

    [Fact]
    public async Task GetValidatorSetByHeightAsync_GenericException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetValidatorSetByHeight", new InvalidOperationException("boom"));

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetValidatorSetByHeightAsync(1));
    }

    [Fact]
    public async Task ABCIQueryAsync_GenericException_ThrowsCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithException("ABCIQuery", new InvalidOperationException("boom"));

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.ABCIQueryAsync("/", []));
    }

    // ── catch (OperationCanceledException) paths ─────────────────────────────

    [Fact]
    public async Task GetStatusAsync_OperationCanceled_Rethrows()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetNodeInfo", new OperationCanceledException())
            .With("GetSyncing", new GetSyncingResponse { Syncing = false });

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetStatusAsync());
    }

    [Fact]
    public async Task GetLatestBlockAsync_OperationCanceled_Rethrows()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetLatestBlock", new OperationCanceledException());

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetLatestBlockAsync());
    }

    [Fact]
    public async Task GetLatestValidatorsAsync_OperationCanceled_Rethrows()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetLatestValidatorSet", new OperationCanceledException());

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetLatestValidatorsAsync());
    }

    // ── Generic exception wrapping ───────────────────────────────────────────

    [Fact]
    public async Task GetSyncingAsync_GenericException_WrappedInCometBftGrpcException()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetSyncing", new InvalidOperationException("boom"));

        await using var client = CreateClient(invoker);
        var ex = await Assert.ThrowsAsync<CometBftGrpcException>(() => client.GetSyncingAsync());
        Assert.NotNull(ex);
    }

    [Fact]
    public async Task GetSyncingAsync_OperationCanceled_Rethrows()
    {
        var invoker = new FakeCallInvoker()
            .WithException("GetSyncing", new OperationCanceledException());

        await using var client = CreateClient(invoker);
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetSyncingAsync());
    }

    // ── Constructor guard ─────────────────────────────────────────────────────

    [Fact]
    public void ServiceClientConstructor_NullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CometBftSdkGrpcClient((Service.ServiceClient)null!, DefaultOptions()));
    }

    [Fact]
    public void ServiceClientConstructor_NullOptions_ThrowsArgumentNullException()
    {
        var invoker = new FakeCallInvoker();
        Assert.Throws<ArgumentNullException>(() =>
            new CometBftSdkGrpcClient(new Service.ServiceClient(invoker), null!));
    }
}
