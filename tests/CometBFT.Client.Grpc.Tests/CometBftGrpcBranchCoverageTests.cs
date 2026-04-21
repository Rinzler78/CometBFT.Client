using System.Reflection;
using System.Runtime.Serialization;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using NSubstitute;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc;
using CometBFT.Client.Grpc.Internal;
using CometBFT.Client.Grpc.Proto;
using Xunit;
using LegacyProto = Tendermint.Client.Grpc.LegacyProto;

namespace CometBFT.Client.Grpc.Tests;

public sealed class CometBftGrpcBranchCoverageTests
{
    private static GrpcChannel CreateChannel() => GrpcChannel.ForAddress("http://localhost:9090");

    [Fact]
    public void PublicConstructor_BlankBaseUrl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CometBftGrpcClient(Options.Create(new CometBftGrpcOptions { BaseUrl = " " })));
    }

    [Fact]
    public void PublicConstructor_NullBaseUrl_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CometBftGrpcClient(Options.Create(new CometBftGrpcOptions { BaseUrl = null! })));
    }

    [Fact]
    public async Task PublicConstructor_HttpsAddress_DoesNotThrow()
    {
        await using var client = new CometBftGrpcClient(Options.Create(new CometBftGrpcOptions
        {
            BaseUrl = "https://localhost:9090",
        }));

        var ex = await Record.ExceptionAsync(client.DisposeAsync().AsTask);
        Assert.Null(ex);
    }

    [Fact]
    public async Task PingAsync_WhenFactoryThrows_WrapsInCometBftGrpcException()
    {
        using var channel = CreateChannel();
        await using var client = new CometBftGrpcClient(channel, () => throw new InvalidOperationException("factory failed"));

        var ex = await Assert.ThrowsAsync<CometBftGrpcException>(() => client.PingAsync());
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task BroadcastTxAsync_WhenFactoryThrows_WrapsInCometBftGrpcException()
    {
        using var channel = CreateChannel();
        await using var client = new CometBftGrpcClient(channel, () => throw new InvalidOperationException("factory failed"));

        var ex = await Assert.ThrowsAsync<CometBftGrpcException>(() => client.BroadcastTxAsync([0x01]));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task InternalCtor_WithApiClient_DoesNotThrow()
    {
        using var channel = CreateChannel();
        var apiClient = NSubstitute.Substitute.For<IBroadcastApiClient>();
        await using var client = new CometBftGrpcClient(channel, apiClient);
        var ex = await Record.ExceptionAsync(client.DisposeAsync().AsTask);
        Assert.Null(ex);
    }

    [Fact]
    public void InternalCtor_WithApiClient_NullChannel_ThrowsArgumentNullException()
    {
        var apiClient = NSubstitute.Substitute.For<IBroadcastApiClient>();
        Assert.Throws<ArgumentNullException>(() => new CometBftGrpcClient((GrpcChannel)null!, apiClient));
    }

    [Fact]
    public async Task InternalCtor_WithFactory_DoesNotThrow()
    {
        using var channel = CreateChannel();
        var apiClient = NSubstitute.Substitute.For<IBroadcastApiClient>();
        await using var client = new CometBftGrpcClient(channel, () => Task.FromResult(apiClient));
        var ex = await Record.ExceptionAsync(client.DisposeAsync().AsTask);
        Assert.Null(ex);
    }

    [Fact]
    public void InternalCtor_WithFactory_NullChannel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CometBftGrpcClient((GrpcChannel)null!, () => Task.FromResult(NSubstitute.Substitute.For<IBroadcastApiClient>())));
    }

    [Fact]
    public async Task BroadcastTxAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        using var channel = CreateChannel();
        var apiClient = new ConfigurableCallInvoker();
        var client = new CometBftGrpcClient(channel, () => Task.FromResult<IBroadcastApiClient>(new GrpcChannelBroadcastApiClient(new BroadcastAPI.BroadcastAPIClient(apiClient))));
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.BroadcastTxAsync([0x01]));
    }

    [Fact]
    public async Task PingAsync_ConcurrentCalls_CreateClientOnlyOnce()
    {
        using var channel = CreateChannel();
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await Task.Delay(50);
            return true;
        });

        var factoryCalls = 0;
        await using var client = new CometBftGrpcClient(channel, async () =>
        {
            factoryCalls++;
            await Task.Delay(25);
            return apiClient;
        });

        await Task.WhenAll(client.PingAsync(), client.PingAsync(), client.PingAsync());

        Assert.Equal(1, factoryCalls);
        await apiClient.Received(3).PingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAndDetectAsync_WithCometBftProtocol_SetsDetectedProtocol()
    {
        using var channel = CreateChannel();
        var apiClient = Substitute.For<IBroadcastApiClient>();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var method = typeof(CometBftGrpcClient).GetMethod("CreateAndDetectAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task<IBroadcastApiClient>)method.Invoke(client, [channel, GrpcProtocol.CometBft, CancellationToken.None])!;
        var created = await task;

        Assert.IsType<GrpcChannelBroadcastApiClient>(created);
        Assert.Equal(GrpcProtocol.CometBft, client.DetectedProtocol);
    }

    [Fact]
    public async Task CreateAndDetectAsync_WithLegacyProtocol_SetsDetectedProtocol()
    {
        using var channel = CreateChannel();
        var apiClient = Substitute.For<IBroadcastApiClient>();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var method = typeof(CometBftGrpcClient).GetMethod("CreateAndDetectAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task<IBroadcastApiClient>)method.Invoke(client, [channel, GrpcProtocol.TendermintLegacy, CancellationToken.None])!;
        var created = await task;

        Assert.IsType<LegacyBroadcastApiClient>(created);
        Assert.Equal(GrpcProtocol.TendermintLegacy, client.DetectedProtocol);
    }

    [Fact]
    public async Task GetOrCreateClientAsync_WithoutApiClientOrFactory_ThrowsInvalidOperationException()
    {
#pragma warning disable SYSLIB0050 // Test-only use to exercise an otherwise unreachable defensive branch.
        var client = (CometBftGrpcClient)FormatterServices.GetUninitializedObject(typeof(CometBftGrpcClient));
#pragma warning restore SYSLIB0050
        typeof(CometBftGrpcClient).GetField("_channel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, GrpcChannel.ForAddress("http://localhost:9090"));
        typeof(CometBftGrpcClient).GetField("_initLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, new SemaphoreSlim(1, 1));

        var method = typeof(CometBftGrpcClient).GetMethod("GetOrCreateClientAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var task = (Task<IBroadcastApiClient>)method.Invoke(client, [CancellationToken.None])!;
            await task;
        });

        Assert.Contains("No API client or factory configured", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_InternalCtor_NullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GrpcChannelBroadcastApiClient((BroadcastAPI.BroadcastAPIClient)null!));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_InternalCtor_ValidClient_DoesNotThrow()
    {
        var ex = Record.Exception(() => new GrpcChannelBroadcastApiClient(new BroadcastAPI.BroadcastAPIClient(new ConfigurableCallInvoker())));
        Assert.Null(ex);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_InternalCtor_NullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LegacyBroadcastApiClient((LegacyProto.BroadcastAPI.BroadcastAPIClient)null!));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_InternalCtor_ValidClient_DoesNotThrow()
    {
        var ex = Record.Exception(() => new LegacyBroadcastApiClient(new LegacyProto.BroadcastAPI.BroadcastAPIClient(new ConfigurableCallInvoker())));
        Assert.Null(ex);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_PingAsync_OperationCanceledException_Propagates()
    {
        var client = new GrpcChannelBroadcastApiClient(new BroadcastAPI.BroadcastAPIClient(new CancelingCallInvoker()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PingAsync());
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_PingAsync_OperationCanceledException_Propagates()
    {
        var client = new LegacyBroadcastApiClient(new LegacyProto.BroadcastAPI.BroadcastAPIClient(new CancelingCallInvoker()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PingAsync());
    }

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_BroadcastTxAsync_WithAllFields_MapsTuple()
    {
        var client = new GrpcChannelBroadcastApiClient(
            new BroadcastAPI.BroadcastAPIClient(
                new ConfigurableCallInvoker().With("BroadcastTx", new ResponseBroadcastTx
                {
                    CheckTx = new ResponseCheckTx
                    {
                        Code = 7,
                        Data = Google.Protobuf.ByteString.CopyFromUtf8("payload"),
                        Log = "bad tx",
                        GasWanted = 99,
                        GasUsed = 12,
                        Codespace = "sdk",
                    },
                })));

        var result = await client.BroadcastTxAsync([0x01, 0x02]);

        Assert.Equal(7u, result.Code);
        Assert.Equal(Convert.ToBase64String(Google.Protobuf.ByteString.CopyFromUtf8("payload").ToByteArray()), result.Data);
        Assert.Equal("bad tx", result.Log);
        Assert.Equal("sdk", result.Codespace);
        Assert.Equal("0102", result.Hash);
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_BroadcastTxAsync_WithAllFields_MapsTuple()
    {
        var client = new LegacyBroadcastApiClient(
            new LegacyProto.BroadcastAPI.BroadcastAPIClient(
                new ConfigurableCallInvoker().With("BroadcastTx", new LegacyProto.ResponseBroadcastTx
                {
                    CheckTx = new LegacyProto.ResponseCheckTx
                    {
                        Code = 9,
                        Data = Google.Protobuf.ByteString.CopyFromUtf8("legacy"),
                        Log = "legacy bad tx",
                        GasWanted = 88,
                        GasUsed = 11,
                        Codespace = "sdk",
                    },
                })));

        var result = await client.BroadcastTxAsync([0x0A]);

        Assert.Equal(9u, result.Code);
        Assert.Equal("0A", result.Hash);
        Assert.Equal("sdk", result.Codespace);
        Assert.Equal("legacy bad tx", result.Log);
    }

    private sealed class CancelingCallInvoker : CallInvoker
    {
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            return new AsyncUnaryCall<TResponse>(
                Task.FromCanceled<TResponse>(new CancellationToken(true)),
                Task.FromResult(new Metadata()),
                () => Status.DefaultCancelled,
                () => new Metadata(),
                () => { });
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
            throw new NotImplementedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotImplementedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
            throw new NotImplementedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotImplementedException();
    }

    private sealed class ConfigurableCallInvoker : CallInvoker
    {
        private readonly Dictionary<string, object> _responses = new(StringComparer.Ordinal);

        public ConfigurableCallInvoker With<TResponse>(string methodName, TResponse response)
        {
            _responses[methodName] = response!;
            return this;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            if (_responses.TryGetValue(method.Name, out var value))
            {
                return new AsyncUnaryCall<TResponse>(
                    Task.FromResult((TResponse)value),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { });
            }

            throw new RpcException(new Status(StatusCode.Unimplemented, "not configured"));
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
            throw new NotImplementedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotImplementedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
            throw new NotImplementedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) =>
            throw new NotImplementedException();
    }
}
