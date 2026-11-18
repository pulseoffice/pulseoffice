using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class Round203FocusVisualTests
{
    [Fact]
    public void The_colour_spectrum_surfaces_show_a_focus_ring()
    {
        var theme = File.ReadAllText(Path.Combine(Sources().FullName, "PulseOffice.UI.Controls", "Theming", "PulseTheme.axaml"));

        Assert.Contains(":is(spectrum|SpectrumSurface)", theme, StringComparison.Ordinal);
    }

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
