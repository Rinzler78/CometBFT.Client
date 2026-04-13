using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket.Internal;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Unit tests for <see cref="DefaultWebSocketClientFactory"/>.
/// Covers lines 9-13 (100% of the file, 0% → 90%+).
/// </summary>
public sealed class DefaultWebSocketClientFactoryTests
{
    [Fact]
    public void Create_ReturnsWebsocketClient_WithOptionsApplied()
    {
        // Arrange
        var factory = new DefaultWebSocketClientFactory();
        var uri = new Uri("ws://localhost:26657/websocket");
        var options = new CometBftWebSocketOptions
        {
            ReconnectTimeout = TimeSpan.FromSeconds(45),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(15),
        };

        // Act — covers lines 8-13: new WebsocketClient(uri) { … }
        var client = factory.Create(uri, options);

        try
        {
            // Assert — properties are set from options
            Assert.Equal(TimeSpan.FromSeconds(45), client.ReconnectTimeout);
            Assert.Equal(TimeSpan.FromSeconds(15), client.ErrorReconnectTimeout);
        }
        finally
        {
            client.Dispose();
        }
    }
}
