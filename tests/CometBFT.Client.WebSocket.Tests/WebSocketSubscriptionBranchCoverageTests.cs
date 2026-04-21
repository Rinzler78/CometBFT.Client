using Microsoft.Extensions.Options;
using CometBFT.Client.WebSocket;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

public sealed class WebSocketSubscriptionBranchCoverageTests
{
    [Fact]
    public async Task SubscribeNewBlockAsync_WhenTimeoutOccursWithoutErrorSubscriber_DoesNotThrow()
    {
        await using var server = new PassiveWebSocketServer(sendAck: false);
        await server.StartAsync();
        await using var client = new CometBftWebSocketClient(Options.Create(new Core.Options.CometBftWebSocketOptions
        {
            BaseUrl = server.Url,
            SubscribeAckTimeout = TimeSpan.FromMilliseconds(100),
            ReconnectTimeout = TimeSpan.FromSeconds(1),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(1),
        }));

        await client.ConnectAsync();
        var ex = await Record.ExceptionAsync(() => client.SubscribeNewBlockAsync());
        Assert.Null(ex);
    }
}
