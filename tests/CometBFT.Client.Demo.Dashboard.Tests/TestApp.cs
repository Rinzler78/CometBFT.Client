using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(CometBFT.Client.Demo.Dashboard.Tests.TestApp))]

namespace CometBFT.Client.Demo.Dashboard.Tests;

public class TestApp : Application
{
    public override void Initialize() { }
}
