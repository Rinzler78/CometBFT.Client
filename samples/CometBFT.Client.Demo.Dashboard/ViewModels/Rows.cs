namespace CometBFT.Client.Demo.Dashboard.ViewModels;

public sealed record BlockRow(long Height, string Time, int TxCount, string Proposer);

public sealed record TxRow(string Hash, long Height, uint Code, string Log)
{
    /// <summary>Human-readable status text rendered in the badge.</summary>
    public string StatusText => Code == 0 ? "OK" : "ERR";

    /// <summary>True when the transaction succeeded (code 0).</summary>
    public bool IsSuccess => Code == 0;
}

public sealed record ValidatorRow(
    int Rank,
    string Address,
    long VotingPower,
    long ProposerPriority,
    double VotingPowerPct);

public sealed record EventLogRow(string Timestamp, string Category, string Description);
