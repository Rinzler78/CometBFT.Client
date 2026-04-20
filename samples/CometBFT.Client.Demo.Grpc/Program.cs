using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;
using CometBFT.Client.Demo.Shared;

// ── Config ───────────────────────────────────────────────────────────────────
var grpcUrl = args.FirstOrDefault(a => a.StartsWith("--grpc-url=", StringComparison.OrdinalIgnoreCase))
                  ?.Split('=', 2)[1]
              ?? Environment.GetEnvironmentVariable("COMETBFT_GRPC_URL")
              ?? DemoDefaults.GrpcUrl;

// ── DI ───────────────────────────────────────────────────────────────────────
var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Warning);
    })
    .ConfigureServices(services =>
    {
        services.AddCometBftSdkGrpc(o => o.BaseUrl = grpcUrl);
        services.AddHostedService<DashboardService>();
    })
    .Build();

await host.RunAsync();
return 0;

// ── Dashboard hosted service ─────────────────────────────────────────────────
internal sealed class DashboardService : BackgroundService
{
    private const int RefreshSeconds = 10;
    private readonly string _grpcUrl;
    private readonly ICometBftSdkGrpcClient _sdkClient;

    public DashboardService(ICometBftSdkGrpcClient sdkClient)
    {
        _sdkClient = sdkClient;
        _grpcUrl = Environment.GetEnvironmentVariable("COMETBFT_GRPC_URL") ?? DemoDefaults.GrpcUrl;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = new GrpcState();

        try
        {
            await AnsiConsole.Live(BuildLayout(state))
                .StartAsync(async ctx =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var ts = DateTimeOffset.UtcNow;
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        // ── ICometBftSdkGrpcClient.GetStatusAsync ─────────────────
                        try
                        {
                            sw.Restart();
                            var (nodeInfo, syncInfo) = await _sdkClient.GetStatusAsync(stoppingToken);
                            sw.Stop();
                            state.Status =
                                $"Network: [yellow]{Markup.Escape(nodeInfo.Network)}[/]  " +
                                $"Version: {Markup.Escape(nodeInfo.Version)}\n" +
                                $"Moniker: [bold]{Markup.Escape(nodeInfo.Moniker)}[/]\n" +
                                $"Node ID: [dim]{Markup.Escape(nodeInfo.Id)}[/]\n" +
                                $"Endpoint: [dim]{Markup.Escape(_grpcUrl)}[/]\n" +
                                $"Syncing: [{(syncInfo.CatchingUp ? "yellow" : "green")}]{(syncInfo.CatchingUp ? "catching up" : "synced")}[/]\n" +
                                $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.Status = $"[red]{Markup.Escape(ex.Message)}[/]\n[dim]{ts:HH:mm:ss}[/]";
                            state.AddLog($"[red]status: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }

                        // ── ICometBftSdkGrpcClient.GetSyncingAsync ────────────────
                        try
                        {
                            var syncing = await _sdkClient.GetSyncingAsync(stoppingToken);
                            state.Syncing = syncing
                                ? "[yellow]catching up[/]"
                                : "[green]fully synced[/]";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.Syncing = $"[red]{Markup.Escape(ex.Message)}[/]";
                            state.AddLog($"[red]syncing: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }

                        // ── ICometBftSdkGrpcClient.GetLatestBlockAsync ────────────
                        long latestHeight = 0;
                        sw.Restart();
                        try
                        {
                            var block = await _sdkClient.GetLatestBlockAsync(stoppingToken);
                            sw.Stop();
                            latestHeight = block.Height;
                            var proposerPfx = block.Proposer.Length > 0
                                ? block.Proposer[..Math.Min(16, block.Proposer.Length)]
                                : "?";
                            state.Block =
                                $"Height: [bold]{block.Height:N0}[/]\n" +
                                $"Time: {block.Time:HH:mm:ss}\n" +
                                $"Proposer: [dim]{Markup.Escape(proposerPfx)}…[/]\n" +
                                $"Txs: {block.Txs.Count}\n" +
                                $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.Block = $"[red]{Markup.Escape(ex.Message)}[/]";
                            state.AddLog($"[red]block: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }

                        // ── ICometBftSdkGrpcClient.GetBlockByHeightAsync ──────────
                        try
                        {
                            if (latestHeight > 1)
                            {
                                sw.Restart();
                                var prev = await _sdkClient.GetBlockByHeightAsync(latestHeight - 1, stoppingToken);
                                sw.Stop();
                                state.BlockByHeight =
                                    $"Height: [bold]{prev.Height:N0}[/]\n" +
                                    $"Time: {prev.Time:HH:mm:ss}\n" +
                                    $"Txs: {prev.Txs.Count}\n" +
                                    $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                            }
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.BlockByHeight = $"[red]{Markup.Escape(ex.Message)}[/]";
                            state.AddLog($"[red]block-by-height: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }

                        // ── ICometBftSdkGrpcClient.GetLatestValidatorsAsync ───────
                        sw.Restart();
                        try
                        {
                            var vals = await _sdkClient.GetLatestValidatorsAsync(stoppingToken);
                            sw.Stop();
                            var rows = vals.Take(5).Select(v =>
                            {
                                var addr = v.Address.Length > 0
                                    ? v.Address[..Math.Min(20, v.Address.Length)]
                                    : "?";
                                return $"[dim]{Markup.Escape(addr)}…[/] power={v.VotingPower:N0}";
                            });
                            state.Validators =
                                string.Join("\n", rows) +
                                $"\n[dim]+{Math.Max(0, vals.Count - 5)} more · {sw.ElapsedMilliseconds} ms[/]";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.Validators = $"[red]{Markup.Escape(ex.Message)}[/]";
                            state.AddLog($"[red]validators: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }

                        // ── ICometBftSdkGrpcClient.GetValidatorSetByHeightAsync ───
                        try
                        {
                            if (latestHeight > 1)
                            {
                                sw.Restart();
                                var vals = await _sdkClient.GetValidatorSetByHeightAsync(latestHeight - 1, stoppingToken);
                                sw.Stop();
                                var rows = vals.Take(3).Select(v =>
                                {
                                    var addr = v.Address.Length > 0
                                        ? v.Address[..Math.Min(20, v.Address.Length)]
                                        : "?";
                                    return $"[dim]{Markup.Escape(addr)}…[/] power={v.VotingPower:N0}";
                                });
                                state.ValidatorsByHeight =
                                    $"Height: [bold]{latestHeight - 1:N0}[/]\n" +
                                    string.Join("\n", rows) +
                                    $"\n[dim]+{Math.Max(0, vals.Count - 3)} more · {sw.ElapsedMilliseconds} ms[/]";
                            }
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.ValidatorsByHeight = $"[red]{Markup.Escape(ex.Message)}[/]";
                            state.AddLog($"[red]validators-by-height: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }

                        // ── ICometBftSdkGrpcClient.ABCIQueryAsync ─────────────────
                        try
                        {
                            sw.Restart();
                            var abci = await _sdkClient.ABCIQueryAsync("/app/version", [], cancellationToken: stoppingToken);
                            sw.Stop();
                            var value = abci.Value.Count > 0
                                ? System.Text.Encoding.UTF8.GetString([.. abci.Value])
                                : "(empty)";
                            state.AbciQuery =
                                $"Path: [dim]/app/version[/]\n" +
                                $"Code: {abci.Code}  Height: {abci.Height}\n" +
                                $"Value: [bold]{Markup.Escape(value)}[/]\n" +
                                $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.AbciQuery = $"[red]{Markup.Escape(ex.Message)}[/]";
                            state.AddLog($"[red]abci-query: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }

                        ctx.UpdateTarget(BuildLayout(state));
                        ctx.Refresh();

                        await Task.Delay(TimeSpan.FromSeconds(RefreshSeconds), stoppingToken);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            await _sdkClient.DisposeAsync();
        }
    }

    private static string FirstLine(string? s, int maxLen = 120)
    {
        if (string.IsNullOrEmpty(s)) return "(no detail)";
        var line = s.Split('\n', 2)[0].Trim();
        return line.Length > maxLen ? line[..maxLen] + "…" : line;
    }

    private static IRenderable BuildLayout(GrpcState s) =>
        new Rows(
            new Panel(new Markup("[bold cyan]CometBFT.Client.Demo.Grpc[/]  [dim](cosmos.base.tendermint.v1beta1.Service — ICometBftSdkGrpcClient)[/]")) { Border = BoxBorder.Rounded },
            new Columns(
                new Panel(new Markup(s.Status)) { Header = new PanelHeader("Node Status (GetStatusAsync)"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Syncing)) { Header = new PanelHeader("Syncing (GetSyncingAsync)"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.Block)) { Header = new PanelHeader("Latest Block (GetLatestBlockAsync)"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.BlockByHeight)) { Header = new PanelHeader("Block by Height (GetBlockByHeightAsync)"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.Validators)) { Header = new PanelHeader("Latest Validators (GetLatestValidatorsAsync)"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.ValidatorsByHeight)) { Header = new PanelHeader("Validators by Height (GetValidatorSetByHeightAsync)"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.AbciQuery)) { Header = new PanelHeader("ABCI Query (ABCIQueryAsync)"), Border = BoxBorder.Rounded },
                new Panel(new Markup("[dim]ICometBftGrpcClient (PingAsync, BroadcastTxAsync)[/]\n[dim]uses cometbft.rpc.grpc.v1beta1.BroadcastAPI[/]\n[dim]requires direct node access —[/]\n[dim]not available on public relays.[/]\n[dim]See integration tests.[/]")) { Header = new PanelHeader("Raw gRPC (ICometBftGrpcClient)"), Border = BoxBorder.Rounded }),
            new Panel(new Markup(s.Log)) { Header = new PanelHeader("Log"), Border = BoxBorder.Rounded });
}

internal sealed class GrpcState
{
    private const int MaxLog = 20;
    private readonly Queue<string> _log = new();

    public string Status { get; set; } = "[dim]…[/]";
    public string Syncing { get; set; } = "[dim]…[/]";
    public string Block { get; set; } = "[dim]…[/]";
    public string BlockByHeight { get; set; } = "[dim]…[/]";
    public string Validators { get; set; } = "[dim]…[/]";
    public string ValidatorsByHeight { get; set; } = "[dim]…[/]";
    public string AbciQuery { get; set; } = "[dim]…[/]";
    public string Log => _log.Count > 0 ? string.Join("\n", _log.Reverse()) : "[dim](empty)[/]";

    public void AddLog(string line)
    {
        if (_log.Count >= MaxLog) _log.Dequeue();
        _log.Enqueue(line);
    }
}
