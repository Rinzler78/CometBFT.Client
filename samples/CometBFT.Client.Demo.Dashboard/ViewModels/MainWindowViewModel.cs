using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CometBFT.Client.Demo.Dashboard.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    // ── Connection ────────────────────────────────────────────────────────────

    [ObservableProperty] private string _connectionStatus = "Connecting…";
    [ObservableProperty] private bool _isConnected;

    // ── Node info (gRPC GetStatusAsync) ───────────────────────────────────────

    [ObservableProperty] private string _chainId = "—";
    [ObservableProperty] private string _moniker = "—";
    [ObservableProperty] private string _nodeId = "—";
    [ObservableProperty] private string _nodeVersion = "—";
    [ObservableProperty] private bool _isSyncing;

    // ── Latest block (gRPC GetLatestBlockAsync on NewBlock) ───────────────────

    [ObservableProperty] private long _latestHeight;
    [ObservableProperty] private string _latestBlockTime = "—";
    [ObservableProperty] private int _latestBlockTxCount;

    // ── Network (REST GetNetInfoAsync — periodic 30s) ─────────────────────────

    [ObservableProperty] private int _peerCount;

    // ── Mempool (REST GetUnconfirmedTxsAsync on TxExecuted) ───────────────────

    [ObservableProperty] private int _pendingTxCount;
    [ObservableProperty] private int _pendingTxBytes;

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<BlockRow> Blocks { get; } = [];
    public ObservableCollection<TxRow> Transactions { get; } = [];
    public ObservableCollection<ValidatorRow> Validators { get; } = [];
    public ObservableCollection<EventLogRow> EventLog { get; } = [];
}
