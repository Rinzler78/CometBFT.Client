using System.Text.Json;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using Websocket.Client;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class WebSocketAckTimeoutInteropTests
{
    private const string ExpectedErrorKeyword = "ACK";
    private static readonly TimeSpan AckTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task OnMessageReceived_WhenAckTimesOutAndVoteArrivesLater_StillPublishesVote()
    {
        await using var server = new PassiveWebSocketServer(sendAck: false);
        await server.StartAsync();

        var options = Options.Create(new CometBftWebSocketOptions
        {
            BaseUrl = server.Url,
            SubscribeAckTimeout = AckTimeout,
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        });

        await using var client = new CometBftWebSocketClient(options);
        var voteReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? capturedError = null;

        client.ErrorOccurred += (_, args) => capturedError = args.Value;
        client.VoteReceived += (_, args) =>
        {
            if (args.Value.Height > 0 && args.Value.ValidatorAddress.Length > 0)
            {
                voteReceived.TrySetResult(true);
            }
        };

        await client.ConnectAsync();
        await client.SubscribeVoteAsync();

        client.OnMessageReceived(ResponseMessage.TextMessage(BuildLateVoteFrame(height: 42)));

        await voteReceived.Task.WaitAsync(WaitTimeout);

        Assert.NotNull(capturedError);
        Assert.Contains(ExpectedErrorKeyword, capturedError!.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds a minimal Vote event envelope without any hardcoded wire strings.</summary>
    private static string BuildLateVoteFrame(long height) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = WebSocketRpcMethods.JsonRpcVersion,
            id = 1,
            result = new
            {
                data = new
                {
                    type = WebSocketQueries.EnvelopeTypeOf(WebSocketEventTopic.Vote),
                    value = new
                    {
                        Vote = new
                        {
                            type = 2,
                            height = height.ToString(),
                            round = 0,
                            validator_address = "VALADDR1",
                            timestamp = "2024-06-01T12:00:00Z",
                        },
                    },
                },
            },
        });
}
