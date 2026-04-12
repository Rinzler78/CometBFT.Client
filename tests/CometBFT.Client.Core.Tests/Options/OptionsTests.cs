using CometBFT.Client.Core.Options;
using Xunit;

namespace CometBFT.Client.Core.Tests.Options;

/// <summary>
/// Unit tests for CometBFT client options classes.
/// </summary>
public sealed class OptionsTests
{
    [Fact]
    public void CometBftRestOptions_DefaultBaseUrl()
    {
        var opts = new CometBftRestOptions();
        Assert.Equal("http://localhost:26657", opts.BaseUrl);
    }

    [Fact]
    public void CometBftRestOptions_DefaultTimeout_Is30Seconds()
    {
        var opts = new CometBftRestOptions();
        Assert.Equal(TimeSpan.FromSeconds(30), opts.Timeout);
    }

    [Fact]
    public void CometBftRestOptions_DefaultMaxRetry_Is3()
    {
        var opts = new CometBftRestOptions();
        Assert.Equal(3, opts.MaxRetryAttempts);
    }

    [Fact]
    public void CometBftWebSocketOptions_DefaultBaseUrl()
    {
        var opts = new CometBftWebSocketOptions();
        Assert.Equal("ws://localhost:26657/websocket", opts.BaseUrl);
    }

    [Fact]
    public void CometBftGrpcOptions_DefaultBaseUrl()
    {
        var opts = new CometBftGrpcOptions();
        Assert.Equal("http://localhost:9090", opts.BaseUrl);
    }

    [Fact]
    public void CometBftRestOptions_CanSetBaseUrl()
    {
        var opts = new CometBftRestOptions { BaseUrl = "http://mynode:26657" };
        Assert.Equal("http://mynode:26657", opts.BaseUrl);
    }
}
