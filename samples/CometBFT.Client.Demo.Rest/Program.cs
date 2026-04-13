using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;
using CometBFT.Client.Demo.Shared;

// ── Config ───────────────────────────────────────────────────────────────────
var rpcUrl = args.FirstOrDefault(a => a.StartsWith("--rpc-url=", StringComparison.OrdinalIgnoreCase))
                 ?.Split('=', 2)[1]
              ?? Environment.GetEnvironmentVariable("COMETBFT_RPC_URL")
              ?? DemoDefaults.RpcUrl;

// --unsafe flag: enables display of Unsafe endpoint diagnostics.
// Only works against a node started with --rpc.unsafe=true.
var unsafeMode = args.Any(a => a.Equals("--unsafe", StringComparison.OrdinalIgnoreCase));

// ── DI ───────────────────────────────────────────────────────────────────────
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddCometBftRest(o => o.BaseUrl = rpcUrl);
        services.AddSingleton(new DemoConfig(unsafeMode));
        services.AddHostedService<DashboardService>();
    })
    .Build();

await host.RunAsync();
return 0;

// ── Config record ────────────────────────────────────────────────────────────
internal sealed record DemoConfig(bool UnsafeMode);

// ── Dashboard hosted service ─────────────────────────────────────────────────
internal sealed class DashboardService : BackgroundService
{
    private const int RefreshSeconds = 10;
    private readonly string _rpcUrl;
    private readonly bool _unsafeMode;

    private readonly ICometBftRestClient _client;

    public DashboardService(ICometBftRestClient client, DemoConfig config)
    {
        _client = client;
        _unsafeMode = config.UnsafeMode;
        _rpcUrl = Environment.GetEnvironmentVariable("COMETBFT_RPC_URL") ?? DemoDefaults.RpcUrl;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = new DashboardState();

        await AnsiConsole.Live(BuildLayout(state))
            .StartAsync(async ctx =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var ts = DateTimeOffset.UtcNow;
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        var healthy = await _client.GetHealthAsync(stoppingToken);
                        var (nodeInfo, syncInfo) = await _client.GetStatusAsync(stoppingToken);
                        sw.Stop();
                        state.Health = $"[{(healthy ? "green" : "red")}]{(healthy ? "✓ healthy" : "✗ unhealthy")}[/]\n" +
                                       $"Network: [yellow]{nodeInfo.Network}[/]  Version: {Markup.Escape(nodeInfo.Version)}\n" +
                                       $"Height: [bold]{syncInfo.LatestBlockHeight:N0}[/]  Catching up: {syncInfo.CatchingUp}\n" +
                                       $"Endpoint: [dim]{Markup.Escape(_rpcUrl)}[/]\n" +
                                       $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Health = $"[red]{Markup.Escape(ex.Message)}[/]\n[dim]{ts:HH:mm:ss}[/]";
                        state.AddLog($"[red]health/status: {Markup.Escape(ex.Message)}[/]");
                    }

                    sw.Restart();
                    try
                    {
                        var block = await _client.GetBlockAsync(cancellationToken: stoppingToken);
                        sw.Stop();
                        state.Block = $"Height: [bold]{block.Height:N0}[/]\n" +
                                      $"Hash: [dim]{Markup.Escape(block.Hash)}[/]\n" +
                                      $"Proposer: {Markup.Escape(block.Proposer)}\n" +
                                      $"Txs: {block.Txs.Count}\n" +
                                      $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Block = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]block: {Markup.Escape(ex.Message)}[/]");
                    }

                    sw.Restart();
                    try
                    {
                        var vals = await _client.GetValidatorsAsync(cancellationToken: stoppingToken);
                        sw.Stop();
                        var rows = vals.Take(5).Select(v =>
                            $"[dim]{Markup.Escape(v.Address[..Math.Min(16, v.Address.Length)])}…[/] power={v.VotingPower:N0}");
                        state.Validators = string.Join("\n", rows) +
                                           $"\n[dim]+{Math.Max(0, vals.Count - 5)} more · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Validators = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]validators: {Markup.Escape(ex.Message)}[/]");
                    }

                    sw.Restart();
                    try
                    {
                        var abci = await _client.GetAbciInfoAsync(stoppingToken);
                        sw.Stop();
                        state.Abci = $"Data: {Markup.Escape(abci.GetValueOrDefault("data", "?"))}\n" +
                                     $"Version: {Markup.Escape(abci.GetValueOrDefault("version", "?"))}\n" +
                                     $"App ver: {Markup.Escape(abci.GetValueOrDefault("app_version", "?"))}\n" +
                                     $"Last h: {Markup.Escape(abci.GetValueOrDefault("last_block_height", "?"))}\n" +
                                     $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Abci = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]abci_info: {Markup.Escape(ex.Message)}[/]");
                    }

                    sw.Restart();
                    try
                    {
                        var results = await _client.GetBlockResultsAsync(cancellationToken: stoppingToken);
                        sw.Stop();
                        state.BlockResults = $"Tx results: [bold]{results.Count}[/]\n" +
                                             $"Success: {results.Count(result => result.Code == 0)}  Errors: {results.Count(result => result.Code != 0)}\n" +
                                             $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.BlockResults = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]block_results: {Markup.Escape(ex.Message)}[/]");
                    }

                    sw.Restart();
                    try
                    {
                        var netInfo = await _client.GetNetInfoAsync(stoppingToken);
                        var mempool = await _client.GetUnconfirmedTxsAsync(cancellationToken: stoppingToken);
                        sw.Stop();
                        state.Network = $"Peers: [bold]{netInfo.PeerCount}[/]\n" +
                                        $"Listening: {netInfo.Listening}\n" +
                                        $"Mempool txs: [bold]{mempool.Count}[/] (total: {mempool.Total})\n" +
                                        $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Network = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]net_info: {Markup.Escape(ex.Message)}[/]");
                    }

                    if (_unsafeMode)
                    {
                        try
                        {
                            // Unsafe mode: probe dial_seeds with an empty list to verify the endpoint is reachable.
                            await _client.DialSeedsAsync([], stoppingToken);
                            state.Unsafe = $"[yellow]⚠ Unsafe mode active[/]\ndial_seeds: [green]✓ reachable[/]\n[dim]{ts:HH:mm:ss}[/]";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.Unsafe = $"[yellow]⚠ Unsafe mode active[/]\ndial_seeds: [red]{Markup.Escape(ex.Message)}[/]\n" +
                                           "[dim]Requires --rpc.unsafe=true on target node.[/]";
                        }
                    }

                    ctx.UpdateTarget(BuildLayout(state));
                    ctx.Refresh();

                    await Task.Delay(TimeSpan.FromSeconds(RefreshSeconds), stoppingToken);
                }
            });
    }

    private IRenderable BuildLayout(DashboardState s)
    {
        var rows = new List<IRenderable>
        {
            new Panel(new Markup("[bold cyan]CometBFT.Client.Demo.Rest[/]")) { Border = BoxBorder.Rounded },
            new Columns(
                new Panel(new Markup(s.Health)) { Header = new PanelHeader("Health / Status"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Block)) { Header = new PanelHeader("Latest Block"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.Validators)) { Header = new PanelHeader("Validators"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Abci)) { Header = new PanelHeader("ABCI Info"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.BlockResults)) { Header = new PanelHeader("Block Results"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Network)) { Header = new PanelHeader("Network / Mempool"), Border = BoxBorder.Rounded }),
        };

        if (_unsafeMode)
        {
            rows.Add(new Panel(new Markup(s.Unsafe)) { Header = new PanelHeader("Unsafe Endpoints (--unsafe)"), Border = BoxBorder.Rounded });
        }

        rows.Add(new Panel(new Markup(s.Log)) { Header = new PanelHeader("Log"), Border = BoxBorder.Rounded });
        return new Rows(rows);
    }
}

internal sealed class DashboardState
{
    private const int MaxLog = 20;
    private readonly Queue<string> _logQueue = new();

    public string Health { get; set; } = "[dim]…[/]";
    public string Block { get; set; } = "[dim]…[/]";
    public string Validators { get; set; } = "[dim]…[/]";
    public string Abci { get; set; } = "[dim]…[/]";
    public string BlockResults { get; set; } = "[dim]…[/]";
    public string Network { get; set; } = "[dim]…[/]";
    public string Unsafe { get; set; } = "[dim]Probing unsafe endpoint…[/]";
    public string Log => _logQueue.Count > 0 ? string.Join("\n", _logQueue.Reverse()) : "[dim](empty)[/]";

    public void AddLog(string line)
    {
        if (_logQueue.Count >= MaxLog)
        {
            _logQueue.Dequeue();
        }

        _logQueue.Enqueue(line);
    }
}
