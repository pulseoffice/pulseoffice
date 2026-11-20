using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round279TableStyleTextFormTests
{
    private readonly FolioTableStyleForm _form = new(new TextCatalog(UiLanguage.English));

    [Fact]
    public void The_form_offers_italics_fonts_and_lines_for_header_bands_and_regions()
    {
        var keys = _form.Tabs(TableProperties.None).SelectMany(tab => tab.Fields).Select(field => field.Key).ToList();

        Assert.Contains("HeaderRow.italic", keys);
        Assert.Contains("SecondBand.fontSize", keys);
        Assert.Contains("TotalRow.lineWidth", keys);
        Assert.Contains("FirstColumn.font", keys);
    }

    [Fact]
    public void Text_and_line_fields_are_read_into_the_region()
    {
        var values = new Dictionary<string, object>
        {
            ["HeaderRow.italic"] = true,
            ["TotalRow.font"] = "Serif",
            ["TotalRow.fontSize"] = 12.0,
            ["TotalRow.lineWidth"] = 1.0,
            ["TotalRow.lineColor"] = "#336699",
        };

        var read = _form.Read(TableProperties.None, TableProperties.None, values);

        var line = new BorderLine(1, 0xFF336699);
        Assert.Equal(new TableRegionFormat { Italic = true }, read.HeaderRow);
        Assert.Equal(new TableRegionFormat { Font = "Serif", FontSize = 12, Borders = new BoxBorders { Top = line, Bottom = line, Left = line, Right = line } }, read.TotalRow);
        Assert.Null(read.FirstBand);
    }
}
