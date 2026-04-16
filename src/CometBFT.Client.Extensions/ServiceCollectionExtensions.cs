using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using CometBFT.Client.Core.Codecs;
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

        // Validate eagerly at registration time using a temporary instance.
        var tempOptions = new CometBftRestOptions();
        configure(tempOptions);
        tempOptions.Validate();

        services.Configure<CometBftRestOptions>(configure);

        services
            .AddHttpClient<ICometBftRestClient, CometBftRestClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<CometBftRestOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                // Disable HttpClient-level timeout: Polly TimeoutAsync governs per-attempt timeouts.
                // A fixed HttpClient.Timeout would absorb the full retry backoff and fire as
                // TaskCanceledException before Polly can exhaust its retry count.
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            })
            // Polly policies use values captured from the validated temp options at registration time.
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(tempOptions.Timeout))
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: tempOptions.MaxRetryAttempts,
                    sleepDurationProvider: attempt =>
                    {
                        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                        return TimeSpan.FromTicks((long)(tempOptions.RetryDelay.Ticks * Math.Pow(2, attempt - 1))) + jitter;
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

        // Validate eagerly at registration time using a temporary instance.
        var tempOptions = new CometBftWebSocketOptions();
        configure(tempOptions);
        tempOptions.Validate();

        services.Configure<CometBftWebSocketOptions>(configure);
        services.AddSingleton<ICometBftWebSocketClient, CometBftWebSocketClient>();

        return services;
    }

    /// <summary>
    /// Registers a typed CometBFT WebSocket subscription client that decodes
    /// transactions into <typeparamref name="TTx"/> using the provided codec.
    /// </summary>
    /// <typeparam name="TTx">The application-specific transaction type.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftWebSocketOptions"/>.</param>
    /// <param name="codec">The codec used to decode transaction bytes into <typeparamref name="TTx"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/>, <paramref name="configure"/>,
    /// or <paramref name="codec"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Registers <see cref="ICometBftWebSocketClient{TTx}"/> as a singleton.
    /// The raw <see cref="ICometBftWebSocketClient"/> is NOT registered by this overload;
    /// call <see cref="AddCometBftWebSocket"/> separately if both are needed.
    /// </remarks>
    public static IServiceCollection AddCometBftWebSocket<TTx>(
        this IServiceCollection services,
        Action<CometBftWebSocketOptions> configure,
        ITxCodec<TTx> codec)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(codec);

        var tempOptions = new CometBftWebSocketOptions();
        configure(tempOptions);
        tempOptions.Validate();

        services.Configure<CometBftWebSocketOptions>(configure);
        services.AddSingleton(codec);
        services.AddSingleton<ICometBftWebSocketClient<TTx>>(sp =>
            new CometBftWebSocketClient<TTx>(
                sp.GetRequiredService<IOptions<CometBftWebSocketOptions>>(),
                sp.GetRequiredService<ITxCodec<TTx>>()));

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

        // Validate eagerly at registration time using a temporary instance.
        var tempOptions = new CometBftGrpcOptions();
        configure(tempOptions);
        tempOptions.Validate();

        services.Configure<CometBftGrpcOptions>(configure);
        services.AddSingleton<ICometBftGrpcClient, CometBftGrpcClient>();

        return services;
    }

    /// <summary>
    /// Registers the Cosmos SDK gRPC client (<c>cosmos.base.tendermint.v1beta1.Service</c>)
    /// and its dependencies.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftSdkGrpcOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    public static IServiceCollection AddCometBftSdkGrpc(
        this IServiceCollection services,
        Action<CometBftSdkGrpcOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Validate eagerly at registration time using a temporary instance.
        var tempOptions = new CometBftSdkGrpcOptions();
        configure(tempOptions);
        tempOptions.Validate();

        services.Configure<CometBftSdkGrpcOptions>(configure);
        services.AddSingleton<ICometBftSdkGrpcClient, CometBftSdkGrpcClient>();

        return services;
    }
}
