using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ComboBoxGlyphTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task LaFrecciaDellaCasellaASceltaEQuellaSottileDelSelettoreDeiCaratteri(bool enabled) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var combo = new ComboBox { ItemsSource = new[] { "a" }, IsEnabled = enabled };
        var window = new Window { Content = combo };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var glyph = combo.GetVisualDescendants().OfType<PathIcon>().Single(icon => icon.Name == "DropDownGlyph");

        Assert.Equal(7, glyph.Bounds.Width, 3);
        Assert.Equal(7, glyph.Bounds.Height, 3);
        Assert.InRange(glyph.Data!.Bounds.Width, 13, 13.2);
        window.Close();
    }, CancellationToken.None);
}
