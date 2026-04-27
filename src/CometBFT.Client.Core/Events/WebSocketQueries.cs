namespace CometBFT.Client.Core.Events;

/// <summary>
/// Builds subscribe queries (<c>tm.event='Name'</c>) and maps between envelope
/// type strings (<c>tendermint/event/Name</c>) and <see cref="WebSocketEventTopic"/>.
/// Single source of truth shared by the WebSocket client and its tests — no caller
/// duplicates the literal strings.
/// </summary>
public static class WebSocketQueries
{
    /// <summary>Prefix of every subscribe query. Paired with <see cref="QuerySuffix"/>.</summary>
    public const string QueryPrefix = "tm.event='";

    /// <summary>Suffix of every subscribe query. Paired with <see cref="QueryPrefix"/>.</summary>
    public const string QuerySuffix = "'";

    /// <summary>Prefix of the envelope <c>type</c> field in inbound event frames.</summary>
    public const string EnvelopeTypePrefix = "tendermint/event/";

    /// <summary>Returns the subscribe query for a topic: <c>tm.event='Name'</c>.</summary>
    public static string Of(WebSocketEventTopic topic) =>
        QueryPrefix + NameOf(topic) + QuerySuffix;

    /// <summary>Returns the envelope type string: <c>tendermint/event/Name</c>.</summary>
    public static string EnvelopeTypeOf(WebSocketEventTopic topic) =>
        EnvelopeTypePrefix + NameOf(topic);

    /// <summary>Canonical topic name (e.g. <c>NewBlock</c>).</summary>
    public static string NameOf(WebSocketEventTopic topic) => topic switch
    {
        WebSocketEventTopic.NewBlock => "NewBlock",
        WebSocketEventTopic.NewBlockHeader => "NewBlockHeader",
        WebSocketEventTopic.Tx => "Tx",
        WebSocketEventTopic.Vote => "Vote",
        WebSocketEventTopic.ValidatorSetUpdates => "ValidatorSetUpdates",
        WebSocketEventTopic.NewBlockEvents => "NewBlockEvents",
        WebSocketEventTopic.CompleteProposal => "CompleteProposal",
        WebSocketEventTopic.NewEvidence => "NewEvidence",
        WebSocketEventTopic.TimeoutPropose => "TimeoutPropose",
        WebSocketEventTopic.TimeoutWait => "TimeoutWait",
        WebSocketEventTopic.Lock => "Lock",
        WebSocketEventTopic.Unlock => "Unlock",
        WebSocketEventTopic.Relock => "Relock",
        WebSocketEventTopic.PolkaAny => "PolkaAny",
        WebSocketEventTopic.PolkaNil => "PolkaNil",
        WebSocketEventTopic.PolkaAgain => "PolkaAgain",
        WebSocketEventTopic.MissingProposalBlock => "MissingProposalBlock",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null),
    };

    /// <summary>The nine consensus-internal topics that merge into a single stream.</summary>
    public static readonly IReadOnlyList<WebSocketEventTopic> ConsensusInternalTopics =
    [
        WebSocketEventTopic.TimeoutPropose,
        WebSocketEventTopic.TimeoutWait,
        WebSocketEventTopic.Lock,
        WebSocketEventTopic.Unlock,
        WebSocketEventTopic.Relock,
        WebSocketEventTopic.PolkaAny,
        WebSocketEventTopic.PolkaNil,
        WebSocketEventTopic.PolkaAgain,
        WebSocketEventTopic.MissingProposalBlock,
    ];
}
