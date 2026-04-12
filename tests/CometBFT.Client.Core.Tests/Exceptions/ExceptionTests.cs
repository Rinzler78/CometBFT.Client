using CometBFT.Client.Core.Exceptions;
using Xunit;

namespace CometBFT.Client.Core.Tests.Exceptions;

/// <summary>
/// Unit tests for the CometBFT client exception hierarchy.
/// </summary>
public sealed class ExceptionTests
{
    [Fact]
    public void CometBftClientException_DefaultConstructor_HasNullMessage()
    {
        var ex = new CometBftClientException();
        Assert.NotNull(ex);
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
}
