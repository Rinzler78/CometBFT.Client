namespace CometBFT.Client.Demo.Dashboard.ViewModels;

public sealed record BlockRow(long Height, string Time, int TxCount, string Proposer);
public sealed record TxRow(string Hash, long Height, uint Code, string Log);
public sealed record ValidatorRow(string Address, long VotingPower, long ProposerPriority);
public sealed record EventLogRow(string Timestamp, string Description);
