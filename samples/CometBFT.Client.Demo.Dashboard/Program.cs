using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Extensions;
using CometBFT.Client.Demo.Dashboard;
using CometBFT.Client.Demo.Dashboard.Services;
using CometBFT.Client.Demo.Dashboard.ViewModels;
using CometBFT.Client.Demo.Shared;

// ── CLI / env overrides ───────────────────────────────────────────────────────

static string Arg(string[] args, string flag, string envVar, string fallback)
{
    var fromArgs = args.FirstOrDefault(a =>
        a.StartsWith($"--{flag}=", StringComparison.OrdinalIgnoreCase))
        ?.Split('=', 2)[1];
    return fromArgs
        ?? Environment.GetEnvironmentVariable(envVar)
        ?? fallback;
}

var rpcUrl = Arg(args, "rpc-url", "COMETBFT_RPC_URL", DemoDefaults.RpcUrl);
var grpcUrl = Arg(args, "grpc-url", "COMETBFT_GRPC_URL", DemoDefaults.GrpcUrl);
var wsUrl = Arg(args, "ws-url", "COMETBFT_WS_URL", DemoDefaults.WsUrl);

// ── Build IHost ───────────────────────────────────────────────────────────────

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Warning);
    })
    .ConfigureServices(services =>
    {
        services.AddCometBftClient(o =>
        {
            o.RestBaseUrl = rpcUrl;
            o.GrpcBaseUrl = grpcUrl;
            o.WebSocketBaseUrl = wsUrl;
        });

        services.AddSingleton<MainWindowViewModel>();
        services.AddHostedService<DashboardBackgroundService>();
    })
    .Build();

// ── Wire Avalonia ─────────────────────────────────────────────────────────────

App.Services = host.Services;

// Start the background host (WS + periodic refresh) before Avalonia pumps the event loop.
_ = host.StartAsync();

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);

await host.StopAsync();
return 0;
