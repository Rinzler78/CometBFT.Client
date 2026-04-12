using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Extensions;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

/// <summary>
/// Coverage-oriented tests for dependency injection registrations.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
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
}
