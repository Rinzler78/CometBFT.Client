using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Codecs;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Extensions;
using CometBFT.Client.Rest;
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
}
