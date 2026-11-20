using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round285TableStyleClearFormTests
{
    private readonly FolioTableStyleForm _form = new(new TextCatalog(UiLanguage.English));

    [Fact]
    public void Clearing_an_inherited_font_size_and_line_writes_explicit_overrides()
    {
        var line = new BorderLine(1, 0xFF336699);
        var inherited = new TableProperties
        {
            TotalRow = new TableRegionFormat { Font = "Serif", FontSize = 12, Borders = new BoxBorders { Top = line, Bottom = line, Left = line, Right = line } },
        };
        var values = new Dictionary<string, object>
        {
            ["TotalRow.font"] = string.Empty,
            ["TotalRow.fontSize"] = 0.0,
            ["TotalRow.lineWidth"] = 0.0,
            ["TotalRow.lineColor"] = "#336699",
        };

        var read = _form.Read(inherited, TableProperties.None, values);

        Assert.Equal(string.Empty, read.TotalRow!.Font);
        Assert.Equal(0, read.TotalRow.FontSize);
        Assert.Equal(0, read.TotalRow.Borders!.Bottom!.Width);
        Assert.Null(read.Over(inherited).TotalRow!.Font is { Length: > 0 } ? "kept" : null);
    }
}
