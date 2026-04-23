namespace CometBFT.Client.Core.Events;

/// <summary>
/// JSON-RPC method names and protocol constants accepted by CometBFT's WebSocket RPC.
/// Shared between the wire serializer and tests so every literal lives in one place.
/// </summary>
public static class WebSocketRpcMethods
{
    /// <summary>Protocol version string used in every JSON-RPC envelope.</summary>
    public const string JsonRpcVersion = "2.0";

    /// <summary>Subscribe request method. Params: <c>{"query": "..."}</c>.</summary>
    public const string Subscribe = "subscribe";

    /// <summary>Unsubscribe-all request method. Params: <c>{}</c>.</summary>
    public const string UnsubscribeAll = "unsubscribe_all";
}
