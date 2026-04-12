using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;

// ── Config ───────────────────────────────────────────────────────────────────
var wsUrl = args.FirstOrDefault(a => a.StartsWith("--ws-url=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1]
            ?? Environment.GetEnvironmentVariable("COMETBFT_WS_URL")
            ?? "wss://cosmoshub.tendermintrpc.lava.build:443/websocket";

// ── DI ───────────────────────────────────────────────────────────────────────
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddCometBftWebSocket(o => o.BaseUrl = wsUrl);
        services.AddHostedService<DashboardService>();
    })
    .Build();

await host.RunAsync();
return 0;

// ── Dashboard hosted service ─────────────────────────────────────────────────
internal sealed class DashboardService : BackgroundService
{
    private const int MaxEntries = 20;

    private readonly ICometBftWebSocketClient _ws;
    private readonly WsState _state = new();

    public DashboardService(ICometBftWebSocketClient ws) => _ws = ws;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ws.NewBlockReceived += OnNewBlock;
        _ws.NewBlockHeaderReceived += OnNewBlockHeader;
        _ws.TxExecuted += OnTx;
        _ws.VoteReceived += OnVote;
        _ws.ValidatorSetUpdated += OnValidatorSetUpdated;

        try
        {
            await _ws.ConnectAsync(stoppingToken);
            await _ws.SubscribeNewBlockAsync(stoppingToken);
            await _ws.SubscribeNewBlockHeaderAsync(stoppingToken);
            await _ws.SubscribeTxAsync(stoppingToken);
            await _ws.SubscribeVoteAsync(stoppingToken);
            await _ws.SubscribeValidatorSetUpdatesAsync(stoppingToken);
            _state.AddLog($"[green]Connected · subscribed to NewBlock, NewBlockHeader, Tx, Vote, ValidatorSetUpdates[/]");
        }
        catch (Exception ex)
        {
            _state.AddLog($"[red]Connection failed: {Markup.Escape(ex.Message)}[/]");
        }

        await AnsiConsole.Live(BuildLayout(_state))
            .StartAsync(async ctx =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ctx.UpdateTarget(BuildLayout(_state));
                    ctx.Refresh();
                    await Task.Delay(500, stoppingToken);
                }
            });

        _ws.NewBlockReceived -= OnNewBlock;
        _ws.NewBlockHeaderReceived -= OnNewBlockHeader;
        _ws.TxExecuted -= OnTx;
        _ws.VoteReceived -= OnVote;
        _ws.ValidatorSetUpdated -= OnValidatorSetUpdated;
        await _ws.DisposeAsync();
    }

    private void OnNewBlock(object? sender, TendermintEventArgs<Block> e)
    {
        var block = e.Value;
        var addr = block.Proposer.Length > 0
            ? block.Proposer[..Math.Min(16, block.Proposer.Length)]
            : "?";
        _state.PrependBlock($"[bold]{block.Height:N0}[/] {block.Time:HH:mm:ss} proposer=[dim]{Markup.Escape(addr)}[/] txs={block.Txs.Count}");
    }

    private void OnTx(object? sender, TendermintEventArgs<TxResult> e)
    {
        var tx = e.Value;
        var hashPfx = tx.Hash.Length > 0 ? tx.Hash[..Math.Min(16, tx.Hash.Length)] : "?";
        _state.PrependTx($"[dim]{Markup.Escape(hashPfx)}…[/] h={tx.Height} code={tx.Code} gas={tx.GasUsed}/{tx.GasWanted}");
    }

    private void OnVote(object? sender, TendermintEventArgs<Vote> e)
    {
        var vote = e.Value;
        var addrPfx = vote.ValidatorAddress.Length > 0
            ? vote.ValidatorAddress[..Math.Min(16, vote.ValidatorAddress.Length)]
            : "?";
        _state.AddLog($"[dim]VOTE[/] h={vote.Height} r={vote.Round} val=[dim]{Markup.Escape(addrPfx)}[/]");
    }

    private void OnNewBlockHeader(object? sender, TendermintEventArgs<BlockHeader> e)
    {
        var header = e.Value;
        _state.Header = $"Height: [bold]{header.Height:N0}[/]\nChain: {Markup.Escape(header.ChainId)}\nTime: {header.Time:HH:mm:ss}\nProposer: [dim]{Markup.Escape(header.ProposerAddress)}[/]";
    }

    private void OnValidatorSetUpdated(object? sender, TendermintEventArgs<IReadOnlyList<Validator>> e)
    {
        var validators = e.Value;
        _state.ValidatorUpdates = $"Validators in update: [bold]{validators.Count}[/]\n" +
                                  string.Join("\n", validators.Take(3).Select(v => $"[dim]{Markup.Escape(v.Address[..Math.Min(16, v.Address.Length)])}…[/] power={v.VotingPower}"));
    }

    private static IRenderable BuildLayout(WsState s) =>
        new Rows(
            new Panel(new Markup("[bold cyan]CometBFT.Client.Demo.WebSocket[/]")) { Border = BoxBorder.Rounded },
            new Columns(
                new Panel(new Markup(s.Blocks)) { Header = new PanelHeader("Live Blocks"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.Txs)) { Header = new PanelHeader("Live Transactions"), Border = BoxBorder.Rounded }),
            new Columns(
                new Panel(new Markup(s.Header)) { Header = new PanelHeader("Latest Header"), Border = BoxBorder.Rounded },
                new Panel(new Markup(s.ValidatorUpdates)) { Header = new PanelHeader("Validator Set Updates"), Border = BoxBorder.Rounded }),
            new Panel(new Markup(s.Log)) { Header = new PanelHeader("Log"), Border = BoxBorder.Rounded });
}

internal sealed class WsState
{
    private const int MaxItems = 20;
    private readonly LinkedList<string> _blocks = new();
    private readonly LinkedList<string> _txs = new();
    private readonly Queue<string> _log = new();

    public string Blocks => _blocks.Count > 0 ? string.Join("\n", _blocks) : "[dim]Waiting for NewBlock events…[/]";
    public string Txs => _txs.Count > 0 ? string.Join("\n", _txs) : "[dim]Waiting for Tx events…[/]";
    public string Header { get; set; } = "[dim]Waiting for NewBlockHeader events…[/]";
    public string ValidatorUpdates { get; set; } = "[dim]Waiting for ValidatorSetUpdates events…[/]";
    public string Log => _log.Count > 0 ? string.Join("\n", _log.Reverse()) : "[dim](no log entries)[/]";

    public void PrependBlock(string entry) => PrependTo(_blocks, entry);
    public void PrependTx(string entry) => PrependTo(_txs, entry);

    public void AddLog(string line)
    {
        if (_log.Count >= MaxItems)
        {
            _log.Dequeue();
        }

        _log.Enqueue(line);
    }

    private static void PrependTo(LinkedList<string> list, string entry)
    {
        list.AddFirst(entry);
        while (list.Count > MaxItems)
        {
            list.RemoveLast();
        }
    }
}
