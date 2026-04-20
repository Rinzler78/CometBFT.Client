using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Warning);
    })
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

                    // ── IHealthService + IStatusService ───────────────────────────
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
                        state.AddLog($"[red]health/status: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── IStatusService: consensus state / params ───────────────────
                    sw.Restart();
                    try
                    {
                        var consensusState = await _client.GetConsensusStateAsync(stoppingToken);
                        var consensusParams = await _client.GetConsensusParamsAsync(cancellationToken: stoppingToken);
                        sw.Stop();
                        state.Consensus =
                            $"Round: {Markup.Escape(consensusState.GetValueOrDefault("round_state/round", "?"))}\n" +
                            $"Step: {Markup.Escape(consensusState.GetValueOrDefault("round_state/step", "?"))}\n" +
                            $"MaxBytes: {consensusParams.BlockMaxBytes:N0}\n" +
                            $"MaxGas: {consensusParams.BlockMaxGas:N0}\n" +
                            $"MaxAgeDuration: {consensusParams.EvidenceMaxAgeDuration}\n" +
                            $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Consensus = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]consensus: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── IStatusService: consensus dump / genesis / mempool count ──
                    // Each call is isolated — relay may disable genesis or dump_consensus_state.
                    var dumpLine = "[dim]dump_consensus_state: N/A[/]";
                    try
                    {
                        var dump = await _client.DumpConsensusStateAsync(stoppingToken);
                        var peersJson = dump.GetValueOrDefault("peers", "");
                        var peerCount = string.IsNullOrEmpty(peersJson) ? 0
                            : peersJson.Split("node_address", StringSplitOptions.None).Length - 1;
                        dumpLine = $"dump_consensus_state: [green]✓ OK[/] ({peerCount} peers)";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    { state.AddLog($"[dim]dump: {Markup.Escape(FirstLine(ex.Message))}[/]"); }

                    var genesisLine = "[dim]genesis: N/A (endpoint disabled on relay)[/]";
                    try
                    {
                        var genesis = await _client.GetGenesisAsync(stoppingToken);
                        var chunk0 = await _client.GetGenesisChunkAsync(0, stoppingToken);
                        genesisLine =
                            $"Genesis chain: [yellow]{Markup.Escape(genesis.GetValueOrDefault("chain_id", "?"))}[/]\n" +
                            $"Genesis chunk 0: {chunk0.Data.Length} bytes";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    { state.AddLog($"[dim]genesis: {Markup.Escape(FirstLine(ex.Message))}[/]"); }

                    var mempoolCountLine = "[dim]num_unconfirmed_txs: N/A[/]";
                    try
                    {
                        var numTxs = await _client.GetNumUnconfirmedTxsAsync(stoppingToken);
                        mempoolCountLine = $"Mempool count: [bold]{numTxs.Total}[/] total / {numTxs.Count} returned";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    { state.AddLog($"[dim]num_unconfirmed_txs: {Markup.Escape(FirstLine(ex.Message))}[/]"); }

                    state.NodeMeta = $"{dumpLine}\n{genesisLine}\n{mempoolCountLine}\n[dim]{ts:HH:mm:ss}[/]";

                    // ── IBlockService: latest block ───────────────────────────────
                    string latestHash = string.Empty;
                    long latestHeight = 0;
                    sw.Restart();
                    try
                    {
                        var block = await _client.GetBlockAsync(cancellationToken: stoppingToken);
                        sw.Stop();
                        latestHash = block.Hash;
                        latestHeight = block.Height;
                        state.Block = $"Height: [bold]{block.Height:N0}[/]\n" +
                                      $"Hash: [dim]{Markup.Escape(block.Hash)}[/]\n" +
                                      $"Proposer: {Markup.Escape(block.Proposer)}\n" +
                                      $"Txs: {block.Txs.Count}\n" +
                                      $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.Block = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]block: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── IBlockService: header, commit, block-by-hash ───────────────
                    sw.Restart();
                    try
                    {
                        var header = await _client.GetHeaderAsync(cancellationToken: stoppingToken);
                        var commit = await _client.GetCommitAsync(cancellationToken: stoppingToken);
                        sw.Stop();

                        var headerByHash = "(skip — no hash yet)";
                        var blockByHash = "(skip — no hash yet)";
                        if (latestHash.Length > 0)
                        {
                            var hbh = await _client.GetHeaderByHashAsync(latestHash, stoppingToken);
                            headerByHash = $"h={hbh.Height} {hbh.ChainId}";
                            var bbh = await _client.GetBlockByHashAsync(latestHash, stoppingToken);
                            blockByHash = $"h={bbh.Height} txs={bbh.Txs.Count}";
                        }

                        state.BlockExtra =
                            $"Header h={header.Height} chain={Markup.Escape(header.ChainId)}\n" +
                            $"Commit: {Markup.Escape(commit.GetValueOrDefault("height", "?"))}\n" +
                            $"HeaderByHash: [dim]{Markup.Escape(headerByHash)}[/]\n" +
                            $"BlockByHash: [dim]{Markup.Escape(blockByHash)}[/]\n" +
                            $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.BlockExtra = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]block-extra: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── IBlockService: blockchain range + search blocks ────────────
                    sw.Restart();
                    try
                    {
                        var chain = await _client.GetBlockchainAsync(
                            minHeight: latestHeight > 10 ? latestHeight - 10 : null,
                            maxHeight: latestHeight > 0 ? latestHeight : null,
                            cancellationToken: stoppingToken);

                        var searched = await _client.SearchBlocksAsync(
                            "block.height > 0",
                            page: 1,
                            perPage: 3,
                            cancellationToken: stoppingToken);
                        sw.Stop();

                        state.BlockRange =
                            $"Blockchain headers returned: [bold]{chain.Headers.Count}[/]\n" +
                            $"SearchBlocks(\"block.height>0\") → {searched.Count} results\n" +
                            $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.BlockRange = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]block-range: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── IBlockService: block results ───────────────────────────────
                    string firstTxHash = string.Empty;
                    sw.Restart();
                    try
                    {
                        var results = await _client.GetBlockResultsAsync(cancellationToken: stoppingToken);
                        sw.Stop();
                        if (results.Count > 0 && results[0].Hash.Length > 0)
                            firstTxHash = results[0].Hash;
                        state.BlockResults = $"Tx results: [bold]{results.Count}[/]\n" +
                                             $"Success: {results.Count(r => r.Code == 0)}  Errors: {results.Count(r => r.Code != 0)}\n" +
                                             $"[dim]{ts:HH:mm:ss} · {sw.ElapsedMilliseconds} ms[/]";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        state.BlockResults = $"[red]{Markup.Escape(ex.Message)}[/]";
                        state.AddLog($"[red]block_results: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── ITxService: GetTxAsync + SearchTxAsync ─────────────────────
                    // Each call isolated — tx_search may be disabled on public relays.
                    var getTxLine = "(no tx hash available yet)";
                    if (firstTxHash.Length > 0)
                    {
                        try
                        {
                            sw.Restart();
                            var tx = await _client.GetTxAsync(firstTxHash, cancellationToken: stoppingToken);
                            sw.Stop();
                            var hashPfx = tx.Hash.Length > 0 ? tx.Hash[..Math.Min(16, tx.Hash.Length)] : "?";
                            getTxLine = $"[dim]{Markup.Escape(hashPfx)}…[/] code={tx.Code} gas={tx.GasUsed}/{tx.GasWanted}";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            getTxLine = $"[dim]N/A ({Markup.Escape(FirstLine(ex.Message))})[/]";
                            state.AddLog($"[dim]get_tx: {Markup.Escape(FirstLine(ex.Message))}[/]");
                        }
                    }

                    var searchTxLine = "[dim]tx_search: N/A (endpoint disabled on relay)[/]";
                    try
                    {
                        sw.Restart();
                        var searched = await _client.SearchTxAsync(
                            "tx.height > 0", page: 1, perPage: 3, cancellationToken: stoppingToken);
                        sw.Stop();
                        searchTxLine = $"SearchTxAsync: {searched.Count} results";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    { state.AddLog($"[dim]tx_search: {Markup.Escape(FirstLine(ex.Message))}[/]"); }

                    state.TxQuery =
                        $"GetTxAsync: {getTxLine}\n" +
                        $"{searchTxLine}\n" +
                        $"[dim]BroadcastTxAsync / BroadcastTxSyncAsync /[/]\n" +
                        $"[dim]BroadcastTxCommitAsync / CheckTxAsync /[/]\n" +
                        $"[dim]BroadcastEvidenceAsync: require signed tx bytes[/]\n" +
                        $"[dim]{ts:HH:mm:ss}[/]";

                    // ── IValidatorService ─────────────────────────────────────────
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
                        state.AddLog($"[red]validators: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── IAbciService: GetAbciInfoAsync + AbciQueryAsync ────────────
                    // Isolated — abci_query may be disabled on public relays.
                    var abciInfoLine = "[dim]abci_info: N/A[/]";
                    try
                    {
                        sw.Restart();
                        var abciInfo = await _client.GetAbciInfoAsync(stoppingToken);
                        sw.Stop();
                        abciInfoLine =
                            $"Version: {Markup.Escape(abciInfo.GetValueOrDefault("version", "?"))}\n" +
                            $"Last h: {Markup.Escape(abciInfo.GetValueOrDefault("last_block_height", "?"))}";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    { state.AddLog($"[dim]abci_info: {Markup.Escape(FirstLine(ex.Message))}[/]"); }

                    var abciQueryLine = "[dim]abci_query: N/A (endpoint disabled on relay)[/]";
                    try
                    {
                        sw.Restart();
                        var abciQuery = await _client.AbciQueryAsync("/app/version", "", cancellationToken: stoppingToken);
                        sw.Stop();
                        abciQueryLine = $"AbciQuery(/app/version): {Markup.Escape(abciQuery.GetValueOrDefault("response/value", "(empty)"))}";
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    { state.AddLog($"[dim]abci_query: {Markup.Escape(FirstLine(ex.Message))}[/]"); }

                    state.Abci = $"{abciInfoLine}\n{abciQueryLine}\n[dim]{ts:HH:mm:ss}[/]";

                    // ── IStatusService: net info + mempool ────────────────────────
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
                        state.AddLog($"[red]net_info: {Markup.Escape(FirstLine(ex.Message))}[/]");
                    }

                    // ── IUnsafeService (--unsafe) ─────────────────────────────────
                    if (_unsafeMode)
                    {
                        try
                        {
                            await _client.DialSeedsAsync([], stoppingToken);
                            await _client.DialPeersAsync([], persistent: false, unconditional: false, isPrivate: false, stoppingToken);
                            state.Unsafe = $"[yellow]⚠ Unsafe mode active[/]\n" +
                                           $"dial_seeds: [green]✓ reachable[/]\n" +
                                           $"dial_peers: [green]✓ reachable[/]\n" +
                                           $"[dim]{ts:HH:mm:ss}[/]";
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            state.Unsafe = $"[yellow]⚠ Unsafe mode active[/]\n" +
                                           $"[red]{Markup.Escape(ex.Message)}[/]\n" +
                                           "[dim]Requires --rpc.unsafe=true on target node.[/]";
                        }
                    }

                    ctx.UpdateTarget(BuildLayout(state));
                    ctx.Refresh();

                    await Task.Delay(TimeSpan.FromSeconds(RefreshSeconds), stoppingToken);
                }
            });
    }

    private static string FirstLine(string? s, int maxLen = 120)
    {
        if (string.IsNullOrEmpty(s)) return "(no detail)";
        var line = s.Split('\n', 2)[0].Trim();
        return line.Length > maxLen ? line[..maxLen] + "…" : line;
    }

    private IRenderable BuildLayout(DashboardState s)
    {
        var rows = new List<IRenderable>
        {
            new Panel(new Markup("[bold cyan]CometBFT.Client.Demo.Rest[/]  [dim](IHealthService + IStatusService + IBlockService + ITxService + IValidatorService + IAbciService + IUnsafeService)[/]")) { Border = BoxBorder.Rounded },
            new Columns(
                new Panel(new Markup(s.Health)) { Header = new PanelHeader("Health / Status"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Consensus)) { Header = new PanelHeader("Consensus State / Params"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.NodeMeta)) { Header = new PanelHeader("Dump / Genesis / Mempool Count"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Network)) { Header = new PanelHeader("Net Info / Mempool"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.Block)) { Header = new PanelHeader("Latest Block"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.BlockExtra)) { Header = new PanelHeader("Header / Commit / By Hash"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.BlockRange)) { Header = new PanelHeader("Blockchain Range / Search Blocks"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.BlockResults)) { Header = new PanelHeader("Block Results"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.TxQuery)) { Header = new PanelHeader("Tx Query / Broadcast (N/A)"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Validators)) { Header = new PanelHeader("Validators"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.Abci)) { Header = new PanelHeader("ABCI Info / Query"), Border = BoxBorder.Rounded },
                new Panel(new Markup("[dim]BroadcastTxAsync, BroadcastTxSyncAsync,[/]\n[dim]BroadcastTxCommitAsync, CheckTxAsync,[/]\n[dim]BroadcastEvidenceAsync[/]\n\n[yellow]Require signed tx bytes —[/]\n[yellow]see integration tests.[/]")) { Header = new PanelHeader("Tx Broadcast (N/A in public demo)"), Border = BoxBorder.Rounded }),
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
    public string Consensus { get; set; } = "[dim]…[/]";
    public string NodeMeta { get; set; } = "[dim]…[/]";
    public string Block { get; set; } = "[dim]…[/]";
    public string BlockExtra { get; set; } = "[dim]…[/]";
    public string BlockRange { get; set; } = "[dim]…[/]";
    public string BlockResults { get; set; } = "[dim]…[/]";
    public string TxQuery { get; set; } = "[dim]…[/]";
    public string Validators { get; set; } = "[dim]…[/]";
    public string Abci { get; set; } = "[dim]…[/]";
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
