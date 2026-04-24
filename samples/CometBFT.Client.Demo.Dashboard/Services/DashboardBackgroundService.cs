using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Demo.Dashboard.ViewModels;

namespace CometBFT.Client.Demo.Dashboard.Services;

/// <summary>
/// Orchestrator — connects to the CometBFT WebSocket, drives REST refreshes and
/// feeds the ViewModel through its typed domain-oriented API. This class knows
/// nothing about Avalonia threading or UI row records; all of that lives in
/// <see cref="MainWindowViewModel"/>.
/// </summary>
/// <remarks>
/// Subscribes are issued as a concurrent burst (<c>Task.WhenAll</c>). The ACK
/// pattern documented in <c>openspec/changes/websocket-events-protocol-v0.39.1/</c>
/// shows relays batch-flush ACKs only when multiple subscribes arrive on the wire;
/// serial subscribes stall the first ACK for 30–45 s.
///
/// CometBFT's default <c>max_subscriptions_per_client = 5</c>; the dashboard requests
/// 7 topics so the last 2 may be rejected on a standard node. <see cref="Resilient"/>
/// handles this gracefully — surviving topics continue streaming and the UI shows
/// <c>"Degraded (n/7 topics)"</c> instead of <c>"Connected"</c>.
/// </remarks>
internal sealed class DashboardBackgroundService : BackgroundService
{
    private static readonly TimeSpan PeriodicRefreshInterval = TimeSpan.FromSeconds(30);
    private const int SubscribeCount = 7;

    private readonly ICometBftWebSocketClient _ws;
    private readonly ICometBftRestClient _rest;
    private readonly MainWindowViewModel _vm;
    private readonly ILogger<DashboardBackgroundService> _logger;

    // Stored so event handlers fired after shutdown still pass a valid token.
    private CancellationToken _stoppingToken;

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
        _stoppingToken = stoppingToken;

        try
        {
            _ws.NewBlockReceived += OnNewBlock;
            _ws.NewBlockHeaderReceived += OnNewBlockHeader;
            _ws.TxExecuted += OnTxExecuted;
            _ws.VoteReceived += OnVote;
            _ws.ValidatorSetUpdated += OnValidatorSetUpdated;
            _ws.ErrorOccurred += OnError;

            // Safe to subscribe before ConnectAsync — *Stream observables are pre-initialized.
            using var evidenceSub = _ws.NewEvidenceStream.Subscribe(e =>
                _vm.AppendEventLog("evidence", $"Evidence at h={e.Height} type={e.EvidenceType}"));

            _logger.LogInformation("Connecting to WebSocket…");
            await _ws.ConnectAsync(stoppingToken);
            _vm.SetConnectionStatus("Subscribing…", isConnected: true);

            // Start both groups before awaiting either — full parallelism preserved.
            // Subscribes are split from REST so we can count subscribe successes.
            var subscribeTask = Task.WhenAll(
                Resilient("subscribe NewBlock", _ws.SubscribeNewBlockAsync(stoppingToken)),
                Resilient("subscribe NewBlockHeader", _ws.SubscribeNewBlockHeaderAsync(stoppingToken)),
                Resilient("subscribe Tx", _ws.SubscribeTxAsync(stoppingToken)),
                Resilient("subscribe Vote", _ws.SubscribeVoteAsync(stoppingToken)),
                Resilient("subscribe ValidatorSetUpdates", _ws.SubscribeValidatorSetUpdatesAsync(stoppingToken)),
                Resilient("subscribe NewBlockEvents", _ws.SubscribeNewBlockEventsAsync(stoppingToken)),
                Resilient("subscribe NewEvidence", _ws.SubscribeNewEvidenceAsync(stoppingToken)));

            var restTask = Task.WhenAll(
                RefreshNodeInfoAsync(stoppingToken),
                RefreshValidatorsAsync(stoppingToken),
                RefreshNetInfoAsync(stoppingToken));

            var results = await subscribeTask;
            await restTask;

            var ok = results.Count(r => r);
            _vm.SetConnectionStatus(
                ok == SubscribeCount ? "Connected" : $"Degraded ({ok}/{SubscribeCount} topics)",
                isConnected: ok > 0);
            _logger.LogInformation("Dashboard live ({Ok}/{Total} topics active).", ok, SubscribeCount);

            using var timer = new PeriodicTimer(PeriodicRefreshInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await Task.WhenAll(
                    RefreshNodeInfoAsync(stoppingToken),
                    RefreshNetInfoAsync(stoppingToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Dashboard service failure");
            _vm.AppendEventLog("fatal", $"Service crashed: {ex.Message}");
        }
        finally
        {
            _ws.NewBlockReceived -= OnNewBlock;
            _ws.NewBlockHeaderReceived -= OnNewBlockHeader;
            _ws.TxExecuted -= OnTxExecuted;
            _ws.VoteReceived -= OnVote;
            _ws.ValidatorSetUpdated -= OnValidatorSetUpdated;
            _ws.ErrorOccurred -= OnError;

            _vm.SetConnectionStatus("Disconnected", isConnected: false);
        }
    }

    // ── WebSocket event handlers — delegate straight to the ViewModel ────────

    private void OnNewBlock(object? sender, CometBftEventArgs<Block<string>> e) =>
        _ = HandleNewBlockEnrichmentAsync(e.Value);

    private void OnNewBlockHeader(object? sender, CometBftEventArgs<BlockHeader> e) =>
        _vm.AppendEventLog("header", $"Header #{e.Value.Height} — {e.Value.Time:HH:mm:ss}");

    private void OnTxExecuted(object? sender, CometBftEventArgs<TxResult<string>> e) =>
        _ = HandleTxEnrichmentAsync(e.Value);

    private void OnVote(object? sender, CometBftEventArgs<Vote> e) =>
        _vm.AppendEventLog("vote", $"Vote h={e.Value.Height} r={e.Value.Round} type={e.Value.Type}");

    private void OnValidatorSetUpdated(object? sender, CometBftEventArgs<IReadOnlyList<Validator>> e) =>
        _ = RefreshValidatorsAsync(_stoppingToken);

    private void OnError(object? sender, CometBftEventArgs<Exception> e)
    {
        _logger.LogError(e.Value, "WS Error: {Message}", e.Value.Message);
        _vm.AppendEventLog("error", e.Value.Message);
    }

    // ── Enrichment — WebSocket event → VM + REST follow-up ──────────────────

    private async Task HandleNewBlockEnrichmentAsync(Block<string> wsBlock)
    {
        _vm.OnNewBlock(wsBlock);

        try
        {
            var fullBlock = await _rest.GetBlockAsync(wsBlock.Height).ConfigureAwait(false);
            _vm.AppendEventLog(
                "block",
                $"Block #{fullBlock.Height} enriched via REST. Hash: {MainWindowViewModel.Abbreviate(fullBlock.Hash, 8)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("REST enrichment failed for block {H}: {Msg}", wsBlock.Height, ex.Message);
        }
    }

    private async Task HandleTxEnrichmentAsync(TxResult<string> tx)
    {
        _vm.OnTransaction(tx);

        try
        {
            var mempool = await _rest.GetNumUnconfirmedTxsAsync().ConfigureAwait(false);
            _vm.UpdateMempool(mempool);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("REST mempool refresh failed: {Msg}", ex.Message);
        }
    }

    // ── Periodic REST helpers ─────────────────────────────────────────────────

    private async Task RefreshNodeInfoAsync(CancellationToken ct)
    {
        try
        {
            var (nodeInfo, syncInfo) = await _rest.GetStatusAsync(ct).ConfigureAwait(false);
            _vm.UpdateNodeStatus(nodeInfo, syncInfo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _vm.AppendEventLog("error", $"REST Status failed: {ex.Message}");
        }
    }

    private async Task RefreshValidatorsAsync(CancellationToken ct)
    {
        try
        {
            var validators = await _rest.GetValidatorsAsync(cancellationToken: ct).ConfigureAwait(false);
            _vm.UpdateValidators(validators);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _vm.AppendEventLog("error", $"REST Validators failed: {ex.Message}");
        }
    }

    private async Task RefreshNetInfoAsync(CancellationToken ct)
    {
        try
        {
            var netInfo = await _rest.GetNetInfoAsync(ct).ConfigureAwait(false);
            _vm.UpdatePeerCount(netInfo.PeerCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("RefreshNetInfo failed: {Msg}", ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Awaits <paramref name="task"/> and returns <c>true</c> on success or <c>false</c>
    /// on non-cancellation failure. Used in the startup burst so one failing topic (e.g.
    /// relay rate-limits the 6th subscribe) does not fail-fast <c>Task.WhenAll</c> and
    /// does not block the UI from reflecting partial connectivity.
    /// </summary>
    internal async Task<bool> Resilient(string step, Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup step '{Step}' failed (dashboard continues with other topics).", step);
            _vm.AppendEventLog("startup", $"{step} failed: {ex.Message}");
            return false;
        }
    }
}
