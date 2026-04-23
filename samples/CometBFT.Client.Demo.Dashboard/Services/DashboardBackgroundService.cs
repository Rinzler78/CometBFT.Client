using Avalonia.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Demo.Dashboard.ViewModels;

namespace CometBFT.Client.Demo.Dashboard.Services;

/// <summary>
/// Drives the dashboard: one WebSocket connection with a burst of concurrent
/// <c>Subscribe…Async</c> calls plus REST state refreshes, then a 30 s periodic
/// poll for non-event data. The burst is intentional — the ACK pattern documented
/// in <c>/openspec/changes/websocket-events-protocol-v0.39.1/</c> shows relays
/// batch-flush ACKs only when multiple subscribes arrive on the wire, so serial
/// subscribes stall the first ACK for 30–45 s.
/// </summary>
internal sealed class DashboardBackgroundService : BackgroundService
{
    private const int MaxBlocks = 50;
    private const int MaxTxs = 50;
    private const int MaxEventLog = 100;
    private static readonly TimeSpan PeriodicRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly ICometBftWebSocketClient _ws;
    private readonly ICometBftRestClient _rest;
    private readonly MainWindowViewModel _vm;
    private readonly ILogger<DashboardBackgroundService> _logger;

    public DashboardBackgroundService(
        ICometBftWebSocketClient ws,
        ICometBftRestClient rest,
        MainWindowViewModel vm,
        ILogger<DashboardBackgroundService> logger)
    {
        _ws = ws;
        _rest = rest;
        _vm = vm;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ws.NewBlockReceived += OnNewBlock;
        _ws.NewBlockHeaderReceived += OnNewBlockHeader;
        _ws.TxExecuted += OnTxExecuted;
        _ws.VoteReceived += OnVote;
        _ws.ValidatorSetUpdated += OnValidatorSetUpdated;
        _ws.ErrorOccurred += OnError;

        using var evidenceSub = _ws.NewEvidenceStream.Subscribe(e =>
            AppendEventLog("evidence", $"Evidence at h={e.Height} type={e.EvidenceType}"));

        try
        {
            _logger.LogInformation("Connecting to WebSocket…");
            await _ws.ConnectAsync(stoppingToken);
            await SetConnectionStatusAsync("Subscribing…", isConnected: true);

            // Burst all subscribes + initial REST loads concurrently. On the relay the
            // subscribe batch completes in ~12 s and REST completes in ~1 s — the UI
            // becomes fully live in roughly one subscribe burst with no serial stall.
            // Each task is wrapped in Resilient(...) so a single failure (e.g. relay
            // rate-limits NewEvidence) does not fail-fast Task.WhenAll and abort the
            // whole dashboard — failures are logged and the surviving topics stream.
            await Task.WhenAll(
                Resilient("subscribe NewBlock", _ws.SubscribeNewBlockAsync(stoppingToken)),
                Resilient("subscribe NewBlockHeader", _ws.SubscribeNewBlockHeaderAsync(stoppingToken)),
                Resilient("subscribe Tx", _ws.SubscribeTxAsync(stoppingToken)),
                Resilient("subscribe Vote", _ws.SubscribeVoteAsync(stoppingToken)),
                Resilient("subscribe ValidatorSetUpdates", _ws.SubscribeValidatorSetUpdatesAsync(stoppingToken)),
                Resilient("subscribe NewBlockEvents", _ws.SubscribeNewBlockEventsAsync(stoppingToken)),
                Resilient("subscribe NewEvidence", _ws.SubscribeNewEvidenceAsync(stoppingToken)),
                RefreshNodeInfoAsync(stoppingToken),
                RefreshValidatorsAsync(stoppingToken),
                RefreshNetInfoAsync(stoppingToken));

            await SetConnectionStatusAsync("Connected", isConnected: true);
            _logger.LogInformation("Dashboard live.");

            using var timer = new PeriodicTimer(PeriodicRefreshInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshNodeInfoAsync(stoppingToken);
                await RefreshNetInfoAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Dashboard service failure");
        }
        finally
        {
            _ws.NewBlockReceived -= OnNewBlock;
            _ws.NewBlockHeaderReceived -= OnNewBlockHeader;
            _ws.TxExecuted -= OnTxExecuted;
            _ws.VoteReceived -= OnVote;
            _ws.ValidatorSetUpdated -= OnValidatorSetUpdated;
            _ws.ErrorOccurred -= OnError;

            Dispatcher.UIThread.Post(() =>
            {
                _vm.ConnectionStatus = "Disconnected";
                _vm.IsConnected = false;
            });
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnNewBlock(object? sender, CometBftEventArgs<Block<string>> e) =>
        _ = HandleNewBlockEnrichmentAsync(e.Value);

    private void OnNewBlockHeader(object? sender, CometBftEventArgs<BlockHeader> e) =>
        AppendEventLog("header", $"Header #{e.Value.Height} — {e.Value.Time:HH:mm:ss}");

    private void OnTxExecuted(object? sender, CometBftEventArgs<TxResult<string>> e) =>
        _ = HandleTxEnrichmentAsync(e.Value);

    private void OnVote(object? sender, CometBftEventArgs<Vote> e) =>
        AppendEventLog("vote", $"Vote h={e.Value.Height} r={e.Value.Round} type={e.Value.Type}");

    private void OnValidatorSetUpdated(object? sender, CometBftEventArgs<IReadOnlyList<Validator>> e) =>
        _ = RefreshValidatorsAsync(CancellationToken.None);

    private void OnError(object? sender, CometBftEventArgs<Exception> e)
    {
        _logger.LogError(e.Value, "WS Error: {Message}", e.Value.Message);
        AppendEventLog("error", e.Value.Message);
    }

    // ── Enrichment (REST calls triggered by events) ───────────────────────────

    private async Task HandleNewBlockEnrichmentAsync(Block<string> wsBlock)
    {
        var row = new BlockRow(
            wsBlock.Height,
            wsBlock.Time.ToString("HH:mm:ss"),
            wsBlock.Txs.Count,
            wsBlock.Proposer[..Math.Min(12, wsBlock.Proposer.Length)] + "…");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_vm.Blocks.Any(b => b.Height == wsBlock.Height))
            {
                return;
            }
            _vm.Blocks.Insert(0, row);
            _vm.LatestHeight = wsBlock.Height;
            _vm.LatestBlockTime = wsBlock.Time.ToString("yyyy-MM-dd HH:mm:ss UTC");
            _vm.LatestBlockTxCount = wsBlock.Txs.Count;
            while (_vm.Blocks.Count > MaxBlocks)
            {
                _vm.Blocks.RemoveAt(_vm.Blocks.Count - 1);
            }
        });

        try
        {
            var fullBlock = await _rest.GetBlockAsync(wsBlock.Height).ConfigureAwait(false);
            AppendEventLog("block", $"Block #{fullBlock.Height} enriched via REST. Hash: {fullBlock.Hash[..8]}…");
        }
        catch (Exception ex)
        {
            _logger.LogDebug("REST enrichment failed for block {H}: {Msg}", wsBlock.Height, ex.Message);
        }
    }

    private async Task HandleTxEnrichmentAsync(TxResult<string> tx)
    {
        var row = new TxRow(
            tx.Hash[..Math.Min(16, tx.Hash.Length)] + "…",
            tx.Height,
            tx.Code,
            tx.Log ?? string.Empty);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _vm.Transactions.Insert(0, row);
            while (_vm.Transactions.Count > MaxTxs)
            {
                _vm.Transactions.RemoveAt(_vm.Transactions.Count - 1);
            }
        });

        try
        {
            var mempool = await _rest.GetNumUnconfirmedTxsAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.PendingTxCount = mempool.Total;
                _vm.PendingTxBytes = mempool.TotalBytes;
            });
        }
        catch
        {
            // best-effort for demo robustness
        }
    }

    // ── Periodic REST helpers ─────────────────────────────────────────────────

    private async Task RefreshNodeInfoAsync(CancellationToken ct)
    {
        try
        {
            var (nodeInfo, syncInfo) = await _rest.GetStatusAsync(ct).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.ChainId = nodeInfo.Network;
                _vm.Moniker = nodeInfo.Moniker;
                _vm.NodeId = nodeInfo.Id[..Math.Min(16, nodeInfo.Id.Length)] + "…";
                _vm.NodeVersion = nodeInfo.Version;
                _vm.IsSyncing = syncInfo.CatchingUp;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendEventLog("error", $"REST Status failed: {ex.Message}");
        }
    }

    private async Task RefreshValidatorsAsync(CancellationToken ct)
    {
        try
        {
            var validators = await _rest.GetValidatorsAsync(cancellationToken: ct).ConfigureAwait(false);
            var sorted = validators.OrderByDescending(v => v.VotingPower).ToList();
            var totalPower = sorted.Sum(v => (double)v.VotingPower);
            var rows = sorted.Select((v, i) => new ValidatorRow(
                i + 1,
                v.Address[..Math.Min(16, v.Address.Length)] + "…",
                v.VotingPower,
                v.ProposerPriority,
                totalPower > 0 ? Math.Round(v.VotingPower / totalPower * 100, 1) : 0)).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _vm.Validators.Clear();
                foreach (var r in rows)
                {
                    _vm.Validators.Add(r);
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendEventLog("error", $"REST Validators failed: {ex.Message}");
        }
    }

    private async Task RefreshNetInfoAsync(CancellationToken ct)
    {
        try
        {
            var netInfo = await _rest.GetNetInfoAsync(ct).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => _vm.PeerCount = netInfo.PeerCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("RefreshNetInfo failed: {Msg}", ex.Message);
        }
    }

    private Task SetConnectionStatusAsync(string status, bool isConnected) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _vm.ConnectionStatus = status;
            _vm.IsConnected = isConnected;
        }).GetTask();

    /// <summary>
    /// Awaits <paramref name="task"/> and swallows non-cancellation exceptions so that
    /// <c>Task.WhenAll</c> does not fail-fast when a single startup step fails (e.g. the
    /// relay 429s one of ten parallel subscribes). Cancellation propagates normally so
    /// shutdown works as expected.
    /// </summary>
    private async Task Resilient(string step, Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup step '{Step}' failed (dashboard continues with other topics).", step);
            AppendEventLog("startup", $"{step} failed: {ex.Message}");
        }
    }

    private void AppendEventLog(string category, string message)
    {
        var row = new EventLogRow(DateTime.UtcNow.ToString("HH:mm:ss"), category, message);
        Dispatcher.UIThread.Post(() =>
        {
            _vm.EventLog.Insert(0, row);
            while (_vm.EventLog.Count > MaxEventLog)
            {
                _vm.EventLog.RemoveAt(_vm.EventLog.Count - 1);
            }
        });
    }
}
