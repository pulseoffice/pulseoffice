using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round154LinkReferenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static void Link(Worksheet sheet, string cell, string target) =>
        sheet[A(cell)] = new Cell { Value = CellValue.Text("go"), Hyperlink = target };

    private static void LinkedObject(Worksheet sheet, string target) =>
        sheet.Objects.Add(new PlacedObject
        {
            Id = "o", Content = new ChartDefinition { Id = "o", Type = ChartType.Column, SourceSheet = "Summary", SourceRange = "Z1:Z2" }, Anchor = A("H1"), Hyperlink = target,
        });

    [Fact]
    public void Inserting_rows_moves_cell_and_object_links_that_point_into_the_sheet()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var summary = workbook.AddSheet("Summary");
        Link(summary, "A1", "#Sheet1.A5");
        Link(data, "B1", "#A6:B7");
        LinkedObject(summary, "#'Sheet1'.C5");

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Equal("#Sheet1.A7", summary[A("A1")].Hyperlink);
        Assert.Equal("#A8:B9", data[A("B3")].Hyperlink);
        Assert.Equal("#'Sheet1'.C7", summary.Objects[0].Hyperlink);
    }

    [Fact]
    public void Inserting_rows_carries_a_cells_own_link_with_the_cell()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        Link(data, "A1", "https://example.com");

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Null(data[A("A1")].Hyperlink);
        Assert.Equal("https://example.com", data[A("A3")].Hyperlink);
    }

    [Fact]
    public void Deleting_the_rows_a_link_points_to_breaks_the_link()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var summary = workbook.AddSheet("Summary");
        Link(summary, "A1", "#Sheet1.A5");

        new SheetStructure().DeleteRows(engine, 0, 3, 3);

        Assert.Equal("#Sheet1.#REF!", summary[A("A1")].Hyperlink);
    }

    [Fact]
    public void Renaming_a_sheet_rewrites_links_and_undo_restores_them()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var summary = workbook.AddSheet("Summary");
        Link(summary, "A1", "#Sheet1.A5");
        LinkedObject(summary, "#Sheet1!C5");

        var renaming = new SheetRenaming();
        var plan = renaming.Plan(workbook, data, "My Data");
        renaming.Apply(engine, plan);

        Assert.Equal("#'My Data'.A5", summary[A("A1")].Hyperlink);
        Assert.Equal("#'My Data'!C5", summary.Objects[0].Hyperlink);

        renaming.Revert(engine, plan);

        Assert.Equal("#Sheet1.A5", summary[A("A1")].Hyperlink);
        Assert.Equal("#Sheet1!C5", summary.Objects[0].Hyperlink);
    }

    [Fact]
    public void Deleting_a_sheet_breaks_the_links_to_it_and_restoring_it_gives_them_back()
    {
        var (workbook, _) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var summary = workbook.AddSheet("Summary");
        Link(summary, "A1", "#Sheet1.A5");

        var references = new ChartSheetReferences();
        var changes = references.Removed(workbook, data);
        workbook.RemoveSheet(data);
        references.Apply(changes, forward: true);

        Assert.Equal("#Sheet1.#REF!", summary[A("A1")].Hyperlink);

        workbook.InsertSheet(data, 0);
        references.Apply(changes, forward: false);

        Assert.Equal("#Sheet1.A5", summary[A("A1")].Hyperlink);
    }

    [Fact]
    public void Moving_the_cells_a_link_points_to_moves_the_link()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var summary = workbook.AddSheet("Summary");
        Link(summary, "A1", "#Sheet1.C1");

        new SheetRangeMove(new SheetStructure()).Move(engine,
            new RangeMove(data.Name, new CellRange(A("C1"), A("C2")), data.Name, A("E10")));

        Assert.Equal("#Sheet1.E10", summary[A("A1")].Hyperlink);
    }

    [Fact]
    public void Links_to_names_web_addresses_and_unknown_sheets_are_left_alone()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var summary = workbook.AddSheet("Summary");
        Link(summary, "A1", "#Totals");
        Link(summary, "A2", "https://example.com/#A5");
        Link(summary, "A3", "#Other.A5");

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Equal(["#Totals", "https://example.com/#A5", "#Other.A5"],
            new[] { "A1", "A2", "A3" }.Select(cell => summary[A(cell)].Hyperlink));
    }
}
