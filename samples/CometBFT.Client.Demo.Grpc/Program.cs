using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    private readonly ICometBftSdkGrpcClient _client;

    public DashboardService(ICometBftSdkGrpcClient client)
    {
        _client = client;
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

                        // ── Status ────────────────────────────────────────────────
                        try
                        {
                            var (nodeInfo, syncInfo) = await _client.GetStatusAsync(stoppingToken);
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

                        // ── Latest block ──────────────────────────────────────────
                        sw.Restart();
                        try
                        {
                            var block = await _client.GetLatestBlockAsync(stoppingToken);
                            sw.Stop();
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

                        // ── Validators ────────────────────────────────────────────
                        sw.Restart();
                        try
                        {
                            var vals = await _client.GetLatestValidatorsAsync(stoppingToken);
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
            await _client.DisposeAsync();
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
            new Panel(new Markup("[bold cyan]CometBFT.Client.Demo.Grpc[/]  [dim](cosmos.base.tendermint.v1beta1.Service)[/]")) { Border = BoxBorder.Rounded },
            new Columns(
                new Panel(new Markup(s.Status)) { Header = new PanelHeader("Health / Status"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Block)) { Header = new PanelHeader("Latest Block"), Border = BoxBorder.Rounded }),
            new Panel(new Markup(s.Validators)) { Header = new PanelHeader("Validators"), Border = BoxBorder.Rounded },
            new Panel(new Markup(s.Log)) { Header = new PanelHeader("Log"), Border = BoxBorder.Rounded });
}

internal sealed class GrpcState
{
    private const int MaxLog = 20;
    private readonly Queue<string> _log = new();

    public string Status { get; set; } = "[dim]…[/]";
    public string Block { get; set; } = "[dim]…[/]";
    public string Validators { get; set; } = "[dim]…[/]";
    public string Log => _log.Count > 0 ? string.Join("\n", _log.Reverse()) : "[dim](empty)[/]";

    public void AddLog(string line)
    {
        if (_log.Count >= MaxLog) _log.Dequeue();
        _log.Enqueue(line);
    }
}
