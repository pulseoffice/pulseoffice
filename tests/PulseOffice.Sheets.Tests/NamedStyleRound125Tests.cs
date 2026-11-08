using PulseOffice.Sheets;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class NamedStyleRound125Tests
{
    [Fact]
    public void A_child_style_inherits_every_property_its_parent_sets()
    {
        var workbook = new Workbook();
        workbook.NamedStyles.Define("Base", CellStyle.Default with
        {
            Language = "de-DE",
            WrapText = true,
            Vertical = CellVerticalAlignment.Top,
            RotationDegrees = 45,
            Indent = 2,
            Locked = false,
        });
        workbook.NamedStyles.Define("Child", CellStyle.Default with { BackgroundHex = "#FF0000" }, parent: "Base");

        var style = workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("Child")!);

        Assert.Equal("de-DE", style.Language);
        Assert.True(style.WrapText);
        Assert.Equal(CellVerticalAlignment.Top, style.Vertical);
        Assert.Equal(45, style.RotationDegrees);
        Assert.Equal(2, style.Indent);
        Assert.False(style.Locked);
        Assert.Equal("#FF0000", style.BackgroundHex);
    }

    [Fact]
    public void A_child_style_keeps_its_own_values_over_the_parent()
    {
        var workbook = new Workbook();
        workbook.NamedStyles.Define("Base", CellStyle.Default with { Language = "de-DE", Vertical = CellVerticalAlignment.Top, Indent = 2 });
        workbook.NamedStyles.Define("Child", CellStyle.Default with { Language = "fr-FR", Vertical = CellVerticalAlignment.Middle, Indent = 4 }, parent: "Base");

        var style = workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("Child")!);

        Assert.Equal("fr-FR", style.Language);
        Assert.Equal(CellVerticalAlignment.Middle, style.Vertical);
        Assert.Equal(4, style.Indent);
    }
}
