using Avalonia.Threading;
using Microsoft.Extensions.Hosting;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Demo.Dashboard.ViewModels;

namespace CometBFT.Client.Demo.Dashboard.Services;

internal sealed class DashboardBackgroundService : BackgroundService
{
    private const int MaxBlocks = 50;
    private const int MaxTxs = 50;
    private const int MaxEventLog = 100;

    private readonly ICometBftWebSocketClient _ws;
    private readonly ICometBftRestClient _rest;
    private readonly ICometBftSdkGrpcClient _grpc;
    private readonly MainWindowViewModel _vm;

    public DashboardBackgroundService(
        ICometBftWebSocketClient ws,
        ICometBftRestClient rest,
        ICometBftSdkGrpcClient grpc,
        MainWindowViewModel vm)
    {
        _ws = ws;
        _rest = rest;
        _grpc = grpc;
        _vm = vm;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ws.NewBlockReceived += OnNewBlock;
        _ws.NewBlockHeaderReceived += OnNewBlockHeader;
        _ws.TxExecuted += OnTxExecuted;
        _ws.VoteReceived += OnVote;
        _ws.ValidatorSetUpdated += OnValidatorSetUpdated;
        _ws.ErrorOccurred += OnError;

        try
        {
            await _ws.ConnectAsync(stoppingToken);
            _vm.ConnectionStatus = "Connected";
            _vm.IsConnected = true;

            await _ws.SubscribeNewBlockAsync(stoppingToken);
            await _ws.SubscribeNewBlockHeaderAsync(stoppingToken);
            await _ws.SubscribeTxAsync(stoppingToken);
            await _ws.SubscribeVoteAsync(stoppingToken);
            await _ws.SubscribeValidatorSetUpdatesAsync(stoppingToken);

            // Initial load
            await RefreshNodeInfoAsync(stoppingToken);
            await RefreshValidatorsAsync(stoppingToken);
            await RefreshNetInfoAsync(stoppingToken);

            // Periodic refresh every 30s for network info and node status
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshNodeInfoAsync(stoppingToken);
                await RefreshNetInfoAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _ws.NewBlockReceived -= OnNewBlock;
            _ws.NewBlockHeaderReceived -= OnNewBlockHeader;
            _ws.TxExecuted -= OnTxExecuted;
            _ws.VoteReceived -= OnVote;
            _ws.ValidatorSetUpdated -= OnValidatorSetUpdated;
            _ws.ErrorOccurred -= OnError;

            _vm.ConnectionStatus = "Disconnected";
            _vm.IsConnected = false;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnNewBlock(object? sender, CometBftEventArgs<Block<string>> e) =>
        _ = HandleNewBlockAsync(e.Value);

    private void OnNewBlockHeader(object? sender, CometBftEventArgs<BlockHeader> e) =>
        AppendEventLog($"Header #{e.Value.Height} — {e.Value.Time:HH:mm:ss}");

    private void OnTxExecuted(object? sender, CometBftEventArgs<TxResult<string>> e) =>
        _ = HandleTxAsync(e.Value);

    private void OnVote(object? sender, CometBftEventArgs<Vote> e) =>
        AppendEventLog($"Vote h={e.Value.Height} r={e.Value.Round} type={e.Value.Type}");

    private void OnValidatorSetUpdated(object? sender, CometBftEventArgs<IReadOnlyList<Validator>> e) =>
        _ = HandleValidatorSetUpdatedAsync();

    private void OnError(object? sender, CometBftEventArgs<Exception> e)
    {
        _vm.ConnectionStatus = $"Error: {e.Value.Message}";
        AppendEventLog($"[ERR] {e.Value.Message}");
    }

    // ── Async enrichment ──────────────────────────────────────────────────────

    private async Task HandleNewBlockAsync(Block<string> wsBlock)
    {
        try
        {
            var block = await _grpc.GetLatestBlockAsync().ConfigureAwait(false);
            var row = new BlockRow(
                block.Height,
                block.Time.ToString("HH:mm:ss"),
                block.Txs.Count,
                block.Proposer[..Math.Min(12, block.Proposer.Length)] + "…");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.LatestHeight = block.Height;
                _vm.LatestBlockTime = block.Time.ToString("yyyy-MM-dd HH:mm:ss UTC");
                _vm.LatestBlockTxCount = block.Txs.Count;
                _vm.Blocks.Insert(0, row);
                while (_vm.Blocks.Count > MaxBlocks)
                    _vm.Blocks.RemoveAt(_vm.Blocks.Count - 1);
            });

            AppendEventLog($"Block #{block.Height} — {block.Txs.Count} txs");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendEventLog($"[ERR] GetLatestBlock: {ex.Message}");
        }
    }

    private async Task HandleTxAsync(TxResult<string> tx)
    {
        var row = new TxRow(
            tx.Hash[..Math.Min(16, tx.Hash.Length)] + "…",
            tx.Height,
            tx.Code,
            tx.Log ?? string.Empty);

        try
        {
            var mempool = await _rest.GetNumUnconfirmedTxsAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.PendingTxCount = mempool.Total;
                _vm.PendingTxBytes = mempool.TotalBytes;
                _vm.Transactions.Insert(0, row);
                while (_vm.Transactions.Count > MaxTxs)
                    _vm.Transactions.RemoveAt(_vm.Transactions.Count - 1);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.Transactions.Insert(0, row);
                while (_vm.Transactions.Count > MaxTxs)
                    _vm.Transactions.RemoveAt(_vm.Transactions.Count - 1);
            });
            AppendEventLog($"[ERR] GetNumUnconfirmedTxs: {ex.Message}");
        }

        AppendEventLog($"Tx {row.Hash} h={tx.Height} code={tx.Code}");
    }

    private async Task HandleValidatorSetUpdatedAsync()
    {
        await RefreshValidatorsAsync(CancellationToken.None);
        AppendEventLog("ValidatorSet updated");
    }

    // ── Periodic refresh helpers ──────────────────────────────────────────────

    private async Task RefreshNodeInfoAsync(CancellationToken ct)
    {
        try
        {
            var (nodeInfo, syncInfo) = await _grpc.GetStatusAsync(ct).ConfigureAwait(false);
            _vm.ChainId = nodeInfo.Network;
            _vm.Moniker = nodeInfo.Moniker;
            _vm.NodeId = nodeInfo.Id[..Math.Min(16, nodeInfo.Id.Length)] + "…";
            _vm.NodeVersion = nodeInfo.Version;
            _vm.IsSyncing = syncInfo.CatchingUp;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendEventLog($"[ERR] GetStatus: {ex.Message}");
        }
    }

    private async Task RefreshValidatorsAsync(CancellationToken ct)
    {
        try
        {
            var validators = await _grpc.GetLatestValidatorsAsync(ct).ConfigureAwait(false);
            var rows = validators
                .OrderByDescending(v => v.VotingPower)
                .Select(v => new ValidatorRow(
                    v.Address[..Math.Min(16, v.Address.Length)] + "…",
                    v.VotingPower,
                    v.ProposerPriority))
                .ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.Validators.Clear();
                foreach (var row in rows)
                    _vm.Validators.Add(row);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendEventLog($"[ERR] GetLatestValidators: {ex.Message}");
        }
    }

    private async Task RefreshNetInfoAsync(CancellationToken ct)
    {
        try
        {
            var netInfo = await _rest.GetNetInfoAsync(ct).ConfigureAwait(false);
            _vm.PeerCount = netInfo.PeerCount;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendEventLog($"[ERR] GetNetInfo: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AppendEventLog(string message)
    {
        var row = new EventLogRow(DateTime.UtcNow.ToString("HH:mm:ss"), message);
        Dispatcher.UIThread.Post(() =>
        {
            _vm.EventLog.Insert(0, row);
            while (_vm.EventLog.Count > MaxEventLog)
                _vm.EventLog.RemoveAt(_vm.EventLog.Count - 1);
        });
    }
}
