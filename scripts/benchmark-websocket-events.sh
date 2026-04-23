#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OLD_REF="${1:-v2.0.0}"
NEW_REF="${2:-v2.1.0}"
ITERATIONS="${3:-1}"
ACK_TIMEOUT_SECONDS="${ACK_TIMEOUT_SECONDS:-5}"
EVENT_TIMEOUT_SECONDS="${EVENT_TIMEOUT_SECONDS:-10}"
OVERALL_TIMEOUT_SECONDS="${OVERALL_TIMEOUT_SECONDS:-55}"
WS_URL="${COMETBFT_WS_URL:-wss://cosmoshub.tendermintrpc.lava.build:443/websocket}"

TMP_DIR="$(mktemp -d)"
cleanup() {
  if [[ -e "${TMP_DIR}/old-wt/.git" ]]; then
    git -C "${ROOT_DIR}" worktree remove --force "${TMP_DIR}/old-wt" >/dev/null 2>&1 || true
  fi
  if [[ -e "${TMP_DIR}/new-wt/.git" ]]; then
    git -C "${ROOT_DIR}" worktree remove --force "${TMP_DIR}/new-wt" >/dev/null 2>&1 || true
  fi
  rm -rf "${TMP_DIR}"
}
trap cleanup EXIT

git -C "${ROOT_DIR}" worktree add --detach "${TMP_DIR}/old-wt" "${OLD_REF}" >/dev/null
git -C "${ROOT_DIR}" worktree add --detach "${TMP_DIR}/new-wt" "${NEW_REF}" >/dev/null

create_project() {
  local worktree="$1"
  local project_name="$2"
  mkdir -p "${worktree}/.benchmarks"
  cat > "${worktree}/.benchmarks/${project_name}.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../src/CometBFT.Client.Extensions/CometBFT.Client.Extensions.csproj" />
  </ItemGroup>
</Project>
EOF
}

create_program() {
  local worktree="$1"
  local version_label="$2"
  local include_new_block_events="$3"
  local scenario_add=""
  local event_hook=""
  local subscribe_case=""

  if [[ "${include_new_block_events}" == "true" ]]; then
    scenario_add='scenarios.Add("new_block_events");'
    event_hook=$'            case "new_block_events":\n                streamSubscription = client.NewBlockEventsStream.Subscribe(args =>\n                {\n                    if (args.Height > 0) eventTcs.TrySetResult(sendSw.ElapsedMilliseconds);\n                });\n                break;'
    subscribe_case=$'            case "new_block_events":\n                await client.SubscribeNewBlockEventsAsync();\n                break;'
  fi

  cat > "${worktree}/.benchmarks/Program.cs" <<EOF
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Extensions;

var wsUrl = Environment.GetEnvironmentVariable("COMETBFT_WS_URL")!;
var iterations = int.Parse(Environment.GetEnvironmentVariable("BENCH_ITERATIONS")!);
var ackTimeoutSeconds = int.Parse(Environment.GetEnvironmentVariable("BENCH_ACK_TIMEOUT_SECONDS")!);
var eventTimeoutSeconds = int.Parse(Environment.GetEnvironmentVariable("BENCH_EVENT_TIMEOUT_SECONDS")!);
var scenarios = new List<string> { "new_block", "new_block_header", "tx", "vote" };
${scenario_add}

var results = await Task.WhenAll(scenarios.Select(BenchmarkScenarioAsync));
Console.WriteLine("version,event,iterations,avg_subscribe_call_ms,avg_first_event_from_send_ms,avg_first_event_from_return_ms,ack_timeouts,events_received,errors");
foreach (var result in results)
{
    Console.WriteLine(result);
}

async Task<string> BenchmarkScenarioAsync(string scenario)
{
    var subscribeCallTimes = new List<long>();
    var eventFromSendTimes = new List<long>();
    var eventFromReturnTimes = new List<long>();
    var ackTimeouts = 0;
    var eventsReceived = 0;
    var errors = new List<string>();

    for (var i = 0; i < iterations; i++)
    {
        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options =>
        {
            options.BaseUrl = wsUrl;
            options.SubscribeAckTimeout = TimeSpan.FromSeconds(ackTimeoutSeconds);
            options.ReconnectTimeout = TimeSpan.FromSeconds(5);
            options.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ICometBftWebSocketClient>();
        Exception? lastError = null;
        var eventTcs = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? streamSubscription = null;

        client.ErrorOccurred += (_, args) =>
        {
            lastError = args.Value;
            if (args.Value.Message.Contains("ACK", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref ackTimeouts);
            }
        };

        var sendSw = new Stopwatch();
        switch (scenario)
        {
            case "new_block":
                client.NewBlockReceived += (_, args) =>
                {
                    if (args.Value.Height > 0) eventTcs.TrySetResult(sendSw.ElapsedMilliseconds);
                };
                break;
            case "new_block_header":
                client.NewBlockHeaderReceived += (_, args) =>
                {
                    if (args.Value.Height > 0) eventTcs.TrySetResult(sendSw.ElapsedMilliseconds);
                };
                break;
            case "tx":
                client.TxExecuted += (_, args) =>
                {
                    if (args.Value.Height > 0) eventTcs.TrySetResult(sendSw.ElapsedMilliseconds);
                };
                break;
            case "vote":
                client.VoteReceived += (_, args) =>
                {
                    if (args.Value.Height > 0 && args.Value.ValidatorAddress.Length > 0)
                        eventTcs.TrySetResult(sendSw.ElapsedMilliseconds);
                };
                break;
${event_hook}
        }

        await client.ConnectAsync();

        sendSw.Start();
        switch (scenario)
        {
            case "new_block":
                await client.SubscribeNewBlockAsync();
                break;
            case "new_block_header":
                await client.SubscribeNewBlockHeaderAsync();
                break;
            case "tx":
                await client.SubscribeTxAsync();
                break;
            case "vote":
                await client.SubscribeVoteAsync();
                break;
${subscribe_case}
        }
        var subscribeCallMs = sendSw.ElapsedMilliseconds;
        subscribeCallTimes.Add(subscribeCallMs);

        try
        {
            var eventFromSendMs = await eventTcs.Task.WaitAsync(TimeSpan.FromSeconds(eventTimeoutSeconds));
            eventFromSendTimes.Add(eventFromSendMs);
            eventFromReturnTimes.Add(Math.Max(0, eventFromSendMs - subscribeCallMs));
            eventsReceived++;
        }
        catch (Exception ex)
        {
            errors.Add($"iter={i + 1}:{lastError?.Message ?? ex.Message}");
        }

        streamSubscription?.Dispose();
        await client.DisconnectAsync();
    }

    return $"${version_label},{scenario},{iterations},{Average(subscribeCallTimes)},{Average(eventFromSendTimes)},{Average(eventFromReturnTimes)},{ackTimeouts},{eventsReceived},\"{string.Join(" | ", errors)}\"";
}

static string Average(IReadOnlyList<long> values) =>
    values.Count == 0 ? "n/a" : values.Average().ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
EOF
}

create_project "${TMP_DIR}/old-wt" "cometbench-old"
create_program "${TMP_DIR}/old-wt" "${OLD_REF}" "false"
create_project "${TMP_DIR}/new-wt" "cometbench-new"
create_program "${TMP_DIR}/new-wt" "${NEW_REF}" "true"

run_benchmark() {
  local worktree="$1"
  local project_name="$2"
  local output_file="$3"
  (
    cd "${worktree}/.benchmarks"
    env \
      COMETBFT_WS_URL="${WS_URL}" \
      BENCH_ITERATIONS="${ITERATIONS}" \
      BENCH_ACK_TIMEOUT_SECONDS="${ACK_TIMEOUT_SECONDS}" \
      BENCH_EVENT_TIMEOUT_SECONDS="${EVENT_TIMEOUT_SECONDS}" \
      timeout "${OVERALL_TIMEOUT_SECONDS}" \
      dotnet run --project "${project_name}.csproj" -c Release > "${output_file}"
  )
}

run_benchmark "${TMP_DIR}/old-wt" "cometbench-old" "${TMP_DIR}/old.csv" &
OLD_PID=$!
run_benchmark "${TMP_DIR}/new-wt" "cometbench-new" "${TMP_DIR}/new.csv" &
NEW_PID=$!
wait "${OLD_PID}"
wait "${NEW_PID}"

cat "${TMP_DIR}/old.csv"
tail -n +2 "${TMP_DIR}/new.csv"
