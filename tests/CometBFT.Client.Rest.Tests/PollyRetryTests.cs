using System.Net;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using CometBFT.Client.Core.Options;
using CometBFT.Client.Rest;
using Xunit;

namespace CometBFT.Client.Rest.Tests;

/// <summary>
/// Tests that a Polly retry policy automatically retries on transient 5xx errors
/// and returns the successful response once the server recovers.
/// Uses a fake HTTP handler and zero retry delay so the tests complete immediately.
/// </summary>
public sealed class PollyRetryTests
{
    /// <summary>
    /// Returns queued responses in order; the last response is repeated when exhausted.
    /// </summary>
    private sealed class FakeSequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public int CallCount { get; private set; }

        public FakeSequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            var response = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
            return Task.FromResult(response);
        }
    }

    /// <summary>A <see cref="DelegatingHandler"/> that executes an async Polly policy around the inner handler.</summary>
    private sealed class PolicyDelegatingHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public PolicyDelegatingHandler(IAsyncPolicy<HttpResponseMessage> policy, HttpMessageHandler innerHandler)
        {
            _policy = policy;
            InnerHandler = innerHandler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => _policy.ExecuteAsync(token => base.SendAsync(request, token), ct);
    }

    private static (CometBftRestClient client, HttpClient httpClient) BuildClientWithRetry(
        FakeSequenceHandler fakeHandler, int retryCount = 3)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(retryCount, _ => TimeSpan.Zero);

        var handler = new PolicyDelegatingHandler(retryPolicy, fakeHandler);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake") };
        var options = new CometBftRestOptions { BaseUrl = "http://fake" };
        return (new CometBftRestClient(httpClient, Options.Create(options)), httpClient);
    }

    [Fact]
    public async Task GetHealthAsync_RetryOnTransientFailure_SucceedsAfterOneError()
    {
        // Arrange: first call → 503, second call → 200
        var fakeHandler = new FakeSequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK));
        var (client, httpClient) = BuildClientWithRetry(fakeHandler);

        // Act
        var healthy = await client.GetHealthAsync();

        // Assert
        Assert.True(healthy);
        Assert.Equal(2, fakeHandler.CallCount);
        httpClient.Dispose();
    }

    [Fact]
    public async Task GetBlockAsync_RetryOnTransientFailure_SucceedsAfterOneError()
    {
        // Arrange: first call → 503, second call → 200 with block JSON
        const string blockJson = """
            {
              "jsonrpc": "2.0",
              "id": 1,
              "result": {
                "block_id": { "hash": "RETRY_HASH" },
                "block": {
                  "header": {
                    "version": { "block": "11" },
                    "chain_id": "testnet",
                    "height": "99",
                    "time": "2024-01-01T00:00:00Z",
                    "proposer_address": "P"
                  },
                  "data": { "txs": [] }
                }
              }
            }
            """;

        var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(blockJson, System.Text.Encoding.UTF8, "application/json"),
        };

        var fakeHandler = new FakeSequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            successResponse);
        var (client, httpClient) = BuildClientWithRetry(fakeHandler);

        // Act
        var block = await client.GetBlockAsync();

        // Assert
        Assert.Equal(99L, block.Height);
        Assert.Equal("RETRY_HASH", block.Hash);
        Assert.Equal(2, fakeHandler.CallCount);
        httpClient.Dispose();
    }
}
