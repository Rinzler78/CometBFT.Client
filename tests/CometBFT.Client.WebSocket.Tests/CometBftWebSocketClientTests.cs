using Microsoft.Extensions.Options;
using NSubstitute;
using CometBFT.Client.Core.Events;
using CometBFT.Client.Core.Exceptions;
using CometBFT.Client.Core.Options;
using CometBFT.Client.WebSocket;
using Xunit;

namespace CometBFT.Client.WebSocket.Tests;

/// <summary>
/// Unit tests for <see cref="CometBftWebSocketClient"/>.
/// </summary>
public sealed class CometBftWebSocketClientTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CometBftWebSocketClient(null!));
    }

    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        var opts = new CometBftWebSocketOptions();
        var ex = Record.Exception(() => new CometBftWebSocketClient(Options.Create(opts)));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ConnectAsync_InvalidUrl_ThrowsCometBftWebSocketException()
    {
        var opts = new CometBftWebSocketOptions { BaseUrl = "not-a-valid-uri" };
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task SubscribeNewBlockAsync_WithoutConnection_ThrowsCometBftWebSocketException()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeNewBlockAsync());
    }

    [Fact]
    public async Task SubscribeTxAsync_WithoutConnection_ThrowsCometBftWebSocketException()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeTxAsync());
    }

    [Fact]
    public async Task SubscribeVoteAsync_WithoutConnection_ThrowsCometBftWebSocketException()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeVoteAsync());
    }

    [Fact]
    public async Task SubscribeValidatorSetUpdatesAsync_WithoutConnection_ThrowsCometBftWebSocketException()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(
            () => client.SubscribeValidatorSetUpdatesAsync());
    }

    [Fact]
    public async Task UnsubscribeAllAsync_WithoutConnection_ThrowsCometBftWebSocketException()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.UnsubscribeAllAsync());
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes_NoThrow()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await client.DisposeAsync();
        var ex = await Record.ExceptionAsync(client.DisposeAsync().AsTask);
        Assert.Null(ex);
    }

    [Fact]
    public async Task ConnectAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ConnectAsync());
    }

    [Fact]
    public async Task NewBlockReceived_EventCanBeSubscribed()
    {
        var opts = new CometBftWebSocketOptions();
        await using var client = new CometBftWebSocketClient(Options.Create(opts));

        var handler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.Block<string>>>>();
        client.NewBlockReceived += handler;
        client.NewBlockReceived -= handler;
    }

    [Fact]
    public async Task TxExecuted_EventCanBeSubscribed()
    {
        var opts = new CometBftWebSocketOptions();
        await using var client = new CometBftWebSocketClient(Options.Create(opts));

        var handler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.TxResult<string>>>>();
        client.TxExecuted += handler;
        client.TxExecuted -= handler;
    }

    [Fact]
    public async Task NewBlockHeaderReceived_EventCanBeSubscribed()
    {
        var opts = new CometBftWebSocketOptions();
        await using var client = new CometBftWebSocketClient(Options.Create(opts));

        var handler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.BlockHeader>>>();
        client.NewBlockHeaderReceived += handler;
        client.NewBlockHeaderReceived -= handler;
    }

    [Fact]
    public async Task SubscribeNewBlockHeaderAsync_WithoutConnection_ThrowsCometBftWebSocketException()
    {
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(
            () => client.SubscribeNewBlockHeaderAsync());
    }

    [Fact]
    public async Task VoteReceived_EventCanBeSubscribed()
    {
        var opts = new CometBftWebSocketOptions();
        await using var client = new CometBftWebSocketClient(Options.Create(opts));

        var handler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.Vote>>>();
        client.VoteReceived += handler;
        client.VoteReceived -= handler;
    }

    [Fact]
    public async Task ValidatorSetUpdated_EventCanBeSubscribed()
    {
        var opts = new CometBftWebSocketOptions();
        await using var client = new CometBftWebSocketClient(Options.Create(opts));

        var handler = Substitute.For<EventHandler<CometBftEventArgs<IReadOnlyList<Core.Domain.Validator>>>>();
        client.ValidatorSetUpdated += handler;
        client.ValidatorSetUpdated -= handler;
    }

    [Fact]
    public async Task AllSubscribeGuards_WithoutConnection_ThrowCometBftWebSocketException()
    {
        // Verify that all 5 subscribe methods enforce the connection-required guard.
        var opts = new CometBftWebSocketOptions();
        var client = new CometBftWebSocketClient(Options.Create(opts));

        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeNewBlockAsync());
        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeNewBlockHeaderAsync());
        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeTxAsync());
        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeVoteAsync());
        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.SubscribeValidatorSetUpdatesAsync());
        await Assert.ThrowsAsync<CometBftWebSocketException>(() => client.UnsubscribeAllAsync());

        await client.DisposeAsync();
    }

    [Fact]
    public async Task AllEvents_CanBeSubscribedAndUnsubscribed_WithoutThrow()
    {
        // Verify that all 5 event handlers can be wired up and removed without error.
        var opts = new CometBftWebSocketOptions();
        await using var client = new CometBftWebSocketClient(Options.Create(opts));

        var blockHandler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.Block<string>>>>();
        var headerHandler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.BlockHeader>>>();
        var txHandler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.TxResult<string>>>>();
        var voteHandler = Substitute.For<EventHandler<CometBftEventArgs<Core.Domain.Vote>>>();
        var validatorHandler = Substitute.For<EventHandler<CometBftEventArgs<IReadOnlyList<Core.Domain.Validator>>>>();

        client.NewBlockReceived += blockHandler;
        client.NewBlockHeaderReceived += headerHandler;
        client.TxExecuted += txHandler;
        client.VoteReceived += voteHandler;
        client.ValidatorSetUpdated += validatorHandler;

        client.NewBlockReceived -= blockHandler;
        client.NewBlockHeaderReceived -= headerHandler;
        client.TxExecuted -= txHandler;
        client.VoteReceived -= voteHandler;
        client.ValidatorSetUpdated -= validatorHandler;
    }
}
