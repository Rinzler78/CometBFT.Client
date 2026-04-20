using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc;
using Xunit;

namespace CometBFT.Client.Grpc.Tests;

/// <summary>
/// Unit tests for <see cref="CometBftSdkGrpcClient"/> guard clauses.
/// Live gRPC connectivity is required for method-level behavior; those are in Integration tests.
/// </summary>
public sealed class CometBftSdkGrpcClientTests
{
    private static CometBftSdkGrpcOptions DefaultOptions() => new()
    {
        BaseUrl = "https://localhost:9090",
        Timeout = TimeSpan.FromSeconds(10),
    };

    private static GrpcChannel TestChannel() =>
        GrpcChannel.ForAddress("https://localhost:9090");

    // ── ObjectDisposedException guards (new C0 methods) ─────────────────────

    [Fact]
    public async Task GetSyncingAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetSyncingAsync());
    }

    [Fact]
    public async Task GetBlockByHeightAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetBlockByHeightAsync(1));
    }

    [Fact]
    public async Task GetValidatorSetByHeightAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetValidatorSetByHeightAsync(1));
    }

    [Fact]
    public async Task ABCIQueryAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ABCIQueryAsync("/app/version", []));
    }

    // ── Pre-existing methods also protected after dispose ────────────────────

    [Fact]
    public async Task GetStatusAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetStatusAsync());
    }

    [Fact]
    public async Task GetLatestBlockAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetLatestBlockAsync());
    }

    [Fact]
    public async Task GetLatestValidatorsAsync_ThrowsObjectDisposedException_AfterDispose()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetLatestValidatorsAsync());
    }

    // ── ArgumentNullException guards ─────────────────────────────────────────

    [Fact]
    public async Task ABCIQueryAsync_ThrowsArgumentNullException_ForNullData()
    {
        await using var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ABCIQueryAsync("/app/version", null!));
    }

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void InternalConstructor_NullChannel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CometBftSdkGrpcClient((GrpcChannel)null!, DefaultOptions()));
    }

    [Fact]
    public void InternalConstructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CometBftSdkGrpcClient(TestChannel(), null!));
    }

    [Fact]
    public void PublicConstructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CometBftSdkGrpcClient((IOptions<CometBftSdkGrpcOptions>)null!));
    }

    // ── Dispose idempotency ───────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_CanBeCalledTwice_NoThrow()
    {
        var client = new CometBftSdkGrpcClient(TestChannel(), DefaultOptions());

        await client.DisposeAsync();
        var ex = await Record.ExceptionAsync(client.DisposeAsync().AsTask);

        Assert.Null(ex);
    }
}
