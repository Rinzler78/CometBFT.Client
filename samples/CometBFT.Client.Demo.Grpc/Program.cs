using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Extensions;
using CometBFT.Client.Grpc;

// ── Config ───────────────────────────────────────────────────────────────────
var grpcUrl = args.FirstOrDefault(a => a.StartsWith("--grpc-url=", StringComparison.OrdinalIgnoreCase))
                  ?.Split('=', 2)[1]
              ?? Environment.GetEnvironmentVariable("COMETBFT_GRPC_URL")
              ?? "cosmoshub.grpc.lava.build";

// ── DI ───────────────────────────────────────────────────────────────────────
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddCometBftGrpc(o => o.BaseUrl = grpcUrl);
        services.AddHostedService<DashboardService>();
    })
    .Build();

await host.RunAsync();
return 0;

// ── Dashboard hosted service ─────────────────────────────────────────────────
internal sealed class DashboardService : BackgroundService
{
    private const int PollSeconds = 10;
    private readonly string _grpcUrl;

    private readonly ICometBftGrpcClient _grpc;

    public DashboardService(ICometBftGrpcClient grpc)
    {
        _grpc = grpc;
        _grpcUrl = Environment.GetEnvironmentVariable("COMETBFT_GRPC_URL") ?? "cosmoshub.grpc.lava.build";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = new GrpcState();
        state.Header = $"[bold cyan]CometBFT.Client.Demo.Grpc[/]\nEndpoint: [dim]{Markup.Escape(_grpcUrl)}[/]\nProtocol: [yellow]detecting…[/]";
        state.AddLog("[dim]gRPC streaming not available for CometBFT v0.38 — polling every 10 s[/]");

        await AnsiConsole.Live(BuildLayout(state))
            .StartAsync(async ctx =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var ts = DateTimeOffset.UtcNow;
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    // Update protocol label after first call resolves the detection
                    var protocolLabel = (_grpc as CometBftGrpcClient)?.DetectedProtocol switch
                    {
                        GrpcProtocol.TendermintLegacy => "[yellow]Tendermint Core (legacy)[/]",
                        GrpcProtocol.CometBft => "[green]CometBFT v0.38.9[/]",
                        _ => "[dim]detecting…[/]",
                    };
                    state.Header = $"[bold cyan]CometBFT.Client.Demo.Grpc[/]\nEndpoint: [dim]{Markup.Escape(_grpcUrl)}[/]\nProtocol: {protocolLabel}";

                    try
                    {
                        var alive = await _grpc.PingAsync(stoppingToken);
                        sw.Stop();
                        state.Broadcast = $"Ping: [{(alive ? "green" : "red")}]{(alive ? "✓ alive" : "✗ down")}[/]\n" +
                                          $"Latency: [bold]{sw.ElapsedMilliseconds} ms[/]\n" +
                                          $"[dim]{ts:HH:mm:ss}[/]";
                        state.Streaming = $"Mode: [yellow]polling[/]\nLast ping: [bold]{ts:HH:mm:ss}[/]\nLatency: {sw.ElapsedMilliseconds} ms";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Broadcast = $"[red]{Markup.Escape(ex.Message)}[/]\n[dim]{ts:HH:mm:ss}[/]";
                        state.Streaming = $"[red]Polling failed[/]\n[dim]{ts:HH:mm:ss}[/]";
                        state.AddLog($"[red]ping: {Markup.Escape(ex.Message)}[/]");
                    }

                    // BroadcastTx probe: send a minimal invalid tx to exercise the check_tx path.
                    try
                    {
                        var broadcastResult = await _grpc.BroadcastTxAsync(new byte[] { 0x0a, 0x01, 0x00 }, stoppingToken);
                        state.CheckTx = FormatCheckTx(broadcastResult, DateTimeOffset.UtcNow);
                        state.AddLog($"[dim]check_tx code={broadcastResult.Code} gas_used={broadcastResult.GasUsed}[/]");
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.CheckTx = $"[dim]check_tx unavailable:[/] [red]{Markup.Escape(ex.Message)}[/]";
                    }

                    ctx.UpdateTarget(BuildLayout(state));
                    ctx.Refresh();

                    await Task.Delay(TimeSpan.FromSeconds(PollSeconds), stoppingToken);
                }
            });

        await _grpc.DisposeAsync();
    }

    private static string FormatCheckTx(BroadcastTxResult r, DateTimeOffset ts) =>
        $"Code:       [{(r.Code == 0 ? "green" : "yellow")}]{r.Code}[/]\n" +
        $"Log:        [dim]{Markup.Escape(r.Log ?? "(none)")}\n[/]" +
        $"Codespace:  [dim]{Markup.Escape(r.Codespace ?? "(none)")}[/]\n" +
        $"GasWanted:  [bold]{r.GasWanted}[/]\n" +
        $"GasUsed:    [bold]{r.GasUsed}[/]\n" +
        $"Hash:       [dim]{Markup.Escape(r.Hash[..Math.Min(16, r.Hash.Length)])}…[/]\n" +
        $"[dim]{ts:HH:mm:ss}[/]";

    private static IRenderable BuildLayout(GrpcState s) =>
        new Rows(
            new Panel(new Markup(s.Header)) { Border = BoxBorder.Rounded },
            new Panel(new Markup(s.Broadcast)) { Header = new PanelHeader("BroadcastAPI — Ping"), Border = BoxBorder.Rounded },
            new Panel(new Markup(s.CheckTx)) { Header = new PanelHeader("BroadcastAPI — check_tx fields"), Border = BoxBorder.Rounded },
            new Panel(new Markup(s.Streaming)) { Header = new PanelHeader("Live Blocks / Streaming Events"), Border = BoxBorder.Rounded },
            new Panel(new Markup(s.Log)) { Header = new PanelHeader("Log"), Border = BoxBorder.Rounded });
}

internal sealed class GrpcState
{
    private const int MaxLog = 20;
    private readonly Queue<string> _log = new();

    public string Header { get; set; } = "[dim]…[/]";
    public string Broadcast { get; set; } = "[dim]…[/]";
    public string CheckTx { get; set; } = "[dim]pending first broadcast probe…[/]";
    public string Streaming { get; set; } = "[dim]gRPC streaming not available - polling every 10 s[/]";
    public string Log => _log.Count > 0 ? string.Join("\n", _log.Reverse()) : "[dim](empty)[/]";

    public void AddLog(string line)
    {
        if (_log.Count >= MaxLog)
        {
            _log.Dequeue();
        }

        _log.Enqueue(line);
    }
}
