using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using Websocket.Client;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Dispatch tests for the five new IObservable streams introduced in v2.1.0.
/// Tests call <see cref="CometBftWebSocketClient.OnMessageReceived"/> directly.
/// </summary>
public sealed class NewStreamDispatchTests
{
    private static CometBftWebSocketClient MakeClient() =>
        new(Options.Create(new CometBftWebSocketOptions()));

    // ── NewBlockEventsStream ─────────────────────────────────────────────────

    [Fact]
    public void OnMessageReceived_NewBlockEvents_EmitsOnNewBlockEventsStream()
    {
        var client = MakeClient();
        NewBlockEventsData? received = null;
        using var sub = client.NewBlockEventsStream.Subscribe(d => received = d);

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewBlockEvents",
              "value": {
                "height": "42",
                "header": {
                  "version": { "block": "11" },
                  "chain_id": "cosmoshub-4",
                  "height": "42",
                  "time": "2024-06-01T12:00:00+00:00",
                  "proposer_address": "PROP"
                },
                "events": [
                  {
                    "type": "ibc_transfer",
                    "attributes": [{ "key": "recipient", "value": "addr1", "index": true }]
                  }
                ]
              }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal(42L, received.Height);
        Assert.Equal("cosmoshub-4", received.Header.ChainId);
        Assert.Single(received.Events);
        Assert.Equal("ibc_transfer", received.Events[0].Type);
    }

    [Fact]
    public void NewBlockEventsStream_IsExposed()
    {
        var client = MakeClient();
        Assert.NotNull(client.NewBlockEventsStream);
    }

    // ── CompleteProposalStream ───────────────────────────────────────────────

    [Fact]
    public void OnMessageReceived_CompleteProposal_EmitsOnCompleteProposalStream()
    {
        var client = MakeClient();
        CompleteProposalData? received = null;
        using var sub = client.CompleteProposalStream.Subscribe(d => received = d);

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/CompleteProposal",
              "value": { "height": "100", "round": 2, "block_id": "BLOCK-100" }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal(100L, received.Height);
        Assert.Equal(2, received.Round);
        Assert.Equal("BLOCK-100", received.BlockId);
    }

    // ── ValidatorSetUpdatesStream ────────────────────────────────────────────

    [Fact]
    public void OnMessageReceived_ValidatorSetUpdates_EmitsOnValidatorSetUpdatesStream()
    {
        var client = MakeClient();
        ValidatorSetUpdatesData? received = null;
        using var sub = client.ValidatorSetUpdatesStream.Subscribe(d => received = d);

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/ValidatorSetUpdates",
              "value": {
                "validator_updates": [
                  { "address": "VAL1", "pub_key": { "data": "KEY1" }, "power": "1000" }
                ]
              }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Single(received.ValidatorUpdates);
        Assert.Equal("VAL1", received.ValidatorUpdates[0].Address);
    }

    [Fact]
    public void OnMessageReceived_ValidatorSetUpdates_StillFiresLegacyEvent()
    {
        var client = MakeClient();
        IReadOnlyList<Validator>? legacyReceived = null;
        client.ValidatorSetUpdated += (_, e) => legacyReceived = e.Value;

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/ValidatorSetUpdates",
              "value": {
                "validator_updates": [
                  { "address": "VAL1", "pub_key": { "data": "KEY1" }, "power": "500" }
                ]
              }
            }
          }
        }
        """));

        Assert.NotNull(legacyReceived);
        Assert.Single(legacyReceived);
    }

    // ── NewEvidenceStream ────────────────────────────────────────────────────

    [Fact]
    public void OnMessageReceived_NewEvidence_EmitsOnNewEvidenceStream()
    {
        var client = MakeClient();
        NewEvidenceData? received = null;
        using var sub = client.NewEvidenceStream.Subscribe(d => received = d);

        client.OnMessageReceived(ResponseMessage.TextMessage("""
        {
          "result": {
            "data": {
              "type": "tendermint/event/NewEvidence",
              "value": {
                "height": "77",
                "evidence_type": "DuplicateVoteEvidence",
                "validator": "VALADDR77"
              }
            }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal(77L, received.Height);
        Assert.Equal("DuplicateVoteEvidence", received.EvidenceType);
        Assert.Equal("VALADDR77", received.Validator);
    }

    // ── ConsensusInternalStream ──────────────────────────────────────────────

    [Theory]
    [InlineData("tendermint/event/TimeoutPropose", "TimeoutPropose")]
    [InlineData("tendermint/event/TimeoutWait", "TimeoutWait")]
    [InlineData("tendermint/event/Lock", "Lock")]
    [InlineData("tendermint/event/Unlock", "Unlock")]
    [InlineData("tendermint/event/Relock", "Relock")]
    [InlineData("tendermint/event/PolkaAny", "PolkaAny")]
    [InlineData("tendermint/event/PolkaNil", "PolkaNil")]
    [InlineData("tendermint/event/PolkaAgain", "PolkaAgain")]
    [InlineData("tendermint/event/MissingProposalBlock", "MissingProposalBlock")]
    public void OnMessageReceived_ConsensusInternalTopic_EmitsWithCorrectType(
        string wireType, string expectedTopic)
    {
        var client = MakeClient();
        CometBftEvent? received = null;
        using var sub = client.ConsensusInternalStream.Subscribe(e => received = e);

        client.OnMessageReceived(ResponseMessage.TextMessage($$"""
        {
          "result": {
            "data": { "type": "{{wireType}}", "value": {} }
          }
        }
        """));

        Assert.NotNull(received);
        Assert.Equal(expectedTopic, received.Type);
        Assert.Empty(received.Attributes);
    }

    // ── Subscribe methods without connection ─────────────────────────────────

    [Fact]
    public async Task SubscribeNewBlockEventsAsync_WithoutConnection_Throws()
    {
        var client = MakeClient();
        await Assert.ThrowsAsync<Core.Exceptions.CometBftWebSocketException>(
            () => client.SubscribeNewBlockEventsAsync());
    }

    [Fact]
    public async Task SubscribeCompleteProposalAsync_WithoutConnection_Throws()
    {
        var client = MakeClient();
        await Assert.ThrowsAsync<Core.Exceptions.CometBftWebSocketException>(
            () => client.SubscribeCompleteProposalAsync());
    }

    [Fact]
    public async Task SubscribeNewEvidenceAsync_WithoutConnection_Throws()
    {
        var client = MakeClient();
        await Assert.ThrowsAsync<Core.Exceptions.CometBftWebSocketException>(
            () => client.SubscribeNewEvidenceAsync());
    }

    [Fact]
    public async Task SubscribeConsensusInternalAsync_WithoutConnection_Throws()
    {
        var client = MakeClient();
        await Assert.ThrowsAsync<Core.Exceptions.CometBftWebSocketException>(
            () => client.SubscribeConsensusInternalAsync());
    }
}
