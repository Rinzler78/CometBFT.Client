using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

/// <summary>
/// Tests for the <c>AddCometBftRest</c> DI extension method.
/// </summary>
public sealed class DiRegistrationTests
{
    [Fact]
    public void AddCometBftRest_RegistersICometBftRestClient()
    {
        var services = new ServiceCollection();
        services.AddCometBftRest(o => o.BaseUrl = "http://localhost:26657");
        var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ICometBftRestClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddCometBftRest_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() =>
            services.AddCometBftRest(o => o.BaseUrl = "http://localhost:26657"));
    }

    [Fact]
    public void AddCometBftRest_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddCometBftRest(null!));
    }
}
