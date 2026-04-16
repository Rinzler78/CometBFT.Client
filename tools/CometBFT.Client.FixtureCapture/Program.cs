/// <summary>
/// One-time fixture capture tool.
///
/// WebSocket mode (default):
///   Connects to a CometBFT WebSocket endpoint, subscribes to NewBlock / Tx / Vote events,
///   and saves the first matching raw JSON payloads as test fixtures.
///
/// REST mode:
///   Calls a set of CometBFT REST (HTTP) endpoints and saves the raw JSON responses.
///
/// Usage:
///   dotnet run --project tools/CometBFT.Client.FixtureCapture [ws-url] [output-dir]
///   dotnet run --project tools/CometBFT.Client.FixtureCapture rest [base-url] [output-dir]
///
/// Defaults:
///   ws-url     = wss://cosmoshub.tendermintrpc.lava.build:443/websocket
///   base-url   = https://cosmoshub.tendermintrpc.lava.build:443
///   output-dir = tests/CometBFT.Client.WebSocket.Tests/Fixtures  (WS)
///              = tests/CometBFT.Client.Rest.Tests/Fixtures       (REST)
/// </summary>

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// ── REST capture mode ─────────────────────────────────────────────────────────

if (args.Length > 0 && args[0] == "rest")
{
    var baseUrl = args.Length > 1
        ? args[1]
        : "https://cosmoshub.tendermintrpc.lava.build:443";

    var restOutputDir = args.Length > 2
        ? args[2]
        : Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "tests", "CometBFT.Client.Rest.Tests", "Fixtures"));

    Directory.CreateDirectory(restOutputDir);

    Console.WriteLine($"REST base URL : {baseUrl}");
    Console.WriteLine($"Output        : {restOutputDir}");
    Console.WriteLine();

    var endpoints = new (string Path, string Fixture)[]
    {
        ("/status",                                                        "rest_status.json"),
        ("/block?height=30674661",                                         "rest_block.json"),
        ("/block_results?height=30674661",                                 "rest_block_results.json"),
        ("/header?height=30674661",                                        "rest_header.json"),
        ("/validators?per_page=5",                                         "rest_validators.json"),
        ("/blockchain?minHeight=30674660&maxHeight=30674661",              "rest_blockchain.json"),
        ("/unconfirmed_txs?limit=5",                                       "rest_unconfirmed_txs.json"),
    };

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

    var restErrors = new List<string>();

    foreach (var (path, fixture) in endpoints)
    {
        Console.Write($"Fetching {path} … ");
        try
        {
            var url = baseUrl.TrimEnd('/') + path;
            var response = await httpClient.GetStringAsync(url);
            var pretty = RestPrettyPrint(response);
            File.WriteAllText(Path.Combine(restOutputDir, fixture), pretty, Encoding.UTF8);
            Console.WriteLine($"[OK] → {fixture}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {ex.Message}");
            restErrors.Add($"{fixture}: {ex.Message}");
        }
    }

    Console.WriteLine();
    if (restErrors.Count == 0)
    {
        Console.WriteLine("All REST fixtures captured successfully.");
    }
    else
    {
        Console.WriteLine("WARNING: some REST fixtures failed:");
        foreach (var e in restErrors)
            Console.WriteLine($"  - {e}");
        Environment.Exit(1);
    }

    return;

    static string RestPrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }
}

// ── WebSocket capture mode (default) ─────────────────────────────────────────

var wsUrl = args.Length > 0
    ? args[0]
    : "wss://cosmoshub.tendermintrpc.lava.build:443/websocket";

var outputDir = args.Length > 1
    ? args[1]
    : Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tests", "CometBFT.Client.WebSocket.Tests", "Fixtures"));

Directory.CreateDirectory(outputDir);

Console.WriteLine($"Endpoint : {wsUrl}");
Console.WriteLine($"Output   : {outputDir}");
Console.WriteLine();

// ── Capture state ────────────────────────────────────────────────────────────

var captured = new HashSet<string>();

bool NeedsNewBlock() => !captured.Contains("new_block_with_txs");
bool NeedsTx()       => !captured.Contains("tx_event");
bool NeedsVote()     => !captured.Contains("vote_event");
bool Done()          => !NeedsNewBlock() && !NeedsTx() && !NeedsVote();

// ── Connect ───────────────────────────────────────────────────────────────────

using var cts  = new CancellationTokenSource(TimeSpan.FromMinutes(5));
using var ws   = new ClientWebSocket();

Console.WriteLine("Connecting…");
await ws.ConnectAsync(new Uri(wsUrl), cts.Token);
Console.WriteLine("Connected.");

// ── Subscribe ─────────────────────────────────────────────────────────────────

await SendAsync(ws, Subscribe(1, "tm.event='NewBlock'"), cts.Token);
await SendAsync(ws, Subscribe(2, "tm.event='Tx'"), cts.Token);
await SendAsync(ws, Subscribe(3, "tm.event='Vote'"), cts.Token);

Console.WriteLine("Subscriptions sent. Waiting for events…");
Console.WriteLine();

// ── Receive loop ─────────────────────────────────────────────────────────────

var buffer = new byte[256 * 1024]; // 256 KB — enough for any block payload

while (!Done() && ws.State == WebSocketState.Open)
{
    cts.Token.ThrowIfCancellationRequested();

    var raw = await ReceiveFullMessageAsync(ws, buffer, cts.Token);
    if (raw is null)
        break;

    // Pretty-print for the file; parse minimally for routing.
    JsonNode? root;
    try
    {
        root = JsonNode.Parse(raw);
    }
    catch
    {
        continue;
    }

    var eventType = root?["result"]?["data"]?["type"]?.GetValue<string>();
    if (eventType is null)
        continue; // ACK or unknown

    var pretty = PrettyPrint(raw);

    switch (eventType)
    {
        case "tendermint/event/NewBlock" when NeedsNewBlock():
            // Only keep blocks that actually carry transactions.
            var txs = root?["result"]?["data"]?["value"]?["block"]?["data"]?["txs"]?.AsArray();
            if (txs is null || txs.Count == 0)
            {
                Console.WriteLine("NewBlock received but no txs — skipping, waiting for a block with txs…");
                continue;
            }

            Save("new_block_with_txs.json", pretty);
            captured.Add("new_block_with_txs");
            Console.WriteLine($"[OK] new_block_with_txs.json  ({txs.Count} tx(s) in block)");
            break;

        case "tendermint/event/Tx" when NeedsTx():
            Save("tx_event.json", pretty);
            captured.Add("tx_event");
            Console.WriteLine("[OK] tx_event.json");
            break;

        case "tendermint/event/Vote" when NeedsVote():
            Save("vote_event.json", pretty);
            captured.Add("vote_event");
            Console.WriteLine("[OK] vote_event.json");
            break;
    }
}

// ── Close ─────────────────────────────────────────────────────────────────────

if (ws.State == WebSocketState.Open)
{
    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
}

if (Done())
{
    Console.WriteLine();
    Console.WriteLine("All fixtures captured successfully.");
}
else
{
    Console.WriteLine();
    Console.WriteLine("WARNING: timed out before capturing all fixtures.");
    Console.WriteLine($"Missing: {string.Join(", ", new[] { "new_block_with_txs", "tx_event", "vote_event" }.Where(k => !captured.Contains(k)))}");
    Environment.Exit(1);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static string Subscribe(int id, string query) =>
    JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        method  = "subscribe",
        id,
        @params = new { query }
    });

static async Task SendAsync(ClientWebSocket ws, string message, CancellationToken ct)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
}

static async Task<string?> ReceiveFullMessageAsync(ClientWebSocket ws, byte[] buffer, CancellationToken ct)
{
    using var ms = new MemoryStream();
    WebSocketReceiveResult result;

    do
    {
        result = await ws.ReceiveAsync(buffer, ct);
        if (result.MessageType == WebSocketMessageType.Close)
            return null;

        ms.Write(buffer, 0, result.Count);
    }
    while (!result.EndOfMessage);

    return Encoding.UTF8.GetString(ms.ToArray());
}

static string PrettyPrint(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement,
            new JsonSerializerOptions { WriteIndented = true });
    }
    catch
    {
        return json;
    }
}

void Save(string fileName, string content)
{
    File.WriteAllText(Path.Combine(outputDir, fileName), content, Encoding.UTF8);
}
