using CometBFT.Client.Core.Exceptions;
using Xunit;

namespace CometBFT.Client.Core.Tests.Exceptions;

/// <summary>
/// Unit tests for the CometBFT client exception hierarchy.
/// </summary>
public sealed class ExceptionTests
{
    [Fact]
    public void CometBftClientException_DefaultConstructor_DoesNotThrow()
    {
        var ex = new CometBftClientException();
        Assert.NotNull(ex);
        Assert.NotEmpty(ex.Message); // default Exception message is never null/empty
    }

    [Fact]
    public void CometBftClientException_MessageConstructor_PreservesMessage()
    {
        var ex = new CometBftClientException("test message");
        Assert.Equal("test message", ex.Message);
    }

    [Fact]
    public void CometBftClientException_InnerException_IsPreserved()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CometBftClientException("outer", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void CometBftRestException_IsACometBftClientException()
    {
        var ex = new CometBftRestException("rest error");
        Assert.IsAssignableFrom<CometBftClientException>(ex);
    }

    [Fact]
    public void CometBftRestException_WithStatusCode_StoresCode()
    {
        var ex = new CometBftRestException("not found", System.Net.HttpStatusCode.NotFound);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public void CometBftRestException_WithRpcErrorCode_StoresCode()
    {
        var ex = new CometBftRestException("rpc error", -32601);
        Assert.Equal(-32601, ex.RpcErrorCode);
    }

    [Fact]
    public void CometBftWebSocketException_IsACometBftClientException()
    {
        var ex = new CometBftWebSocketException("ws error");
        Assert.IsAssignableFrom<CometBftClientException>(ex);
    }

    [Fact]
    public void CometBftGrpcException_IsACometBftClientException()
    {
        var ex = new CometBftGrpcException("grpc error");
        Assert.IsAssignableFrom<CometBftClientException>(ex);
    }

    [Fact]
    public void CometBftGrpcException_WithStatusCode_StoresCode()
    {
        var ex = new CometBftGrpcException("unavailable", 14);
        Assert.Equal(14, ex.GrpcStatusCode);
    }

    // ── Additional overloads ─────────────────────────────────────────────────

    [Fact]
    public void CometBftRestException_Parameterless_IsException()
    {
        var ex = new CometBftRestException();
        Assert.IsAssignableFrom<CometBftClientException>(ex);
    }

    [Fact]
    public void CometBftRestException_MessageAndStatusCode_WithInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CometBftRestException("msg", System.Net.HttpStatusCode.InternalServerError, inner);
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void CometBftRestException_MessageAndRpcCode_WithInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CometBftRestException("msg", -32600, inner);
        Assert.Equal(-32600, ex.RpcErrorCode);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void CometBftRestException_MessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CometBftRestException("msg", inner);
        Assert.Equal("msg", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void CometBftWebSocketException_Parameterless_IsException()
    {
        var ex = new CometBftWebSocketException();
        Assert.IsAssignableFrom<CometBftClientException>(ex);
    }

    [Fact]
    public void CometBftGrpcException_Parameterless_IsException()
    {
        var ex = new CometBftGrpcException();
        Assert.IsAssignableFrom<CometBftClientException>(ex);
    }

    [Fact]
    public void CometBftGrpcException_MessageAndCodeAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CometBftGrpcException("unavailable", 14, inner);
        Assert.Equal(14, ex.GrpcStatusCode);
        Assert.Same(inner, ex.InnerException);
    }
}
