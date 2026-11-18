using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class Round203MediaControlsTests
{
    [Fact]
    public void Media_controls_are_reachable_by_keyboard_and_named()
    {
        var text = File.ReadAllText(Path.Combine(Sources().FullName, "PulseOffice.UI.Controls", "Objects", "MediaView.cs"));

        Assert.DoesNotContain("Focusable = false", text, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(_toggle,", text, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(_seek,", text, StringComparison.Ordinal);
    }

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
