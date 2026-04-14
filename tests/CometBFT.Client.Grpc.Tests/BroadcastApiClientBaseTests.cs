using Google.Protobuf;
using CometBFT.Client.Grpc.Internal;
using Xunit;

namespace CometBFT.Client.Grpc.Tests;

/// <summary>
/// Unit tests for <see cref="BroadcastApiClientBase"/>.
/// Covers lines 34-43 (the only executable lines in the file, 0% → 90%+).
/// </summary>
public sealed class BroadcastApiClientBaseTests
{
    [Fact]
    public void BuildResult_WithNonEmptyFields_ReturnsAllFieldsPopulated()
    {
        // Arrange
        var txBytes = new byte[] { 0x01, 0x02, 0x03 };
        var data = ByteString.CopyFromUtf8("some-data");
        const string log = "success";
        const string codespace = "sdk";

        // Act
        var (code, dataOut, logOut, gasWanted, gasUsed, codespaceOut, hash) =
            BroadcastApiClientBase.BuildResult(
                code: 0u,
                data: data,
                log: log,
                gasWanted: 200L,
                gasUsed: 150L,
                codespace: codespace,
                txBytes: txBytes);

        // Assert — line 34: hash = Convert.ToHexString(txBytes)
        Assert.Equal(Convert.ToHexString(txBytes), hash);
        // line 38: non-empty data → base64
        Assert.Equal(Convert.ToBase64String(data.ToByteArray()), dataOut);
        // line 39: non-empty log → returned as-is
        Assert.Equal(log, logOut);
        // line 42: non-empty codespace → returned as-is
        Assert.Equal(codespace, codespaceOut);
        Assert.Equal(0u, code);
        Assert.Equal(200L, gasWanted);
        Assert.Equal(150L, gasUsed);
    }

    [Fact]
    public void BuildResult_WithEmptyData_ReturnsNullData()
    {
        // Covers line 38: data.IsEmpty → null
        var txBytes = new byte[] { 0xAA };
        var (_, dataOut, _, _, _, _, _) =
            BroadcastApiClientBase.BuildResult(
                code: 1u,
                data: ByteString.Empty,
                log: "ok",
                gasWanted: 0L,
                gasUsed: 0L,
                codespace: string.Empty,
                txBytes: txBytes);

        Assert.Null(dataOut);
    }

    [Fact]
    public void BuildResult_WithEmptyLog_ReturnsNullLog()
    {
        // Covers line 39: string.IsNullOrEmpty(log) → null
        var txBytes = new byte[] { 0xBB };
        var (_, _, logOut, _, _, _, _) =
            BroadcastApiClientBase.BuildResult(
                code: 0u,
                data: ByteString.Empty,
                log: string.Empty,
                gasWanted: 0L,
                gasUsed: 0L,
                codespace: string.Empty,
                txBytes: txBytes);

        Assert.Null(logOut);
    }

    [Fact]
    public void BuildResult_WithEmptyCodespace_ReturnsNullCodespace()
    {
        // Covers line 42: string.IsNullOrEmpty(codespace) → null
        var txBytes = new byte[] { 0xCC };
        var (_, _, _, _, _, codespaceOut, _) =
            BroadcastApiClientBase.BuildResult(
                code: 0u,
                data: ByteString.Empty,
                log: string.Empty,
                gasWanted: 0L,
                gasUsed: 0L,
                codespace: string.Empty,
                txBytes: txBytes);

        Assert.Null(codespaceOut);
    }

    [Fact]
    public void BuildResult_HashIsHexEncodedTxBytes()
    {
        // Additional validation that the hash is always the hex of txBytes (line 34)
        var txBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var (_, _, _, _, _, _, hash) =
            BroadcastApiClientBase.BuildResult(
                code: 0u,
                data: ByteString.Empty,
                log: string.Empty,
                gasWanted: 0L,
                gasUsed: 0L,
                codespace: string.Empty,
                txBytes: txBytes);

        Assert.Equal("DEADBEEF", hash);
    }
}
