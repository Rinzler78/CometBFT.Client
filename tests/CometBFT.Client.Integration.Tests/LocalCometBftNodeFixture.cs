using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Extensions;
using Xunit;

namespace CometBFT.Client.Integration.Tests;

public sealed class LocalCometBftNodeFixture : IAsyncLifetime
{
    private const string DefaultDockerImage = "cometbft/cometbft:v0.38.17";
    private const int RpcPort = 26657;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private string? _containerName;

    public string RpcUrl { get; private set; } = string.Empty;
    public string WsUrl => $"ws://127.0.0.1:{RpcPort}/websocket";
    public string? GrpcUrl { get; private set; }

    public string? SkipReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _containerName = $"cometbft-client-tests-{Guid.NewGuid():N}";
            var image = Environment.GetEnvironmentVariable("COMETBFT_DOCKER_IMAGE") ?? DefaultDockerImage;
            var startupCommand =
                "cometbft init " +
                "&& sed -i 's/^timeout_commit = .*/timeout_commit = \"1s\"/' /cometbft/config/config.toml " +
                "&& exec cometbft start --proxy_app=kvstore " +
                "--rpc.laddr tcp://0.0.0.0:26657 --p2p.laddr tcp://0.0.0.0:26656";

            await RunDockerAsync([
                "run", "-d", "--rm", "--name", _containerName,
                "--entrypoint", "sh",
                "-p", $"127.0.0.1:{RpcPort}:{RpcPort}",
                image,
                "-lc", startupCommand
            ]).ConfigureAwait(false);

            RpcUrl = $"http://127.0.0.1:{RpcPort}";
            GrpcUrl = null;

            await WaitForRpcAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SkipReason = $"Local CometBFT node unavailable for IntegrationLocal tests. {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_containerName))
        {
            try
            {
                await RunDockerAsync(["rm", "-f", _containerName], throwOnError: false).ConfigureAwait(false);
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        _httpClient.Dispose();
    }

    public ServiceProvider CreateRestProvider()
    {
        var services = new ServiceCollection();
        services.AddCometBftRest(options =>
        {
            options.BaseUrl = RpcUrl;
            options.Timeout = TimeSpan.FromSeconds(5);
            options.MaxRetryAttempts = 0;
            options.RetryDelay = TimeSpan.Zero;
        });

        return services.BuildServiceProvider();
    }

    public ServiceProvider CreateGrpcProvider()
    {
        var services = new ServiceCollection();
        services.AddCometBftGrpc(options =>
        {
            options.BaseUrl = GrpcUrl ?? throw new InvalidOperationException("Local CometBFT gRPC endpoint is unavailable.");
            options.Timeout = TimeSpan.FromSeconds(5);
            options.Protocol = GrpcProtocol.CometBft;
        });

        return services.BuildServiceProvider();
    }

    public ServiceProvider CreateWebSocketProvider()
    {
        var services = new ServiceCollection();
        services.AddCometBftWebSocket(options =>
        {
            options.BaseUrl = WsUrl;
            options.SubscribeAckTimeout = TimeSpan.FromSeconds(2);
            options.ReconnectTimeout = TimeSpan.FromSeconds(1);
            options.ErrorReconnectTimeout = TimeSpan.FromSeconds(1);
        });

        return services.BuildServiceProvider();
    }

    private async Task WaitForRpcAsync()
    {
        // Require two consecutive successful health responses 1 s apart to confirm the node
        // is stable and the RPC server is fully initialized (not just the TCP port open).
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        int consecutiveOk = 0;
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"{RpcUrl}/health", timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    consecutiveOk++;
                    if (consecutiveOk >= 2)
                        return;
                }
                else
                {
                    consecutiveOk = 0;
                }
            }
            catch
            {
                consecutiveOk = 0;
            }

            await Task.Delay(500, timeout.Token).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out while waiting for the local CometBFT node to become healthy.");
    }

    private static async Task<string> RunDockerAsync(string[] arguments, bool throwOnError = true)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker process.");

        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker command failed with exit code {process.ExitCode}.{Environment.NewLine}{stderr}{stdout}");
        }

        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
    }
}
