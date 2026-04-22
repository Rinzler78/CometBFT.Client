using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Codecs;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Extensions;
using CometBFT.Client.Rest;
using CometBFT.Client.WebSocket;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

/// <summary>
/// Coverage-oriented tests for dependency injection registrations.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCometBftRest_RegistersClientAndOptions()
    {
        var services = new ServiceCollection();
        services.AddCometBftRest(options => options.BaseUrl = "https://localhost:26657");
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftRestClient>());
        Assert.Equal("https://localhost:26657", provider.GetRequiredService<IOptions<CometBftRestOptions>>().Value.BaseUrl);
    }

    [Fact]
    public void AddCometBftRest_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddCometBftRest(o => o.BaseUrl = "https://localhost:26657"));
    }

    [Fact]
    public void AddCometBftRest_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddCometBftRest(null!));
    }

    [Fact]
    public void AddCometBftWebSocket_RegistersClientAndOptions()
    {
        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options => options.BaseUrl = "ws://localhost:26657/websocket");
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftWebSocketClient>());
        Assert.Equal("ws://localhost:26657/websocket", provider.GetRequiredService<IOptions<CometBftWebSocketOptions>>().Value.BaseUrl);
    }

    [Fact]
    public void AddCometBftGrpc_RegistersClientAndOptions()
    {
        var services = new ServiceCollection();
        services.AddCometBftGrpc(options => options.BaseUrl = "https://localhost:9090");
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftGrpcClient>());
        Assert.Equal("https://localhost:9090", provider.GetRequiredService<IOptions<CometBftGrpcOptions>>().Value.BaseUrl);
    }

    [Fact]
    public void AddCometBftWebSocket_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddCometBftWebSocket(_ => { }));
    }

    [Fact]
    public void AddCometBftWebSocket_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddCometBftWebSocket(null!));
    }

    [Fact]
    public void AddCometBftGrpc_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddCometBftGrpc(_ => { }));
    }

    [Fact]
    public void AddCometBftGrpc_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddCometBftGrpc(null!));
    }

    [Fact]
    public void AddCometBftWebSocket_Typed_RegistersTypedClient()
    {
        var services = new ServiceCollection();
        services.AddCometBftWebSocket<string>(
            options => options.BaseUrl = "ws://localhost:26657/websocket",
            RawTxCodec.Instance);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftWebSocketClient<string>>());
        Assert.Equal("ws://localhost:26657/websocket",
            provider.GetRequiredService<IOptions<CometBftWebSocketOptions>>().Value.BaseUrl);
    }

    [Fact]
    public void AddCometBftWebSocket_Typed_CodecRegisteredAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddCometBftWebSocket<string>(
            options => options.BaseUrl = "ws://localhost:26657/websocket",
            RawTxCodec.Instance);
        var provider = services.BuildServiceProvider();

        var codec = provider.GetRequiredService<ITxCodec<string>>();
        Assert.Same(RawTxCodec.Instance, codec);
    }

    [Fact]
    public void AddCometBftWebSocket_Typed_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftWebSocket<string>(_ => { }, RawTxCodec.Instance));
    }

    [Fact]
    public void AddCometBftWebSocket_Typed_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftWebSocket<string>(null!, RawTxCodec.Instance));
    }

    [Fact]
    public void AddCometBftWebSocket_Typed_NullCodec_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftWebSocket<string>(_ => { }, null!));
    }

    // ── AddCometBftClient ────────────────────────────────────────────────────

    [Fact]
    public void AddCometBftClient_RegistersFullStack()
    {
        var services = new ServiceCollection();
        services.AddCometBftClient();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftRestClient>());
        Assert.NotNull(provider.GetRequiredService<ICometBftGrpcClient>());
        Assert.NotNull(provider.GetRequiredService<ICometBftWebSocketClient>());
    }

    [Fact]
    public void AddCometBftClient_PropagatesTimeoutToRestAndGrpc()
    {
        var services = new ServiceCollection();
        services.AddCometBftClient(options => options.Timeout = TimeSpan.FromSeconds(12));
        var provider = services.BuildServiceProvider();

        Assert.Equal(TimeSpan.FromSeconds(12), provider.GetRequiredService<IOptions<CometBftRestOptions>>().Value.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(12), provider.GetRequiredService<IOptions<CometBftGrpcOptions>>().Value.Timeout);
    }

    [Fact]
    public void AddCometBftClient_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddCometBftClient());
    }

    [Fact]
    public void AddCometBftClient_WithNullConfigure_UsesDefaults()
    {
        var services = new ServiceCollection();
        var ex = Record.Exception(() => services.AddCometBftClient(null));
        Assert.Null(ex);
    }

    // ── Generic overloads (Phase 5 — extensibility-v2) ───────────────────────

    [Fact]
    public void AddCometBftRest_Generic_ResolvesCustomInterface()
    {
        var services = new ServiceCollection();
        services.AddCometBftRest<ICometBftRestClient, CometBftRestClient>(
            options => options.BaseUrl = "https://localhost:26657");
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftRestClient>());
    }

    [Fact]
    public void AddCometBftRest_Generic_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftRest<ICometBftRestClient, CometBftRestClient>(
                o => o.BaseUrl = "https://localhost:26657"));
    }

    [Fact]
    public void AddCometBftRest_Generic_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftRest<ICometBftRestClient, CometBftRestClient>(null!));
    }

    [Fact]
    public void AddCometBftWebSocket_Generic_ResolvesCustomInterface()
    {
        var services = new ServiceCollection();
        services.AddCometBftWebSocket<string, ICometBftWebSocketClient<string>, CometBftWebSocketClient<string>>(
            options => options.BaseUrl = "ws://localhost:26657/websocket",
            RawTxCodec.Instance);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftWebSocketClient<string>>());
    }

    [Fact]
    public void AddCometBftWebSocket_Generic_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftWebSocket<string, ICometBftWebSocketClient<string>, CometBftWebSocketClient<string>>(
                _ => { }, RawTxCodec.Instance));
    }

    [Fact]
    public void AddCometBftWebSocket_Generic_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftWebSocket<string, ICometBftWebSocketClient<string>, CometBftWebSocketClient<string>>(
                null!, RawTxCodec.Instance));
    }

    [Fact]
    public void AddCometBftWebSocket_Generic_NullCodec_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(
            () => services.AddCometBftWebSocket<string, ICometBftWebSocketClient<string>, CometBftWebSocketClient<string>>(
                _ => { }, null!));
    }

    // ── Fully-parameterized overloads (5-param REST / 6-param WebSocket) ─────

    [Fact]
    public void AddCometBftRest_FullyParameterized_ResolvesCustomInterface()
    {
        var services = new ServiceCollection();
        services.AddCometBftRest<
            CometBFT.Client.Core.Domain.Block,
            CometBFT.Client.Core.Domain.TxResult,
            CometBFT.Client.Core.Domain.Validator,
            ICometBftRestClient,
            CometBftRestClient>(
            options => options.BaseUrl = "https://localhost:26657");
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftRestClient>());
    }

    [Fact]
    public void AddCometBftWebSocket_FullyParameterized_ResolvesCustomInterface()
    {
        var services = new ServiceCollection();
        services.AddCometBftWebSocket<
            string,
            CometBFT.Client.Core.Domain.Block<string>,
            CometBFT.Client.Core.Domain.TxResult<string>,
            CometBFT.Client.Core.Domain.Validator,
            ICometBftWebSocketClient<string>,
            CometBftWebSocketClient<string>>(
            options => options.BaseUrl = "ws://localhost:26657/websocket",
            RawTxCodec.Instance);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICometBftWebSocketClient<string>>());
    }
}
