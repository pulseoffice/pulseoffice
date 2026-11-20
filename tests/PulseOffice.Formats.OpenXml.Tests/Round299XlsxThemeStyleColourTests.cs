using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.OpenXml;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round299XlsxThemeStyleColourTests
{
    [Fact]
    public void Theme_and_indexed_colours_of_fonts_fills_and_borders_resolve_through_the_palette()
    {
        var stylesheet = new Stylesheet(
            new Fonts(new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }, new Color { Theme = 4 })),
            new Fills(new Fill(new PatternFill(new ForegroundColor { Indexed = 10 }) { PatternType = PatternValues.Solid })),
            new Borders(new Border(
                new LeftBorder(new Color { Theme = 5 }) { Style = BorderStyleValues.Thin },
                new RightBorder(), new TopBorder(), new BottomBorder())),
            new CellFormats(new CellFormat { FontId = 0, FillId = 0, BorderId = 0 }));

        var style = Assert.Single(new XlsxStyleReader().ReadStyles(stylesheet));

        Assert.Equal("#4472C4", style.Font.ColorHex);
        Assert.Equal("#FF0000", style.BackgroundHex);
        Assert.Equal("#ED7D31", style.Borders.Left.ColorHex);
    }
}
