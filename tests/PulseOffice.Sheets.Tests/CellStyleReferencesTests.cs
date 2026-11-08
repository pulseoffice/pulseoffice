using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class CellStyleReferencesTests
{
    private readonly ICellStyleReferences _references = new CellStyleReferences();

    private static Workbook Sample()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Evidenza", CellStyle.Default with { BackgroundHex = "#FFE08A" }, "Heading");
        workbook.NamedStyles.Define("Figlio", CellStyle.Default with { Font = CellFont.Default with { Italic = true } }, "evidenza");
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));
        workbook.Sheets[0].SetStyleName(CellAddress.First, "Evidenza");
        var second = workbook.AddSheet("Due");
        second.SetValue(new CellAddress(2, 2), CellValue.Text("x"));
        second.SetStyleName(new CellAddress(2, 2), "EVIDENZA");
        return workbook;
    }

    [Fact]
    public void Renaming_follows_every_cell_and_every_child_and_undo_brings_all_back()
    {
        var workbook = Sample();

        var relink = _references.Rename(workbook, "Evidenza", "  Risalto €  ");

        Assert.NotNull(relink);
        Assert.Null(workbook.NamedStyles.All.FirstOrDefault(style => style.Name == "Evidenza"));
        Assert.Equal("Heading", workbook.NamedStyles.Find("Risalto €")!.Parent);
        Assert.Equal("Risalto €", workbook.NamedStyles.Find("Figlio")!.Parent);
        Assert.Equal("Risalto €", workbook.Sheets[0][CellAddress.First].StyleName);
        Assert.Equal("Risalto €", workbook.Sheets[1][new CellAddress(2, 2)].StyleName);
        Assert.Equal("#FFE08A", workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("Figlio")!).BackgroundHex);

        _references.Revert(workbook, relink);

        Assert.NotNull(workbook.NamedStyles.Find("Evidenza"));
        Assert.Null(workbook.NamedStyles.Find("Risalto €"));
        Assert.Equal("evidenza", workbook.NamedStyles.Find("Figlio")!.Parent);
        Assert.Equal("EVIDENZA", workbook.Sheets[1][new CellAddress(2, 2)].StyleName);

        _references.Apply(workbook, relink);

        Assert.Equal("Risalto €", workbook.Sheets[0][CellAddress.First].StyleName);
    }

    [Theory]
    [InlineData("Heading", CellStyleRenameCheck.BuiltIn)]
    [InlineData("Missing", CellStyleRenameCheck.Missing)]
    public void Built_in_and_missing_styles_cannot_be_renamed(string name, CellStyleRenameCheck expected)
    {
        var workbook = Sample();

        Assert.Equal(expected, _references.CheckRename(workbook, name, "Altro"));
        Assert.Null(_references.Rename(workbook, name, "Altro"));
    }

    [Theory]
    [InlineData("good", CellStyleRenameCheck.Taken)]
    [InlineData("Figlio", CellStyleRenameCheck.Taken)]
    [InlineData("   ", CellStyleRenameCheck.Invalid)]
    [InlineData("a\tb", CellStyleRenameCheck.Invalid)]
    [InlineData("Evidenza", CellStyleRenameCheck.Unchanged)]
    [InlineData("EVIDENZA", CellStyleRenameCheck.Allowed)]
    public void Names_are_checked_before_renaming(string newName, CellStyleRenameCheck expected)
    {
        Assert.Equal(expected, _references.CheckRename(Sample(), "Evidenza", newName));
    }

    [Fact]
    public void A_name_longer_than_the_limit_is_refused()
    {
        Assert.Equal(CellStyleRenameCheck.Invalid, _references.CheckRename(Sample(), "Evidenza", new string('x', _references.MaximumNameLength + 1)));
    }

    [Fact]
    public void Deleting_hands_cells_and_children_to_the_parent_and_undo_restores_them()
    {
        var workbook = Sample();

        var relink = _references.Delete(workbook, "Evidenza");

        Assert.NotNull(relink);
        Assert.Null(workbook.NamedStyles.Find("Evidenza"));
        Assert.Equal("Heading", workbook.NamedStyles.Find("Figlio")!.Parent);
        Assert.Equal("Heading", workbook.Sheets[0][CellAddress.First].StyleName);

        _references.Revert(workbook, relink);

        Assert.NotNull(workbook.NamedStyles.Find("Evidenza"));
        Assert.Equal("Evidenza", workbook.Sheets[0][CellAddress.First].StyleName);
    }

    [Fact]
    public void Renaming_a_style_in_a_parent_cycle_terminates()
    {
        var workbook = new Workbook();
        workbook.NamedStyles.Define("A", CellStyle.Default, parent: "B");
        workbook.NamedStyles.Define("B", CellStyle.Default, parent: "A");

        Assert.NotNull(_references.Rename(workbook, "A", "C"));
        Assert.Equal("C", workbook.NamedStyles.Find("B")!.Parent);
        Assert.Equal("B", workbook.NamedStyles.Find("C")!.Parent);
        workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find("C")!);
    }

    [Fact]
    public void The_sheets_using_a_style_are_listed()
    {
        var workbook = Sample();

        Assert.Equal(2, _references.SheetsUsing(workbook, "evidenza").Count);
    }
}
