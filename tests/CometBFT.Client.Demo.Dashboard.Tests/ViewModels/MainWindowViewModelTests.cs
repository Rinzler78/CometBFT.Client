using Avalonia.Headless.XUnit;
using CometBFT.Client.Core.Domain;
using CometBFT.Client.Demo.Dashboard.ViewModels;
using Xunit;

namespace CometBFT.Client.Demo.Dashboard.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private static Block<string> MakeBlock(long height, int txCount = 0) =>
        new(height, $"hash{height}", DateTimeOffset.UtcNow, $"proposer{height}",
            Enumerable.Repeat("tx==", txCount).ToList());

    // ── Abbreviate (pure — no dispatcher) ──────────────────────────────────

    [Fact]
    public void Abbreviate_StringShorterThanMax_Unchanged() =>
        Assert.Equal("abc", MainWindowViewModel.Abbreviate("abc", 10));

    [Fact]
    public void Abbreviate_StringLongerThanMax_TruncatedWithEllipsis()
    {
        var result = MainWindowViewModel.Abbreviate("abcdefgh", 4);
        Assert.Equal("abcd…", result);
    }

    // ── OnNewBlock ──────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void OnNewBlock_NewHeight_BlockPrepended()
    {
        var vm = new MainWindowViewModel();
        vm.OnNewBlock(MakeBlock(1));
        vm.OnNewBlock(MakeBlock(2));

        Assert.Equal(2, vm.Blocks.Count);
        Assert.Equal(2L, vm.Blocks[0].Height);
        Assert.Equal(2L, vm.LatestHeight);
    }

    [AvaloniaFact]
    public void OnNewBlock_DuplicateHeadHeight_BlockIgnored()
    {
        var vm = new MainWindowViewModel();
        var block = MakeBlock(100);
        vm.OnNewBlock(block);
        vm.OnNewBlock(block);

        Assert.Single(vm.Blocks);
    }

    [AvaloniaFact]
    public void OnNewBlock_ExceedsMaxBlocks_OldestTrimmed()
    {
        var vm = new MainWindowViewModel();
        for (var i = 1; i <= 51; i++)
            vm.OnNewBlock(MakeBlock(i));

        Assert.Equal(50, vm.Blocks.Count);
        Assert.Equal(51L, vm.Blocks[0].Height);
    }

    // ── AppendEventLog ──────────────────────────────────────────────────────

    [AvaloniaFact]
    public void AppendEventLog_ExceedsMaxEventLog_OldestTrimmed()
    {
        var vm = new MainWindowViewModel();
        for (var i = 0; i < 101; i++)
            vm.AppendEventLog("cat", $"msg{i}");

        Assert.Equal(100, vm.EventLog.Count);
    }

    // ── UpdateValidators ────────────────────────────────────────────────────

    [AvaloniaFact]
    public void UpdateValidators_SortedByVotingPowerDescending()
    {
        var vm = new MainWindowViewModel();
        var validators = new[]
        {
            new Validator("addr1", "pk1", 100, 0),
            new Validator("addr2", "pk2", 300, 0),
            new Validator("addr3", "pk3", 200, 0),
        };

        vm.UpdateValidators(validators);

        Assert.Equal(3, vm.Validators.Count);
        Assert.Equal(300L, vm.Validators[0].VotingPower);
        Assert.Equal(200L, vm.Validators[1].VotingPower);
        Assert.Equal(100L, vm.Validators[2].VotingPower);
    }

    // ── SetConnectionStatus ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void SetConnectionStatus_SetsProperties()
    {
        var vm = new MainWindowViewModel();
        vm.SetConnectionStatus("Connected", isConnected: true);

        Assert.Equal("Connected", vm.ConnectionStatus);
        Assert.True(vm.IsConnected);
    }
}
