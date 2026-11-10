using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class NamedStyleRound131Tests
{
    private static readonly BorderEdge Thin = new(BorderStyle.Thin, "#112233", Twip.FromPoints(0.5));

    [Fact]
    public void A_child_can_switch_off_what_its_parent_switches_on()
    {
        var workbook = new Workbook();
        workbook.NamedStyles.Define("Base", CellStyle.Default with
        {
            WrapText = true,
            Locked = false,
            Vertical = CellVerticalAlignment.Top,
            Font = CellFont.Default with { Bold = true },
        });
        workbook.NamedStyles.Define("Child", CellStyle.Default, parent: "Base",
            explicitProperties: CellStyleProperties.Wrap | CellStyleProperties.Locked | CellStyleProperties.Vertical | CellStyleProperties.Bold);

        var style = workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("Child")!);

        Assert.False(style.WrapText);
        Assert.True(style.Locked);
        Assert.Equal(CellVerticalAlignment.Bottom, style.Vertical);
        Assert.False(style.Font.Bold);
    }

    [Fact]
    public void Font_and_borders_are_inherited_attribute_by_attribute()
    {
        var workbook = new Workbook();
        workbook.NamedStyles.Define("Base", CellStyle.Default with
        {
            Font = CellFont.Default with { Bold = true, Size = 14, Family = "Inter" },
            Borders = CellBorders.None with { Top = Thin, Bottom = Thin },
        });
        workbook.NamedStyles.Define("Child", CellStyle.Default with
        {
            Font = CellFont.Default with { ColorHex = "#FF0000" },
            Borders = CellBorders.None with { Left = Thin },
        }, parent: "Base");

        var style = workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("Child")!);

        Assert.True(style.Font.Bold);
        Assert.Equal(14, style.Font.Size);
        Assert.Equal("Inter", style.Font.Family);
        Assert.Equal("#FF0000", style.Font.ColorHex);
        Assert.Equal(Thin, style.Borders.Top);
        Assert.Equal(Thin, style.Borders.Bottom);
        Assert.Equal(Thin, style.Borders.Left);
    }

    [Fact]
    public void Restoring_a_redefined_style_keeps_its_place()
    {
        var workbook = new Workbook();
        var catalog = workbook.NamedStyles;
        var position = catalog.IndexOf("Good");
        var before = catalog.Find("Good");
        var after = catalog.Define("Good", CellStyle.Default with { BackgroundHex = "#00FF00" });

        catalog.Restore("Good", null);
        catalog.Restore("Good", after, position);
        Assert.Equal(position, catalog.IndexOf("Good"));
        catalog.Restore("Good", before, position);
        Assert.Equal(position, catalog.IndexOf("Good"));
        Assert.Same(before, catalog.Find("Good"));
    }

    [Fact]
    public void Removing_a_style_hands_its_children_to_its_parent_with_their_look_unchanged()
    {
        var workbook = new Workbook();
        var catalog = workbook.NamedStyles;
        catalog.Define("Middle", CellStyle.Default with { BackgroundHex = "#FFE08A" }, parent: "Heading");
        catalog.Define("Leaf", CellStyle.Default with { Font = CellFont.Default with { Italic = true } }, parent: "Middle");
        var look = catalog.StyleOf(catalog.Find("Leaf")!);

        Assert.True(catalog.Remove("Middle"));

        var leaf = catalog.Find("Leaf")!;
        Assert.Equal("Heading", leaf.Parent);
        Assert.Equal(look, catalog.StyleOf(leaf));
    }
}
