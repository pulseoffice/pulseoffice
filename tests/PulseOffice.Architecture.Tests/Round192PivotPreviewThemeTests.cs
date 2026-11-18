using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class Round192PivotPreviewThemeTests
{
    private static readonly string[] FixedColours = ["Color.FromRgb(", "Brushes.White", "Brushes.Black", "Color.Parse("];

    [Fact]
    public void The_pivot_dialog_preview_takes_its_colours_from_the_theme()
    {
        var text = File.ReadAllText(Path.Combine(Sources().FullName, "PulseOffice.Sheets.UI", "PivotPromptView.cs"));

        var found = FixedColours.Where(colour => text.Contains(colour, StringComparison.Ordinal)).ToList();

        Assert.True(found.Count == 0, string.Join(";", found));
    }

    private static DirectoryInfo Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PulseOffice.slnx"))) directory = directory.Parent;
        return new DirectoryInfo(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("PulseOffice.slnx"), "src"));
    }
}
