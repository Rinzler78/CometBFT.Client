namespace CometBFT.Client.Core.Events;

/// <summary>
/// CometBFT WebSocket event topics, one per <c>tm.event</c> filter value defined in
/// <c>types/events.go</c>. The <see cref="WebSocketQueries"/> helper converts these
/// into subscribe-query strings (<c>tm.event='NewBlock'</c>) without the caller
/// having to repeat the literal anywhere.
/// </summary>
public enum WebSocketEventTopic
{
    /// <summary>A block has been committed. Payload: full block with txs.</summary>
    NewBlock,

    /// <summary>A block header has been committed. Payload: header only.</summary>
    NewBlockHeader,

    /// <summary>A transaction has been executed. Payload: <c>TxResult</c>.</summary>
    Tx,

    /// <summary>A consensus vote was cast.</summary>
    Vote,

    /// <summary>The validator set has changed.</summary>
    ValidatorSetUpdates,

    /// <summary>Block committed with the full ABCI event list attached.</summary>
    NewBlockEvents,

    /// <summary>Consensus: a proposal is complete for a given round.</summary>
    CompleteProposal,

    /// <summary>New Byzantine evidence was submitted.</summary>
    NewEvidence,

    /// <summary>Consensus-internal: propose step timed out.</summary>
    TimeoutPropose,

    /// <summary>Consensus-internal: wait step timed out.</summary>
    TimeoutWait,

    /// <summary>Consensus-internal: a block was locked.</summary>
    Lock,

    /// <summary>Consensus-internal: a block was unlocked.</summary>
    Unlock,

    /// <summary>Consensus-internal: a block was relocked.</summary>
    Relock,

    /// <summary>Consensus-internal: polka on any block.</summary>
    PolkaAny,

    /// <summary>Consensus-internal: polka on nil.</summary>
    PolkaNil,

    /// <summary>Consensus-internal: repeated polka.</summary>
    PolkaAgain,

    /// <summary>Consensus-internal: proposal block missing.</summary>
    MissingProposalBlock,
}
