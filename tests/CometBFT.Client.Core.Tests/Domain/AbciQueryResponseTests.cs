using CometBFT.Client.Core.Domain;
using Xunit;

namespace CometBFT.Client.Core.Tests.Domain;

public sealed class AbciQueryResponseTests
{
    // ── AbciQueryResponse ────────────────────────────────────────────────────

    [Fact]
    public void AbciQueryResponse_SuccessResponse_PropertiesRoundTrip()
    {
        byte[] key = [0x01, 0x02];
        byte[] value = [0xAA, 0xBB];
        var response = new AbciQueryResponse(
            Code: 0,
            Log: "ok",
            Info: "info",
            Index: 42L,
            Key: key,
            Value: value,
            ProofOps: null,
            Height: 100L,
            Codespace: string.Empty);

        Assert.Equal(0u, response.Code);
        Assert.Equal("ok", response.Log);
        Assert.Equal("info", response.Info);
        Assert.Equal(42L, response.Index);
        Assert.Equal(key, response.Key);
        Assert.Equal(value, response.Value);
        Assert.Null(response.ProofOps);
        Assert.Equal(100L, response.Height);
        Assert.Equal(string.Empty, response.Codespace);
    }

    [Fact]
    public void AbciQueryResponse_ErrorResponse_HasNonZeroCodeAndCodespace()
    {
        var response = new AbciQueryResponse(
            Code: 2,
            Log: "unknown query path",
            Info: string.Empty,
            Index: 0L,
            Key: Array.Empty<byte>(),
            Value: Array.Empty<byte>(),
            ProofOps: null,
            Height: 0L,
            Codespace: "sdk");

        Assert.Equal(2u, response.Code);
        Assert.Equal("sdk", response.Codespace);
    }

    [Fact]
    public void AbciQueryResponse_WithProofOps_ProofOpsNotNull()
    {
        var op = new AbciProofOp("ics23:iavl", [0x01], [0x02, 0x03]);
        var proofOps = new AbciProofOps([op]);
        var response = new AbciQueryResponse(
            Code: 0,
            Log: string.Empty,
            Info: string.Empty,
            Index: 0L,
            Key: [0x01],
            Value: [0x02],
            ProofOps: proofOps,
            Height: 55L,
            Codespace: string.Empty);

        Assert.NotNull(response.ProofOps);
        Assert.Single(response.ProofOps.Ops);
    }

    [Fact]
    public void AbciQueryResponse_RecordEquality_SameValues_AreEqual()
    {
        byte[] key = [0x01];
        byte[] value = [0x02];
        var a = new AbciQueryResponse(0, "log", "info", 1L, key, value, null, 10L, string.Empty);
        var b = new AbciQueryResponse(0, "log", "info", 1L, key, value, null, 10L, string.Empty);

        Assert.Equal(a, b);
    }

    [Fact]
    public void AbciQueryResponse_RecordEquality_DifferentHeight_NotEqual()
    {
        byte[] key = [0x01];
        byte[] value = [0x02];
        var a = new AbciQueryResponse(0, "log", "info", 1L, key, value, null, 10L, string.Empty);
        var b = new AbciQueryResponse(0, "log", "info", 1L, key, value, null, 99L, string.Empty);

        Assert.NotEqual(a, b);
    }

    // ── AbciProofOps ────────────────────────────────────────────────────────

    [Fact]
    public void AbciProofOps_EmptyOps_OpsIsEmpty()
    {
        var proofOps = new AbciProofOps([]);

        Assert.Empty(proofOps.Ops);
    }

    [Fact]
    public void AbciProofOps_MultipleOps_CountMatches()
    {
        var ops = new List<AbciProofOp>
        {
            new("ics23:iavl", [0x01], [0xAA]),
            new("ics23:simple", [0x02], [0xBB, 0xCC]),
        };
        var proofOps = new AbciProofOps(ops);

        Assert.Equal(2, proofOps.Ops.Count);
    }

    [Fact]
    public void AbciProofOps_RecordEquality_SameOps_AreEqual()
    {
        var ops = new List<AbciProofOp> { new("t", [0x01], [0x02]) };
        var a = new AbciProofOps(ops);
        var b = new AbciProofOps(ops);

        Assert.Equal(a, b);
    }

    // ── AbciProofOp ─────────────────────────────────────────────────────────

    [Fact]
    public void AbciProofOp_Properties_RoundTrip()
    {
        byte[] key = [0x0A, 0x0B];
        byte[] data = [0xDE, 0xAD, 0xBE, 0xEF];
        var op = new AbciProofOp("ics23:iavl", key, data);

        Assert.Equal("ics23:iavl", op.Type);
        Assert.Equal(key, op.Key);
        Assert.Equal(data, op.Data);
    }

    [Fact]
    public void AbciProofOp_RecordEquality_SameValues_AreEqual()
    {
        byte[] key = [0x01];
        byte[] data = [0x02];
        var a = new AbciProofOp("ics23:iavl", key, data);
        var b = new AbciProofOp("ics23:iavl", key, data);

        Assert.Equal(a, b);
    }

    [Fact]
    public void AbciProofOp_RecordEquality_DifferentType_NotEqual()
    {
        byte[] key = [0x01];
        byte[] data = [0x02];
        var a = new AbciProofOp("ics23:iavl", key, data);
        var b = new AbciProofOp("ics23:simple", key, data);

        Assert.NotEqual(a, b);
    }
}
