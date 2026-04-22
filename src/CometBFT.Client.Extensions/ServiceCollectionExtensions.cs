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
    /// Registers a CometBFT REST/JSON-RPC 2.0 client using fully-typed domain parameters
    /// and a custom interface and implementation.
    /// </summary>
    /// <typeparam name="TBlock">The block type. Must inherit <see cref="Core.Domain.BlockBase"/>.</typeparam>
    /// <typeparam name="TTxResult">The transaction result type. Must inherit <see cref="Core.Domain.TxResultBase"/>.</typeparam>
    /// <typeparam name="TValidator">The validator type. Must inherit <see cref="Core.Domain.Validator"/>.</typeparam>
    /// <typeparam name="TInterface">
    /// The service interface to register. Must implement
    /// <see cref="ICometBftRestClient{TBlock,TTxResult,TValidator}"/>.
    /// </typeparam>
    /// <typeparam name="TClient">
    /// The concrete implementation. Must implement <typeparamref name="TInterface"/>.
    /// </typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftRestOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Use this overload when a downstream layer extends the domain types:
    /// <code>
    /// services.AddCometBftRest&lt;CosmosBlock&lt;string&gt;, TxResult, Validator,
    ///     ICosmosRestClient, CosmosRestClient&gt;(o =&gt; o.BaseUrl = "…");
    /// </code>
    /// The same Polly retry/circuit-breaker pipeline is applied regardless of the type arguments.
    /// </remarks>
    public static IServiceCollection AddCometBftRest<TBlock, TTxResult, TValidator, TInterface, TClient>(
        this IServiceCollection services,
        Action<CometBftRestOptions> configure)
        where TBlock : Core.Domain.BlockBase
        where TTxResult : Core.Domain.TxResultBase
        where TValidator : Core.Domain.Validator
        where TInterface : class, ICometBftRestClient<TBlock, TTxResult, TValidator>
        where TClient : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Validate eagerly at registration time using a temporary instance.
        var tempOptions = new CometBftRestOptions();
        configure(tempOptions);
        tempOptions.Validate();

        services.Configure<CometBftRestOptions>(configure);

        services
            .AddHttpClient<TInterface, TClient>((sp, client) =>
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
    /// Registers the CometBFT REST/JSON-RPC 2.0 client using a custom interface and
    /// implementation that use the default domain types (<see cref="Core.Domain.Block"/>,
    /// <see cref="Core.Domain.TxResult"/>, <see cref="Core.Domain.Validator"/>).
    /// </summary>
    /// <typeparam name="TInterface">
    /// The service interface to register. Must implement <see cref="ICometBftRestClient"/>.
    /// </typeparam>
    /// <typeparam name="TClient">
    /// The concrete implementation. Must implement <typeparamref name="TInterface"/>.
    /// </typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftRestOptions"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Use this overload when the consumer interface extends <see cref="ICometBftRestClient"/>
    /// (non-generic shim) without changing the domain types. For custom domain types use
    /// <see cref="AddCometBftRest{TBlock,TTxResult,TValidator,TInterface,TClient}"/>.
    /// </remarks>
    public static IServiceCollection AddCometBftRest<TInterface, TClient>(
        this IServiceCollection services,
        Action<CometBftRestOptions> configure)
        where TInterface : class, ICometBftRestClient
        where TClient : class, TInterface
        => services.AddCometBftRest<Core.Domain.Block, Core.Domain.TxResult, Core.Domain.Validator, TInterface, TClient>(configure);

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
        => services.AddCometBftRest<ICometBftRestClient, CometBftRestClient>(configure);

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
    /// Registers a typed CometBFT WebSocket subscription client using fully-typed domain
    /// parameters and a custom interface and implementation.
    /// </summary>
    /// <typeparam name="TTx">The application-specific transaction type.</typeparam>
    /// <typeparam name="TBlock">The block type. Must inherit <see cref="Core.Domain.Block{TTx}"/>.</typeparam>
    /// <typeparam name="TTxResult">The transaction result type. Must inherit <see cref="Core.Domain.TxResult{TTx}"/>.</typeparam>
    /// <typeparam name="TValidator">The validator type. Must inherit <see cref="Core.Domain.Validator"/>.</typeparam>
    /// <typeparam name="TInterface">
    /// The service interface to register. Must implement
    /// <see cref="ICometBftWebSocketClient{TTx,TBlock,TTxResult,TValidator}"/>.
    /// </typeparam>
    /// <typeparam name="TClient">
    /// The concrete implementation. Must implement <typeparamref name="TInterface"/>.
    /// </typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftWebSocketOptions"/>.</param>
    /// <param name="codec">The codec used to decode transaction bytes into <typeparamref name="TTx"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/>, <paramref name="configure"/>,
    /// or <paramref name="codec"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Use this overload when a downstream layer extends domain types:
    /// <code>
    /// services.AddCometBftWebSocket&lt;CosmosTx, CosmosBlock&lt;CosmosTx&gt;,
    ///     CosmosTxResult, CosmosValidator,
    ///     ICosmosWebSocketClient, CosmosWebSocketClient&gt;(o =&gt; …, codec);
    /// </code>
    /// <typeparamref name="TClient"/> is resolved from DI; its constructor parameters
    /// (<see cref="IOptions{TOptions}"/> of <see cref="CometBftWebSocketOptions"/> and
    /// <see cref="ITxCodec{TTx}"/>) are registered by this method.
    /// </remarks>
    public static IServiceCollection AddCometBftWebSocket<TTx, TBlock, TTxResult, TValidator, TInterface, TClient>(
        this IServiceCollection services,
        Action<CometBftWebSocketOptions> configure,
        ITxCodec<TTx> codec)
        where TTx : notnull
        where TBlock : Core.Domain.Block<TTx>
        where TTxResult : Core.Domain.TxResult<TTx>
        where TValidator : Core.Domain.Validator
        where TInterface : class, ICometBftWebSocketClient<TTx, TBlock, TTxResult, TValidator>
        where TClient : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(codec);

        var tempOptions = new CometBftWebSocketOptions();
        configure(tempOptions);
        tempOptions.Validate();

        services.Configure<CometBftWebSocketOptions>(configure);
        services.AddSingleton<ITxCodec<TTx>>(codec);
        services.AddSingleton<TInterface, TClient>();

        return services;
    }

    /// <summary>
    /// Registers a typed CometBFT WebSocket subscription client using a custom interface
    /// and implementation that use the default domain types for the given
    /// <typeparamref name="TTx"/>.
    /// </summary>
    /// <typeparam name="TTx">The application-specific transaction type.</typeparam>
    /// <typeparam name="TInterface">
    /// The service interface to register. Must implement <see cref="ICometBftWebSocketClient{TTx}"/>.
    /// </typeparam>
    /// <typeparam name="TClient">
    /// The concrete implementation. Must implement <typeparamref name="TInterface"/>.
    /// </typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An action to configure <see cref="CometBftWebSocketOptions"/>.</param>
    /// <param name="codec">The codec used to decode transaction bytes into <typeparamref name="TTx"/>.</param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/>, <paramref name="configure"/>,
    /// or <paramref name="codec"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Use this overload when the consumer interface extends <see cref="ICometBftWebSocketClient{TTx}"/>
    /// without changing block/tx-result/validator types. For custom domain types use
    /// <see cref="AddCometBftWebSocket{TTx,TBlock,TTxResult,TValidator,TInterface,TClient}"/>.
    /// </remarks>
    public static IServiceCollection AddCometBftWebSocket<TTx, TInterface, TClient>(
        this IServiceCollection services,
        Action<CometBftWebSocketOptions> configure,
        ITxCodec<TTx> codec)
        where TTx : notnull
        where TInterface : class, ICometBftWebSocketClient<TTx>
        where TClient : class, TInterface
        => services.AddCometBftWebSocket<
                TTx,
                Core.Domain.Block<TTx>,
                Core.Domain.TxResult<TTx>,
                Core.Domain.Validator,
                TInterface,
                TClient>(configure, codec);

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
    /// <para>
    /// The supplied <paramref name="codec"/> is registered as a singleton and shared
    /// across concurrent WebSocket message handlers. Codec implementations must be thread-safe.
    /// See <see cref="ITxCodec{TTx}"/> remarks for details.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddCometBftWebSocket<TTx>(
        this IServiceCollection services,
        Action<CometBftWebSocketOptions> configure,
        ITxCodec<TTx> codec)
        where TTx : notnull
        => services.AddCometBftWebSocket<TTx, ICometBftWebSocketClient<TTx>, CometBftWebSocketClient<TTx>>(configure, codec);

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
    /// Registers the complete CometBFT client stack: REST, gRPC BroadcastAPI, and WebSocket.
    /// All clients are configured from a single <see cref="CometBftClientOptions"/> instance.
    /// Default URLs target the Lava Network public Cosmos Hub mainnet relay.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">
    /// An optional action to override default options (URLs, timeouts, retry settings).
    /// When omitted, all defaults apply (public Cosmos Hub mainnet nodes via Lava Network).
    /// </param>
    /// <returns>The <paramref name="services"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    public static IServiceCollection AddCometBftClient(
        this IServiceCollection services,
        Action<CometBftClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var opts = new CometBftClientOptions();
        configure?.Invoke(opts);

        services.AddCometBftRest(o =>
        {
            o.BaseUrl = opts.RestBaseUrl;
            o.Timeout = opts.Timeout;
            o.MaxRetryAttempts = opts.MaxRetryAttempts;
            o.RetryDelay = opts.RetryDelay;
        });

        services.AddCometBftGrpc(o =>
        {
            o.BaseUrl = opts.GrpcBaseUrl;
            o.Timeout = opts.Timeout;
        });

        services.AddCometBftWebSocket(o => o.BaseUrl = opts.WebSocketBaseUrl);

        return services;
    }
}
