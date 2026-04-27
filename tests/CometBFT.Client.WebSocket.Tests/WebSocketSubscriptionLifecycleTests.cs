using System.Linq;
using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// End-to-end subscription lifecycle matrix against <see cref="PassiveWebSocketServer"/>.
/// Shares the <see cref="WebSocketQueries"/> / <see cref="WebSocketEventTopic"/> surface
/// with production so no literal topic or method string is duplicated across layers.
/// </summary>
public sealed class WebSocketSubscriptionLifecycleTests
{
    // Localhost fixture → frames round-trip in < 20 ms. A tight ACK ceiling keeps the
    // suite honest: any missed ACK fails fast instead of hiding behind a generous delay.
    private static readonly TimeSpan AckTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(2);

    private static IOptions<CometBftWebSocketOptions> FastAckOptions(string url) =>
        Options.Create(new CometBftWebSocketOptions
        {
            BaseUrl = url,
            SubscribeAckTimeout = AckTimeout,
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        });

    /// <summary>Minimal Vote event envelope built from the typed topic — no literal wire strings.</summary>
    private static string BuildVoteEvent(long height) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            jsonrpc = WebSocketRpcMethods.JsonRpcVersion,
            id = 0,
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
                            validator_address = "VAL1",
                            timestamp = "2024-01-01T00:00:00Z",
                        },
                    },
                },
            },
        });

    [Fact]
    public async Task Subscribe_PushEvent_UnsubscribeAll_CompletesFullRoundTrip()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));

        var vote = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.VoteReceived += (_, args) => vote.TrySetResult(args.Value.Height);

        await client.ConnectAsync();
        await client.SubscribeVoteAsync();

        await server.PushAsync(BuildVoteEvent(height: 42));

        var height = await vote.Task.WaitAsync(WaitTimeout);
        Assert.Equal(42, height);

        await client.UnsubscribeAllAsync();
        await server.WaitForMessagesAsync(count: 2, WaitTimeout);

        Assert.Equal(1, server.CountSubscribes(WebSocketEventTopic.Vote));
        Assert.Equal(1, server.CountMethod(WebSocketRpcMethods.UnsubscribeAll));
    }

    [Fact]
    public async Task Subscribe_SameQueryTwice_SendsSingleFrame()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        await client.ConnectAsync();

        await client.SubscribeNewBlockAsync();
        await client.SubscribeNewBlockAsync();
        await server.WaitForMessagesAsync(count: 1, WaitTimeout);

        Assert.Equal(1, server.CountSubscribes(WebSocketEventTopic.NewBlock));
    }

    [Fact]
    public async Task UnsubscribeAll_Twice_SendsSingleFrame()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        await client.ConnectAsync();

        await client.SubscribeNewBlockAsync();
        await client.UnsubscribeAllAsync();
        await client.UnsubscribeAllAsync();
        await server.WaitForMessagesAsync(count: 2, WaitTimeout);

        Assert.Equal(1, server.CountSubscribes(WebSocketEventTopic.NewBlock));
        Assert.Equal(1, server.CountMethod(WebSocketRpcMethods.UnsubscribeAll));
    }

    [Fact]
    public async Task Subscribe_SameQueryInParallel_SendsSingleFrame_ThreadSafe()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        await client.ConnectAsync();

        const int concurrency = 16;
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => client.SubscribeNewBlockAsync())
            .ToArray();

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(ex);

        await server.WaitForMessagesAsync(count: 1, WaitTimeout);
        Assert.Equal(1, server.CountSubscribes(WebSocketEventTopic.NewBlock));
    }

    [Fact]
    public async Task Subscribe_AfterUnsubscribeAll_EmitsNewFrame()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        await client.ConnectAsync();

        await client.SubscribeNewBlockAsync();
        await client.UnsubscribeAllAsync();
        await client.SubscribeNewBlockAsync();
        await server.WaitForMessagesAsync(count: 3, WaitTimeout);

        Assert.Equal(2, server.CountSubscribes(WebSocketEventTopic.NewBlock));
        Assert.Equal(1, server.CountMethod(WebSocketRpcMethods.UnsubscribeAll));
    }

    [Fact]
    public async Task SubscribeConsensusInternal_CalledTwice_EachTopicEmitsSingleFrame()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        await client.ConnectAsync();

        await client.SubscribeConsensusInternalAsync();
        await client.SubscribeConsensusInternalAsync();
        await server.WaitForMessagesAsync(
            count: WebSocketQueries.ConsensusInternalTopics.Count, WaitTimeout);

        foreach (var topic in WebSocketQueries.ConsensusInternalTopics)
        {
            Assert.Equal(1, server.CountSubscribes(topic));
        }
    }

    [Fact]
    public async Task Subscribe_WhenServerRepliesWith429_SurfacesErrorAndRollsBackSubscription()
    {
        const int TooManyRequests = 429;
        const string TooManyRequestsMessage = "Too Many Requests";

        await using var server = new PassiveWebSocketServer(sendAck: true);
        server.SetAckPolicy((id, _, _) =>
            WebSocketServerReply.WithError(id, TooManyRequests, TooManyRequestsMessage));
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        Exception? captured = null;
        client.ErrorOccurred += (_, e) => captured = e.Value;

        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();

        // 429 must surface as a non-fatal ErrorOccurred carrying the server code + message.
        Assert.NotNull(captured);
        Assert.Contains(TooManyRequests.ToString(), captured!.Message, StringComparison.Ordinal);
        Assert.Contains(TooManyRequestsMessage, captured.Message, StringComparison.Ordinal);

        // The subscription must have been rolled back so a retry emits a fresh frame.
        await client.SubscribeNewBlockAsync();
        await server.WaitForMessagesAsync(count: 2, WaitTimeout);
        Assert.Equal(2, server.CountSubscribes(WebSocketEventTopic.NewBlock));
    }

    [Fact]
    public async Task Subscribe_WithAlreadyCancelledToken_PropagatesOperationCanceled()
    {
        await using var server = new PassiveWebSocketServer(sendAck: false);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        await client.ConnectAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.SubscribeNewBlockAsync(cts.Token));
    }

    [Fact]
    public async Task Subscribe_DistinctQueries_EachEmitsSingleFrame()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        await using var client = new CometBftWebSocketClient(FastAckOptions(server.Url));
        await client.ConnectAsync();

        WebSocketEventTopic[] topics =
        [
            WebSocketEventTopic.NewBlock,
            WebSocketEventTopic.NewBlockHeader,
            WebSocketEventTopic.Tx,
            WebSocketEventTopic.Vote,
            WebSocketEventTopic.ValidatorSetUpdates,
            WebSocketEventTopic.NewBlockEvents,
            WebSocketEventTopic.NewEvidence,
        ];

        await Task.WhenAll(
            client.SubscribeNewBlockAsync(),
            client.SubscribeNewBlockHeaderAsync(),
            client.SubscribeTxAsync(),
            client.SubscribeVoteAsync(),
            client.SubscribeValidatorSetUpdatesAsync(),
            client.SubscribeNewBlockEventsAsync(),
            client.SubscribeNewEvidenceAsync());
        await server.WaitForMessagesAsync(count: topics.Length, WaitTimeout);

        foreach (var topic in topics)
        {
            Assert.Equal(1, server.CountSubscribes(topic));
        }
    }
}
