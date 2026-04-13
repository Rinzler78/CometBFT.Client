using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc;
using CometBFT.Client.Grpc.Internal;
using Xunit;

#pragma warning disable CA1859 // Use concrete types — IBroadcastApiClient is intentional for substitution

namespace CometBFT.Client.Grpc.Tests;

/// <summary>
/// Unit tests for <see cref="CometBftGrpcClient"/>.
/// </summary>
public sealed class CometBftGrpcClientTests
{
    private static GrpcChannel CreateTestChannel() =>
        GrpcChannel.ForAddress("http://localhost:9090");

    [Fact]
    public async Task PingAsync_ReturnsTrue_WhenApiClientSucceeds()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>()).Returns(true);

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var result = await client.PingAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task PingAsync_ReturnsFalse_WhenApiClientReturnsFalse()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>()).Returns(false);

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var result = await client.PingAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task PingAsync_ThrowsCometBftGrpcException_WhenApiClientThrows()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("connection failed"));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.PingAsync());
    }

    [Fact]
    public async Task PingAsync_PreservesTypedGrpcException()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>())
            .Throws(new CometBftGrpcException("expected"));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.PingAsync());
    }

    [Fact]
    public async Task BroadcastTxAsync_ReturnsBroadcastResult_OnSuccess()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.BroadcastTxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((0u, (string?)null, (string?)"ok", 200L, 150L, (string?)null, "TXHASH"));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var result = await client.BroadcastTxAsync(new byte[] { 0x01, 0x02 });

        Assert.Equal(0u, result.Code);
        Assert.Equal("TXHASH", result.Hash);
        Assert.Equal("ok", result.Log);
        Assert.Equal(200L, result.GasWanted);
        Assert.Equal(150L, result.GasUsed);
    }

    [Fact]
    public async Task BroadcastTxAsync_MapsAllCheckTxFields()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.BroadcastTxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((4u, (string?)"dGVzdA==", (string?)"signature verification failed", 500L, 0L, (string?)"sdk", "AABBCC"));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var result = await client.BroadcastTxAsync(new byte[] { 0x01 });

        Assert.Equal(4u, result.Code);
        Assert.Equal("dGVzdA==", result.Data);
        Assert.Equal("signature verification failed", result.Log);
        Assert.Equal("sdk", result.Codespace);
        Assert.Equal("AABBCC", result.Hash);
        Assert.Equal(500L, result.GasWanted);
        Assert.Equal(0L, result.GasUsed);
    }

    [Fact]
    public async Task BroadcastTxAsync_ReturnsNullDataAndLog_WhenCheckTxFieldsAbsent()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.BroadcastTxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns((0u, (string?)null, (string?)null, 0L, 0L, (string?)null, "HASH"));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var result = await client.BroadcastTxAsync(new byte[] { 0x01 });

        Assert.Equal(0u, result.Code);
        Assert.Null(result.Data);
        Assert.Null(result.Log);
        Assert.Null(result.Codespace);
        Assert.Equal(0L, result.GasWanted);
        Assert.Equal(0L, result.GasUsed);
    }

    [Fact]
    public async Task BroadcastTxAsync_ThrowsArgumentNullException_ForNullTxBytes()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.BroadcastTxAsync(null!));
    }

    [Fact]
    public async Task BroadcastTxAsync_ThrowsCometBftGrpcException_OnRpcException()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.BroadcastTxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Throws(new RpcException(
                new Status(StatusCode.Unavailable, "Service unavailable")));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        await Assert.ThrowsAsync<CometBftGrpcException>(
            () => client.BroadcastTxAsync(new byte[] { 0x01 }));
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledTwice_NoThrow()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        var channel = CreateTestChannel();
        var client = new CometBftGrpcClient(channel, apiClient);

        await client.DisposeAsync();
        var ex = await Record.ExceptionAsync(client.DisposeAsync().AsTask);

        Assert.Null(ex);
    }

    [Fact]
    public async Task PingAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        var channel = CreateTestChannel();
        var client = new CometBftGrpcClient(channel, apiClient);
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.PingAsync());
    }

    [Fact]
    public async Task PublicConstructor_NormalizesBareHostAndCanDispose()
    {
        await using var client = new CometBftGrpcClient(Options.Create(new CometBftGrpcOptions
        {
            BaseUrl = "localhost:9090",
        }));

        var ex = await Record.ExceptionAsync(client.DisposeAsync().AsTask);
        Assert.Null(ex);
    }

    [Fact]
    public void PublicConstructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CometBftGrpcClient((IOptions<CometBftGrpcOptions>)null!));
    }

    [Fact]
    public async Task Protocol_CometBft_DetectedProtocolIsSetImmediately()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(Options.Create(new CometBftGrpcOptions
        {
            BaseUrl = "localhost:9090",
            Protocol = GrpcProtocol.CometBft,
        }));

        Assert.Equal(GrpcProtocol.CometBft, client.DetectedProtocol);
    }

    [Fact]
    public async Task Protocol_TendermintLegacy_DetectedProtocolIsSetImmediately()
    {
        await using var client = new CometBftGrpcClient(Options.Create(new CometBftGrpcOptions
        {
            BaseUrl = "localhost:9090",
            Protocol = GrpcProtocol.TendermintLegacy,
        }));

        Assert.Equal(GrpcProtocol.TendermintLegacy, client.DetectedProtocol);
    }

    [Fact]
    public async Task Protocol_Auto_DetectedProtocolIsNullBeforeFirstCall()
    {
        var channel = CreateTestChannel();
        var apiClient = Substitute.For<IBroadcastApiClient>();
        // Use factory overload — simulates auto detection
        await using var client = new CometBftGrpcClient(
            channel,
            () => Task.FromResult(apiClient));

        Assert.Null(client.DetectedProtocol);
    }

    [Fact]
    public async Task Protocol_Auto_UsesFactoryResolvedClient()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>()).Returns(true);

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(
            channel,
            () => Task.FromResult(apiClient));

        var result = await client.PingAsync();

        Assert.True(result);
        await apiClient.Received(1).PingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Protocol_Auto_FactoryCalledOnlyOnce_OnMultipleCalls()
    {
        var factoryCallCount = 0;
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>()).Returns(true);

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(
            channel,
            () => { factoryCallCount++; return Task.FromResult(apiClient); });

        await client.PingAsync();
        await client.PingAsync();
        await client.PingAsync();

        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public async Task BroadcastTxAsync_PreservesTypedGrpcException_OnCometBftGrpcException()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.BroadcastTxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Throws(new CometBftGrpcException("expected"));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        await Assert.ThrowsAsync<CometBftGrpcException>(() => client.BroadcastTxAsync(new byte[] { 0x01 }));
    }

    [Fact]
    public async Task PingAsync_PropagatesOperationCanceledException_WithoutWrapping()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.PingAsync());
    }

    [Fact]
    public async Task BroadcastTxAsync_PropagatesOperationCanceledException_WithoutWrapping()
    {
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.BroadcastTxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.BroadcastTxAsync(new byte[] { 0x01 }));
    }

    // ── Lines 134 / 136: generic Exception in BroadcastTxAsync ──────────────

    [Fact]
    public async Task BroadcastTxAsync_ThrowsCometBftGrpcException_OnGenericException()
    {
        // Covers lines 134 + 136: catch (Exception ex) → throw new CometBftGrpcException(…, ex)
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.BroadcastTxAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("unexpected failure"));

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(channel, apiClient);

        var ex = await Assert.ThrowsAsync<CometBftGrpcException>(
            () => client.BroadcastTxAsync(new byte[] { 0x01 }));

        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // ── Line 166: double-checked lock inner branch ───────────────────────────

    [Fact]
    public async Task GetOrCreateClient_FactoryCalledOnce_WhenConcurrentCallsRace()
    {
        // Covers line 166: the inner "_apiClient is not null" check inside the lock
        // when two tasks race past the outer null-check simultaneously.
        var factoryCallCount = 0;
        var barrier = new SemaphoreSlim(0, 1); // holds the first caller until we release
        var apiClient = Substitute.For<IBroadcastApiClient>();
        apiClient.PingAsync(Arg.Any<CancellationToken>()).Returns(true);

        var channel = CreateTestChannel();
        await using var client = new CometBftGrpcClient(
            channel,
            async () =>
            {
                factoryCallCount++;
                await barrier.WaitAsync().ConfigureAwait(false);
                return apiClient;
            });

        // Start two concurrent pings; the factory blocks until we release the barrier.
        var t1 = client.PingAsync();
        var t2 = client.PingAsync();

        // Release the factory — one caller wins the lock, the other sees _apiClient != null on re-entry.
        barrier.Release();

        await Task.WhenAll(t1, t2);

        // Factory must have been called exactly once even though two calls raced.
        Assert.Equal(1, factoryCallCount);
    }
}
