using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc.Internal;
using CometBFT.Client.Grpc.Proto;
using Xunit;
using LegacyProto = Tendermint.Client.Grpc.LegacyProto;

namespace CometBFT.Client.Grpc.Tests;

/// <summary>
/// Unit tests for <see cref="LegacyBroadcastApiClient"/>, <see cref="GrpcChannelBroadcastApiClient"/>,
/// and <see cref="BroadcastApiClientFactory"/> constructor guard clauses and protocol selection.
/// </summary>
public sealed class BroadcastApiInternalTests
{
    private static GrpcChannel LocalChannel() =>
        GrpcChannel.ForAddress("https://localhost:9090");

    // ── LegacyBroadcastApiClient ─────────────────────────────────────────────

    [Fact]
    public void LegacyBroadcastApiClient_NullChannel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LegacyBroadcastApiClient((global::Grpc.Net.Client.GrpcChannel)null!));
    }

    [Fact]
    public void LegacyBroadcastApiClient_ValidChannel_DoesNotThrow()
    {
        using var channel = LocalChannel();
        var ex = Record.Exception(() => new LegacyBroadcastApiClient(channel));
        Assert.Null(ex);
    }

    // ── GrpcChannelBroadcastApiClient ────────────────────────────────────────

    [Fact]
    public void GrpcChannelBroadcastApiClient_NullChannel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GrpcChannelBroadcastApiClient((global::Grpc.Net.Client.GrpcChannel)null!));
    }

    [Fact]
    public void GrpcChannelBroadcastApiClient_ValidChannel_DoesNotThrow()
    {
        using var channel = LocalChannel();
        var ex = Record.Exception(() => new GrpcChannelBroadcastApiClient(channel));
        Assert.Null(ex);
    }

    // ── BroadcastApiClientFactory — deterministic paths ─────────────────────

    [Fact]
    public async Task CreateAsync_CometBftProtocol_ReturnsGrpcChannelBroadcastApiClient()
    {
        using var channel = LocalChannel();
        var client = await BroadcastApiClientFactory.CreateAsync(
            channel, GrpcProtocol.CometBft, CancellationToken.None);

        Assert.IsType<GrpcChannelBroadcastApiClient>(client);
    }

    [Fact]
    public async Task CreateAsync_TendermintLegacyProtocol_ReturnsLegacyBroadcastApiClient()
    {
        using var channel = LocalChannel();
        var client = await BroadcastApiClientFactory.CreateAsync(
            channel, GrpcProtocol.TendermintLegacy, CancellationToken.None);

        Assert.IsType<LegacyBroadcastApiClient>(client);
    }

    [Fact]
    public async Task CreateAsync_AutoProtocol_PreCancelledToken_ReturnsGrpcChannelBroadcastApiClient()
    {
        // Pre-cancelled token triggers the generic catch {} in DetectAsync —
        // the factory must default to GrpcChannelBroadcastApiClient rather than throw.
        using var channel = LocalChannel();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var client = await BroadcastApiClientFactory.CreateAsync(
            channel, GrpcProtocol.Auto, cts.Token);

        Assert.IsType<GrpcChannelBroadcastApiClient>(client);
    }

    // ── PingAsync — RpcException path ────────────────────────────────────────

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_PingAsync_OnRpcException_ReturnsFalse()
    {
        // Port 1 is always refused; Grpc.Net.Client wraps it as RpcException(Unavailable)
        using var channel = GrpcChannel.ForAddress("http://localhost:1");
        var client = new GrpcChannelBroadcastApiClient(channel);
        var result = await client.PingAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_PingAsync_OnRpcException_ReturnsFalse()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:1");
        var client = new LegacyBroadcastApiClient(channel);
        var result = await client.PingAsync();
        Assert.False(result);
    }

    // ── BroadcastApiClientFactory — Unimplemented path ───────────────────────

    private sealed class UnimplementedGrpcHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = new Version(2, 0)
            };
            var content = new ByteArrayContent(Array.Empty<byte>());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            response.Content = content;
            response.TrailingHeaders.TryAddWithoutValidation("grpc-status", "12"); // Unimplemented
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task CreateAsync_AutoProtocol_UnimplementedResponse_ReturnsLegacyClient()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = new UnimplementedGrpcHandler()
        });

        var client = await BroadcastApiClientFactory.CreateAsync(
            channel, GrpcProtocol.Auto, CancellationToken.None);

        Assert.IsType<LegacyBroadcastApiClient>(client);
    }

    // ── FakeCallInvoker (shared) ─────────────────────────────────────────────

    private sealed class FakeCallInvoker : CallInvoker
    {
        private readonly Dictionary<string, object> _responses = new(StringComparer.Ordinal);

        public FakeCallInvoker With<TResponse>(string methodName, TResponse response)
        {
            _responses[methodName] = response!;
            return this;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            if (_responses.TryGetValue(method.Name, out var obj))
            {
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

    // ── GrpcChannel public constructor ───────────────────────────────────────

    [Fact]
    public void GrpcChannelBroadcastApiClient_CtorChannel_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new GrpcChannelBroadcastApiClient((GrpcChannel)null!));
    }

    [Fact]
    public void GrpcChannelBroadcastApiClient_CtorChannel_CreatesInstance()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:1");
        var client = new GrpcChannelBroadcastApiClient(channel);
        Assert.NotNull(client);
    }

    [Fact]
    public void LegacyBroadcastApiClient_CtorChannel_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new LegacyBroadcastApiClient((GrpcChannel)null!));
    }

    [Fact]
    public void LegacyBroadcastApiClient_CtorChannel_CreatesInstance()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:1");
        var client = new LegacyBroadcastApiClient(channel);
        Assert.NotNull(client);
    }

    // ── PingAsync failure path (RpcException) ────────────────────────────────

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_PingAsync_RpcException_ReturnsFalse()
    {
        var invoker = new FakeCallInvoker(); // no "Ping" configured → throws RpcException
        var protoClient = new BroadcastAPI.BroadcastAPIClient(invoker);
        var client = new GrpcChannelBroadcastApiClient(protoClient);

        var result = await client.PingAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_PingAsync_RpcException_ReturnsFalse()
    {
        var invoker = new FakeCallInvoker(); // no "Ping" configured → throws RpcException
        var protoClient = new LegacyProto.BroadcastAPI.BroadcastAPIClient(invoker);
        var client = new LegacyBroadcastApiClient(protoClient);

        var result = await client.PingAsync();

        Assert.False(result);
    }

    // ── PingAsync success path ───────────────────────────────────────────────

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_PingAsync_Success_ReturnsTrue()
    {
        var invoker = new FakeCallInvoker().With("Ping", new ResponsePing());
        var protoClient = new BroadcastAPI.BroadcastAPIClient(invoker);
        var client = new GrpcChannelBroadcastApiClient(protoClient);

        var result = await client.PingAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_PingAsync_Success_ReturnsTrue()
    {
        var invoker = new FakeCallInvoker().With("Ping", new LegacyProto.ResponsePing());
        var protoClient = new LegacyProto.BroadcastAPI.BroadcastAPIClient(invoker);
        var client = new LegacyBroadcastApiClient(protoClient);

        var result = await client.PingAsync();

        Assert.True(result);
    }

    // ── BroadcastTxAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GrpcChannelBroadcastApiClient_BroadcastTxAsync_ReturnsResult()
    {
        var invoker = new FakeCallInvoker()
            .With("BroadcastTx", new ResponseBroadcastTx
            {
                CheckTx = new ResponseCheckTx { Code = 0, GasWanted = 100, GasUsed = 80 }
            });
        var protoClient = new BroadcastAPI.BroadcastAPIClient(invoker);
        var client = new GrpcChannelBroadcastApiClient(protoClient);

        var result = await client.BroadcastTxAsync([0xDE, 0xAD]);

        Assert.Equal(0u, result.Code);
        Assert.Equal(100L, result.GasWanted);
        Assert.Equal(80L, result.GasUsed);
    }

    [Fact]
    public async Task LegacyBroadcastApiClient_BroadcastTxAsync_ReturnsResult()
    {
        var invoker = new FakeCallInvoker()
            .With("BroadcastTx", new LegacyProto.ResponseBroadcastTx
            {
                CheckTx = new LegacyProto.ResponseCheckTx { Code = 0, GasWanted = 100, GasUsed = 80 }
            });
        var protoClient = new LegacyProto.BroadcastAPI.BroadcastAPIClient(invoker);
        var client = new LegacyBroadcastApiClient(protoClient);

        var result = await client.BroadcastTxAsync([0xDE, 0xAD]);

        Assert.Equal(0u, result.Code);
        Assert.Equal(100L, result.GasWanted);
    }
}
