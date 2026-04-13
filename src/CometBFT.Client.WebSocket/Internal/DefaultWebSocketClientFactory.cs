using CometBFT.Client.Core.Options;
using Websocket.Client;

namespace CometBFT.Client.WebSocket.Internal;

internal sealed class DefaultWebSocketClientFactory : IWebSocketClientFactory
{
    public WebsocketClient Create(Uri uri, CometBftWebSocketOptions options) =>
        new(uri)
        {
            ReconnectTimeout = options.ReconnectTimeout,
            ErrorReconnectTimeout = options.ErrorReconnectTimeout,
        };
}
