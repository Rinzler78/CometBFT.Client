using Avalonia.Headless.XUnit;
using CometBFT.Client.Core.Interfaces;
using CometBFT.Client.Demo.Dashboard.Services;
using CometBFT.Client.Demo.Dashboard.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CometBFT.Client.Demo.Dashboard.Tests.Services;

public class ResilientHelperTests
{
    // NullLogger avoids Castle proxy issues with ILogger<internal-type>.
    private static DashboardBackgroundService MakeService(MainWindowViewModel? vm = null) =>
        new(
            Substitute.For<ICometBftWebSocketClient>(),
            Substitute.For<ICometBftRestClient>(),
            vm ?? new MainWindowViewModel(),
            NullLogger<DashboardBackgroundService>.Instance);

    // ── Resilient returns bool ──────────────────────────────────────────────

    [Fact]
    public async Task Resilient_SuccessfulTask_ReturnsTrue()
    {
        var svc = MakeService();
        var result = await svc.Resilient("step", Task.CompletedTask);
        Assert.True(result);
    }

    [Fact]
    public async Task Resilient_CancelledTask_Rethrows()
    {
        var svc = MakeService();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.Resilient("step", Task.FromCanceled(new CancellationToken(canceled: true))));
    }

    [AvaloniaFact]
    public async Task Resilient_FailedTask_ReturnsFalseAndAppendsEventLog()
    {
        var vm = new MainWindowViewModel();
        var svc = MakeService(vm);

        var result = await svc.Resilient("step", Task.FromException(new InvalidOperationException("boom")));

        Assert.False(result);
        Assert.Single(vm.EventLog);
        Assert.Contains("step", vm.EventLog[0].Description, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Resilient_MultipleFailures_TrueCountMatchesSuccessRate()
    {
        var vm = new MainWindowViewModel();
        var svc = MakeService(vm);

        var results = await Task.WhenAll(
            svc.Resilient("s1", Task.CompletedTask),
            svc.Resilient("s2", Task.CompletedTask),
            svc.Resilient("s3", Task.CompletedTask),
            svc.Resilient("s4", Task.CompletedTask),
            svc.Resilient("s5", Task.CompletedTask),
            svc.Resilient("s6", Task.FromException(new Exception("rejected"))),
            svc.Resilient("s7", Task.FromException(new Exception("rejected"))));

        Assert.Equal(5, results.Count(r => r));
        Assert.Equal(2, vm.EventLog.Count);
    }
}
