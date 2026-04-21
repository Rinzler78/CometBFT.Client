using Google.Protobuf;
using CometBFT.Client.Grpc.Proto;
using Xunit;
using LegacyProto = Tendermint.Client.Grpc.LegacyProto;

namespace CometBFT.Client.Grpc.Tests.Proto;

/// <summary>
/// Roundtrip serialization tests for all proto-generated message classes.
/// Covers property setters, <c>WriteTo</c>/<c>CalculateSize</c> (via <c>ToByteArray</c>),
/// <c>MergeFrom</c> (via <c>Parser.ParseFrom</c>), <c>Equals</c>, and <c>Clone</c>.
/// </summary>
public sealed class ProtoMessageRoundtripTests
{
    private static void AssertRoundtrip<T>(T original) where T : IMessage<T>
    {
        var bytes = original.ToByteArray();
        var parsed = original.Descriptor.Parser.ParseFrom(bytes);
        Assert.Equal(original, (T)parsed);
        Assert.Equal(original, (T)original.Clone());
    }

    // ── cometbft.rpc.grpc (CometBFT native proto) ───────────────────────────

    [Fact]
    public void RequestPing_Roundtrip()
    {
        AssertRoundtrip(new RequestPing());
    }

    [Fact]
    public void ResponsePing_Roundtrip()
    {
        AssertRoundtrip(new ResponsePing());
    }

    [Fact]
    public void RequestBroadcastTx_Roundtrip()
    {
        var msg = new RequestBroadcastTx { Tx = ByteString.CopyFrom([0xDE, 0xAD, 0xBE, 0xEF]) };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void ResponseBroadcastTx_Roundtrip()
    {
        var msg = new ResponseBroadcastTx
        {
            CheckTx = new ResponseCheckTx { Code = 0, GasWanted = 100, GasUsed = 80 }
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void ResponseCheckTx_Roundtrip()
    {
        var msg = new ResponseCheckTx { Code = 0, Log = "ok", GasWanted = 200, GasUsed = 150 };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void GrpcReflection_Descriptor_IsNotNull()
    {
        var descriptor = RequestPing.Descriptor;
        Assert.NotNull(descriptor);
    }

    // ── tendermint.rpc.grpc (Legacy Tendermint proto) ────────────────────────

    [Fact]
    public void LegacyRequestPing_Roundtrip()
    {
        AssertRoundtrip(new LegacyProto.RequestPing());
    }

    [Fact]
    public void LegacyRequestBroadcastTx_Roundtrip()
    {
        var msg = new LegacyProto.RequestBroadcastTx { Tx = ByteString.CopyFrom([0x01, 0x02]) };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void LegacyResponseBroadcastTx_Roundtrip()
    {
        var msg = new LegacyProto.ResponseBroadcastTx
        {
            CheckTx = new LegacyProto.ResponseCheckTx { Code = 0, GasWanted = 100 }
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void LegacyResponseCheckTx_Roundtrip()
    {
        var msg = new LegacyProto.ResponseCheckTx { Code = 0, Log = "ok", GasWanted = 100, GasUsed = 80 };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void LegacyGrpcReflection_Descriptor_IsNotNull()
    {
        var descriptor = LegacyProto.RequestPing.Descriptor;
        Assert.NotNull(descriptor);
    }
}
