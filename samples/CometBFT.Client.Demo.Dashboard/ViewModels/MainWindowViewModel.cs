using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CometBFT.Client.Core.Domain;

namespace CometBFT.Client.Demo.Dashboard.ViewModels;

/// <summary>
/// Primary dashboard ViewModel. Owns all observable state and the thread-marshaling
/// to the Avalonia UI thread; consumers (BackgroundService) call domain-oriented
/// methods and never touch <see cref="Dispatcher"/> or the row record types directly.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const int MaxBlocks = 50;
    private const int MaxTxs = 50;
    private const int MaxEventLog = 100;
    private const int AddressDisplayLength = 16;
    private const int ProposerDisplayLength = 12;

    // ── Connection ────────────────────────────────────────────────────────────

    [ObservableProperty] private string _connectionStatus = "Connecting…";
    [ObservableProperty] private bool _isConnected;

    // ── Node info (gRPC GetStatusAsync) ───────────────────────────────────────

    [ObservableProperty] private string _chainId = "—";
    [ObservableProperty] private string _moniker = "—";
    [ObservableProperty] private string _nodeId = "—";
    [ObservableProperty] private string _nodeVersion = "—";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SyncStatusText))]
    private bool _isSyncing;

    /// <summary>Human-readable sync status shown as a badge.</summary>
    public string SyncStatusText => IsSyncing ? "Syncing…" : "Synced";

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

    // ── State-change methods (MVVM surface for BackgroundService) ─────────────

    /// <summary>Updates connection indicators. Safe to call from any thread.</summary>
    public void SetConnectionStatus(string status, bool isConnected) =>
        Post(() =>
        {
            ConnectionStatus = status;
            IsConnected = isConnected;
        });

    /// <summary>Appends a line to the event log (timestamp added). Safe from any thread.</summary>
    public void AppendEventLog(string category, string description)
    {
        var row = new EventLogRow(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"), category, description);
        Post(() =>
        {
            EventLog.Insert(0, row);
            while (EventLog.Count > MaxEventLog)
            {
                EventLog.RemoveAt(EventLog.Count - 1);
            }
        });
    }

    /// <summary>
    /// Records a freshly-committed block from the WebSocket stream. No-ops if the
    /// same height is already at the head (duplicate event guard).
    /// </summary>
    public void OnNewBlock(Block<string> block)
    {
        var row = new BlockRow(
            block.Height,
            block.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            block.Txs.Count,
            Abbreviate(block.Proposer, ProposerDisplayLength));

        Post(() =>
        {
            if (Blocks.Count > 0 && Blocks[0].Height == block.Height)
            {
                return;
            }

            Blocks.Insert(0, row);
            LatestHeight = block.Height;
            LatestBlockTime = block.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            LatestBlockTxCount = block.Txs.Count;
            while (Blocks.Count > MaxBlocks)
            {
                Blocks.RemoveAt(Blocks.Count - 1);
            }
        });
    }

    /// <summary>Records an executed transaction from the WebSocket stream.</summary>
    public void OnTransaction(TxResult<string> tx)
    {
        var row = new TxRow(
            Abbreviate(tx.Hash, AddressDisplayLength),
            tx.Height,
            tx.Code,
            tx.Log ?? string.Empty);

        Post(() =>
        {
            Transactions.Insert(0, row);
            while (Transactions.Count > MaxTxs)
            {
                Transactions.RemoveAt(Transactions.Count - 1);
            }
        });
    }

    /// <summary>Refreshes mempool gauges after a REST <c>num_unconfirmed_txs</c> call.</summary>
    public void UpdateMempool(UnconfirmedTxsInfo info) =>
        Post(() =>
        {
            PendingTxCount = info.Total;
            PendingTxBytes = info.TotalBytes;
        });

    /// <summary>Applies a REST <c>/status</c> response to node-info + sync fields.</summary>
    public void UpdateNodeStatus(NodeInfo nodeInfo, SyncInfo syncInfo) =>
        Post(() =>
        {
            ChainId = nodeInfo.Network;
            Moniker = nodeInfo.Moniker;
            NodeId = Abbreviate(nodeInfo.Id, AddressDisplayLength);
            NodeVersion = nodeInfo.Version;
            IsSyncing = syncInfo.CatchingUp;
        });

    /// <summary>Replaces the validator list with a fresh, ranked snapshot.</summary>
    public void UpdateValidators(IReadOnlyList<Validator> validators)
    {
        var sorted = validators.OrderByDescending(v => v.VotingPower).ToList();
        var totalPower = sorted.Sum(v => (double)v.VotingPower);
        var rows = sorted.Select((v, i) => new ValidatorRow(
            i + 1,
            Abbreviate(v.Address, AddressDisplayLength),
            v.VotingPower,
            v.ProposerPriority,
            totalPower > 0 ? Math.Round(v.VotingPower / totalPower * 100, 1) : 0)).ToList();

        Post(() =>
        {
            Validators.Clear();
            foreach (var row in rows)
            {
                Validators.Add(row);
            }
        });
    }

    /// <summary>Applies the peer count from a REST <c>net_info</c> response.</summary>
    public void UpdatePeerCount(int peerCount) => Post(() => PeerCount = peerCount);

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static string Abbreviate(string value, int max) =>
        value.Length <= max ? value : string.Concat(value.AsSpan(0, max), "…");

    private static void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
