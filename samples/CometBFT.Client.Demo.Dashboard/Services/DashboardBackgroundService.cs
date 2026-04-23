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
internal sealed class DashboardBackgroundService : BackgroundService
{
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
            _vm.AppendEventLog("evidence", $"Evidence at h={e.Height} type={e.EvidenceType}"));

        try
        {
            _logger.LogInformation("Connecting to WebSocket…");
            await _ws.ConnectAsync(stoppingToken);
            _vm.SetConnectionStatus("Subscribing…", isConnected: true);

            // Burst all subscribes + initial REST loads concurrently. On the relay the
            // subscribe batch completes in ~12 s and REST completes in ~1 s. Each task
            // is wrapped in Resilient so a single topic failure (e.g. relay rate-limits
            // NewEvidence) does not fail-fast Task.WhenAll and abort the whole dashboard.
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

            _vm.SetConnectionStatus("Connected", isConnected: true);
            _logger.LogInformation("Dashboard live.");

            using var timer = new PeriodicTimer(PeriodicRefreshInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshNodeInfoAsync(stoppingToken);
                await RefreshNetInfoAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
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
        _ = RefreshValidatorsAsync(CancellationToken.None);

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
                $"Block #{fullBlock.Height} enriched via REST. Hash: {Safe(fullBlock.Hash, 8)}…");
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
    /// Awaits <paramref name="task"/> and swallows non-cancellation exceptions so that
    /// <c>Task.WhenAll</c> does not fail-fast when a single startup step fails (e.g. the
    /// relay 429s one of the parallel subscribes). Cancellation propagates normally so
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
            _vm.AppendEventLog("startup", $"{step} failed: {ex.Message}");
        }
    }

    private static string Safe(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
