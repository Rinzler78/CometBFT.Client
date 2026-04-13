using CometBFT.Client.Core.Options;
using Websocket.Client;

namespace CometBFT.Client.WebSocket.Internal;

internal interface IWebSocketClientFactory
{
    WebsocketClient Create(Uri uri, CometBftWebSocketOptions options);
}
