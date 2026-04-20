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

    // ── CometBftRestOptions.Validate() ──────────────────────────────────────

    [Fact]
    public void CometBftRestOptions_Validate_ValidDefaults_DoesNotThrow()
    {
        var opts = new CometBftRestOptions();
        var ex = Record.Exception(opts.Validate);
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    public void CometBftRestOptions_Validate_InvalidBaseUrl_Throws(string baseUrl)
    {
        var opts = new CometBftRestOptions { BaseUrl = baseUrl };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    [Fact]
    public void CometBftRestOptions_Validate_ZeroTimeout_Throws()
    {
        var opts = new CometBftRestOptions { Timeout = TimeSpan.Zero };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    [Fact]
    public void CometBftRestOptions_Validate_NegativeMaxRetry_Throws()
    {
        var opts = new CometBftRestOptions { MaxRetryAttempts = -1 };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    // ── CometBftWebSocketOptions.Validate() ─────────────────────────────────

    [Fact]
    public void CometBftWebSocketOptions_Validate_ValidDefaults_DoesNotThrow()
    {
        var opts = new CometBftWebSocketOptions();
        var ex = Record.Exception(opts.Validate);
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    public void CometBftWebSocketOptions_Validate_InvalidBaseUrl_Throws(string baseUrl)
    {
        var opts = new CometBftWebSocketOptions { BaseUrl = baseUrl };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    [Fact]
    public void CometBftWebSocketOptions_Validate_ZeroReconnectTimeout_Throws()
    {
        var opts = new CometBftWebSocketOptions { ReconnectTimeout = TimeSpan.Zero };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    [Fact]
    public void CometBftWebSocketOptions_Validate_ZeroErrorReconnectTimeout_Throws()
    {
        var opts = new CometBftWebSocketOptions { ErrorReconnectTimeout = TimeSpan.Zero };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    // ── CometBftGrpcOptions.Validate() ──────────────────────────────────────

    [Fact]
    public void CometBftGrpcOptions_Validate_ValidDefaults_DoesNotThrow()
    {
        var opts = new CometBftGrpcOptions();
        var ex = Record.Exception(opts.Validate);
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("host/path/with/slashes")] // path segment → rejected
    public void CometBftGrpcOptions_Validate_InvalidBaseUrl_Throws(string baseUrl)
    {
        var opts = new CometBftGrpcOptions { BaseUrl = baseUrl };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    [Theory]
    [InlineData("localhost:9090")]   // bare host:port
    [InlineData("localhost")]        // bare host only
    [InlineData("https://localhost:9090")] // full URI
    public void CometBftGrpcOptions_Validate_ValidGrpcAddress_DoesNotThrow(string baseUrl)
    {
        var opts = new CometBftGrpcOptions { BaseUrl = baseUrl };
        var ex = Record.Exception(opts.Validate);
        Assert.Null(ex);
    }

    [Fact]
    public void CometBftGrpcOptions_Validate_ZeroTimeout_Throws()
    {
        var opts = new CometBftGrpcOptions { Timeout = TimeSpan.Zero };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    // ── CometBftSdkGrpcOptions.Validate() ───────────────────────────────────

    [Fact]
    public void CometBftSdkGrpcOptions_Validate_ValidDefaults_DoesNotThrow()
    {
        var opts = new CometBftSdkGrpcOptions();
        var ex = Record.Exception(opts.Validate);
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    public void CometBftSdkGrpcOptions_Validate_InvalidBaseUrl_Throws(string baseUrl)
    {
        var opts = new CometBftSdkGrpcOptions { BaseUrl = baseUrl };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    [Fact]
    public void CometBftSdkGrpcOptions_Validate_ZeroTimeout_Throws()
    {
        var opts = new CometBftSdkGrpcOptions { Timeout = TimeSpan.Zero };
        Assert.Throws<InvalidOperationException>(opts.Validate);
    }

    // ── CometBftClientOptions (aggregated options, no Validate) ─────────────

    [Fact]
    public void CometBftClientOptions_DefaultRestBaseUrl()
    {
        var opts = new CometBftClientOptions();
        Assert.Equal("https://cosmoshub.tendermintrpc.lava.build:443", opts.RestBaseUrl);
    }

    [Fact]
    public void CometBftClientOptions_DefaultGrpcBaseUrl()
    {
        var opts = new CometBftClientOptions();
        Assert.Equal("https://cosmoshub.grpc.lava.build:443", opts.GrpcBaseUrl);
    }

    [Fact]
    public void CometBftClientOptions_DefaultWebSocketBaseUrl()
    {
        var opts = new CometBftClientOptions();
        Assert.Equal("wss://cosmoshub.tendermintrpc.lava.build:443/websocket", opts.WebSocketBaseUrl);
    }

    [Fact]
    public void CometBftClientOptions_DefaultTimeout_Is30Seconds()
    {
        var opts = new CometBftClientOptions();
        Assert.Equal(TimeSpan.FromSeconds(30), opts.Timeout);
    }

    [Fact]
    public void CometBftClientOptions_DefaultMaxRetryAttempts_Is3()
    {
        var opts = new CometBftClientOptions();
        Assert.Equal(3, opts.MaxRetryAttempts);
    }

    [Fact]
    public void CometBftClientOptions_DefaultRetryDelay_Is1Second()
    {
        var opts = new CometBftClientOptions();
        Assert.Equal(TimeSpan.FromSeconds(1), opts.RetryDelay);
    }

    [Fact]
    public void CometBftClientOptions_CanOverrideAllProperties()
    {
        var opts = new CometBftClientOptions
        {
            RestBaseUrl = "http://mynode:26657",
            GrpcBaseUrl = "http://mynode:9090",
            WebSocketBaseUrl = "ws://mynode:26657/websocket",
            Timeout = TimeSpan.FromSeconds(60),
            MaxRetryAttempts = 5,
            RetryDelay = TimeSpan.FromMilliseconds(500),
        };

        Assert.Equal("http://mynode:26657", opts.RestBaseUrl);
        Assert.Equal("http://mynode:9090", opts.GrpcBaseUrl);
        Assert.Equal("ws://mynode:26657/websocket", opts.WebSocketBaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(60), opts.Timeout);
        Assert.Equal(5, opts.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(500), opts.RetryDelay);
    }
}
