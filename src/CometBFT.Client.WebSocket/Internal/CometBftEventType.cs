namespace CometBFT.Client.WebSocket.Internal;

/// <summary>
/// Canonical event type strings emitted by CometBFT WebSocket subscriptions.
/// </summary>
internal static class CometBftEventType
{
    internal const string NewBlock = "tendermint/event/NewBlock";
    internal const string NewBlockHeader = "tendermint/event/NewBlockHeader";
    internal const string Tx = "tendermint/event/Tx";
    internal const string Vote = "tendermint/event/Vote";
    internal const string ValidatorSetUpdates = "tendermint/event/ValidatorSetUpdates";
}
