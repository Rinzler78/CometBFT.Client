using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using CometBFT.Client.Grpc.Proto;
using CometBFT.Client.Grpc.Proto.CosmosBase.Tendermint.V1beta1;
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

    // ── cosmos.base.tendermint.v1beta1 ───────────────────────────────────────

    [Fact]
    public void GetNodeInfoRequest_Roundtrip()
    {
        AssertRoundtrip(new GetNodeInfoRequest());
    }

    [Fact]
    public void GetNodeInfoResponse_Roundtrip()
    {
        var msg = new GetNodeInfoResponse
        {
            DefaultNodeInfo = new DefaultNodeInfo
            {
                DefaultNodeId = "abc",
                Network = "mychain",
                Moniker = "node1",
                Version = "0.38.9",
            }
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void DefaultNodeInfo_Roundtrip()
    {
        var msg = new DefaultNodeInfo
        {
            DefaultNodeId = "nodeid",
            Network = "cosmoshub-4",
            Version = "0.38.9",
            ListenAddr = "tcp://0.0.0.0:26656",
            Moniker = "mynode",
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void NodeProtocolVersion_Roundtrip()
    {
        var msg = new NodeProtocolVersion { P2P = 8, Block = 11, App = 1 };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void VersionInfo_Roundtrip()
    {
        var msg = new VersionInfo { Version = "0.38.9" };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void GetSyncingRequest_Roundtrip()
    {
        AssertRoundtrip(new GetSyncingRequest());
    }

    [Fact]
    public void GetSyncingResponse_Roundtrip()
    {
        AssertRoundtrip(new GetSyncingResponse { Syncing = true });
        AssertRoundtrip(new GetSyncingResponse { Syncing = false });
    }

    [Fact]
    public void GetLatestBlockRequest_Roundtrip()
    {
        AssertRoundtrip(new GetLatestBlockRequest());
    }

    [Fact]
    public void GetBlockByHeightRequest_Roundtrip()
    {
        AssertRoundtrip(new GetBlockByHeightRequest { Height = 42 });
    }

    [Fact]
    public void GetLatestBlockResponse_Roundtrip()
    {
        var msg = new GetLatestBlockResponse
        {
            Block = new Block
            {
                Header = new Header { Height = 10 },
                Data = new Data()
            }
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void GetBlockByHeightResponse_Roundtrip()
    {
        var msg = new GetBlockByHeightResponse
        {
            Block = new Block
            {
                Header = new Header { Height = 100 },
                Data = new Data()
            }
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void Block_Roundtrip()
    {
        var msg = new Block
        {
            Header = new Header
            {
                Height = 77,
                Time = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                ProposerAddress = ByteString.CopyFrom([0xAA, 0xBB]),
            },
            Data = new Data()
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void Header_Roundtrip()
    {
        var msg = new Header
        {
            Height = 50,
            ProposerAddress = ByteString.CopyFrom([0x01, 0x02]),
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void Data_Roundtrip()
    {
        var msg = new Data();
        msg.Txs.Add(ByteString.CopyFrom([0xDE, 0xAD]));
        AssertRoundtrip(msg);
    }

    [Fact]
    public void GetLatestValidatorSetRequest_Roundtrip()
    {
        AssertRoundtrip(new GetLatestValidatorSetRequest
        {
            Pagination = new PageRequest { Limit = 100 }
        });
    }

    [Fact]
    public void GetLatestValidatorSetResponse_Roundtrip()
    {
        var msg = new GetLatestValidatorSetResponse();
        msg.Validators.Add(new Validator { Address = "val1", VotingPower = 500 });
        AssertRoundtrip(msg);
    }

    [Fact]
    public void GetValidatorSetByHeightRequest_Roundtrip()
    {
        AssertRoundtrip(new GetValidatorSetByHeightRequest { Height = 20 });
    }

    [Fact]
    public void GetValidatorSetByHeightResponse_Roundtrip()
    {
        var msg = new GetValidatorSetByHeightResponse();
        msg.Validators.Add(new Validator { Address = "val2", VotingPower = 1000 });
        AssertRoundtrip(msg);
    }

    [Fact]
    public void Validator_Roundtrip()
    {
        var msg = new Validator { Address = "val1", VotingPower = 300, ProposerPriority = -100 };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void ABCIQueryRequest_Roundtrip()
    {
        var msg = new ABCIQueryRequest
        {
            Path = "/app/version",
            Data = ByteString.CopyFrom([0x01, 0x02]),
            Height = 100,
            Prove = true
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void ABCIQueryResponse_Roundtrip()
    {
        var msg = new ABCIQueryResponse
        {
            Code = 0,
            Log = "ok",
            Value = ByteString.CopyFrom([0xDE, 0xAD]),
            Height = 55,
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void ProofOps_Roundtrip()
    {
        var msg = new ProofOps();
        msg.Ops.Add(new ProofOp
        {
            Type = "ics23:iavl",
            Key = ByteString.CopyFrom([0x01]),
            Data = ByteString.CopyFrom([0x02, 0x03])
        });
        AssertRoundtrip(msg);
    }

    [Fact]
    public void ProofOp_Roundtrip()
    {
        var msg = new ProofOp
        {
            Type = "ics23:iavl",
            Key = ByteString.CopyFrom([0x0A]),
            Data = ByteString.CopyFrom([0xBB])
        };
        AssertRoundtrip(msg);
    }

    [Fact]
    public void PageRequest_Roundtrip()
    {
        AssertRoundtrip(new PageRequest { Limit = 50, Offset = 0 });
    }

    // ── Descriptor access triggers reflection static init ────────────────────

    [Fact]
    public void QueryReflection_Descriptor_IsNotNull()
    {
        var descriptor = ABCIQueryRequest.Descriptor;
        Assert.NotNull(descriptor);
    }

    // ── cometbft.rpc.grpc (CometBFT proto) ──────────────────────────────────

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
