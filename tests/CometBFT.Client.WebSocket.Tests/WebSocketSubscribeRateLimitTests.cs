using Microsoft.Extensions.Options;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Verifies that when a relay enforces <c>max_subscriptions_per_client</c>, rejected subscribes
/// surface via <see cref="CometBftWebSocketClient.ErrorOccurred"/> while the <c>Subscribe*Async</c>
/// tasks still complete normally (no exception propagated to the caller).
/// </summary>
public sealed class WebSocketSubscribeRateLimitTests
{
    private static readonly TimeSpan AckTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private static CometBftWebSocketOptions OptionsFor(string url) => new()
    {
        BaseUrl = url,
        SubscribeAckTimeout = AckTimeout,
        ReconnectTimeout = TimeSpan.FromSeconds(1),
        ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
    };

    /// <summary>
    /// When the relay rejects a subscribe with a JSON-RPC error, the corresponding
    /// <c>Subscribe*Async</c> task must still complete without throwing.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WhenRateLimitExceeded_TaskCompletesSuccessfully()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        server.SetAckPolicy((id, method, _) =>
            method == WebSocketRpcMethods.Subscribe
                ? WebSocketServerReply.WithError(id, -32603, "max_subscriptions_per_client 0 reached")
                : WebSocketServerReply.Ok(id));

        await using var client = new CometBftWebSocketClient(Options.Create(OptionsFor(server.Url)));
        await client.ConnectAsync();

        var ex = await Record.ExceptionAsync(() => client.SubscribeNewBlockAsync());
        Assert.Null(ex);
    }

    /// <summary>
    /// A relay rejection must surface via <see cref="CometBftWebSocketClient.ErrorOccurred"/>
    /// so callers can observe it without needing try/catch around <c>Subscribe*Async</c>.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WhenRateLimitExceeded_ErrorOccurredFired()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        server.SetAckPolicy((id, method, _) =>
            method == WebSocketRpcMethods.Subscribe
                ? WebSocketServerReply.WithError(id, -32603, "max_subscriptions_per_client 0 reached")
                : WebSocketServerReply.Ok(id));

        await using var client = new CometBftWebSocketClient(Options.Create(OptionsFor(server.Url)));
        var errorSignal = new SemaphoreSlim(0, 1);
        Exception? capturedError = null;
        client.ErrorOccurred += (_, args) =>
        {
            capturedError = args.Value;
            errorSignal.Release();
        };

        await client.ConnectAsync();
        await client.SubscribeNewBlockAsync();

        var signaled = await errorSignal.WaitAsync(WaitTimeout);
        Assert.True(signaled, "ErrorOccurred was not fired within the timeout.");
        Assert.NotNull(capturedError);
        Assert.Contains("32603", capturedError!.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Simulates a standard node with <c>max_subscriptions_per_client = 5</c> while the
    /// dashboard bursts 7 topics. Exactly 2 <c>ErrorOccurred</c> events must fire;
    /// all 7 subscribe tasks must complete without throwing.
    /// </summary>
    [Fact]
    public async Task SubscribeBurstOf7_WithLimit5_TwoTopicsRejectedViaErrorOccurred()
    {
        await using var server = new PassiveWebSocketServer(sendAck: true);
        await server.StartAsync();

        var subCount = 0;
        server.SetAckPolicy((id, method, _) =>
        {
            if (method != WebSocketRpcMethods.Subscribe)
                return WebSocketServerReply.Ok(id);

            return Interlocked.Increment(ref subCount) <= 5
                ? WebSocketServerReply.Ok(id)
                : WebSocketServerReply.WithError(id, -32603, "max_subscriptions_per_client 5 reached");
        });

        await using var client = new CometBftWebSocketClient(Options.Create(OptionsFor(server.Url)));
        var errorCount = 0;
        var errorSignal = new SemaphoreSlim(0, int.MaxValue);
        client.ErrorOccurred += (_, _) =>
        {
            Interlocked.Increment(ref errorCount);
            errorSignal.Release();
        };

        await client.ConnectAsync();

        // Burst all 7 subscribes concurrently — same as DashboardBackgroundService does.
        await Task.WhenAll(
            client.SubscribeNewBlockAsync(),
            client.SubscribeNewBlockHeaderAsync(),
            client.SubscribeTxAsync(),
            client.SubscribeVoteAsync(),
            client.SubscribeValidatorSetUpdatesAsync(),
            client.SubscribeNewBlockEventsAsync(),
            client.SubscribeNewEvidenceAsync());

        // Wait for both rejection ErrorOccurred events.
        for (var i = 0; i < 2; i++)
        {
            var signaled = await errorSignal.WaitAsync(WaitTimeout);
            Assert.True(signaled, $"ErrorOccurred #{i + 1} was not fired within the timeout.");
        }

        Assert.Equal(2, errorCount);
    }
}
