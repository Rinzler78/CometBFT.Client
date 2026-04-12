using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Grpc;
using CometBFT.Client.Rest;
using CometBFT.Client.WebSocket;

namespace CometBFT.Client.Extensions;

/// <summary>
/// Extension methods for registering CometBFT client services with the .NET dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CometBFT REST/JSON-RPC 2.0 client and its dependencies.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftRestOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    public static IServiceCollection AddCometBftRest(
        this IServiceCollection services,
        Action<CometBftRestOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new CometBftRestOptions();
        configure(options);

        services.AddSingleton(options);

        services
            .AddHttpClient<ICometBftRestClient, CometBftRestClient>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                // Disable HttpClient-level timeout: Polly TimeoutAsync governs per-attempt timeouts.
                // A fixed HttpClient.Timeout would absorb the full retry backoff and fire as
                // TaskCanceledException before Polly can exhaust its retry count.
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            })
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(options.Timeout))
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: options.MaxRetryAttempts,
                    sleepDurationProvider: attempt =>
                    {
                        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                        return TimeSpan.FromTicks((long)(options.RetryDelay.Ticks * Math.Pow(2, attempt - 1))) + jitter;
                    }))
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30)));

        return services;
    }

    /// <summary>
    /// Registers the CometBFT WebSocket subscription client and its dependencies.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftWebSocketOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    public static IServiceCollection AddCometBftWebSocket(
        this IServiceCollection services,
        Action<CometBftWebSocketOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new CometBftWebSocketOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<ICometBftWebSocketClient, CometBftWebSocketClient>();

        return services;
    }

    /// <summary>
    /// Registers the CometBFT gRPC client and its dependencies.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftGrpcOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    public static IServiceCollection AddCometBftGrpc(
        this IServiceCollection services,
        Action<CometBftGrpcOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new CometBftGrpcOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<ICometBftGrpcClient, CometBftGrpcClient>();

        return services;
    }
}
