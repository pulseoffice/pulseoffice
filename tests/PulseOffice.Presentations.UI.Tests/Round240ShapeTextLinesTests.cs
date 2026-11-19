using System.Globalization;
using Avalonia.Media;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round240ShapeTextLinesTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    [Fact]
    public Task Long_shape_text_finds_every_line_by_probing_line_ends() => Session.Dispatch(() =>
    {
        var content = string.Join(' ', Enumerable.Repeat("column", 80));
        var text = new FormattedText(content, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, null) { MaxTextWidth = 160 };
        var tops = Enumerable.Range(0, content.Length)
            .Select(index => text.BuildHighlightGeometry(default, index, 1))
            .Where(geometry => geometry is { Bounds.Height: > 0 })
            .Select(geometry => Math.Round(geometry!.Bounds.Top, 3))
            .Distinct()
            .Count();

        var lines = new FormattedShapeTextLines().Measure(text, content);

        Assert.True(tops > 5);
        Assert.Equal(tops, lines.Count);
    }, CancellationToken.None);
}
