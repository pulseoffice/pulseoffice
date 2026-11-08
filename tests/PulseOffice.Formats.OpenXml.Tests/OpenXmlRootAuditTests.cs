using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

[Collection(nameof(OpenXmlLargeDocumentPerformanceTests))]
public sealed class OpenXmlRootAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private (Presentation Deck, Slide Slide) Sample()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    [Fact]
    public void A_comment_author_starting_with_an_emoji_does_not_break_the_save()
    {
        var (deck, slide) = Sample();
        slide.AddComment(new SlideComment { Id = "c1", Author = "😀 Mario", Text = "ciao", CreatedAt = DateTimeOffset.UnixEpoch });

        using var buffer = new MemoryStream();
        new PptxWriter().Write(deck, buffer);
        buffer.Position = 0;
        var result = new PptxReader(_themes, _masters).Read(buffer);

        Assert.True(result.Succeeded);
        Assert.Equal("😀 Mario", Assert.Single(result.Value!.Slides[0].Comments).Author);
    }

    [Fact]
    public void Last_slide_viewed_is_not_read_as_last_slide()
    {
        var properties = new P.NonVisualDrawingProperties(
            new D.HyperlinkOnClick { Id = string.Empty, Action = "ppaction://hlinkshowjump?jump=lastslideviewed" });

        Assert.Null(new PptxInteractionMapper().Read(properties, null!, "s1"));
    }

    private static PulseOffice.Sheets.Workbook PivotBook(int rows)
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var source = workbook.Sheets[0];
        source.SetValue(new CellAddress(0, 0), CellValue.Text("Codice"));
        source.SetValue(new CellAddress(0, 1), CellValue.Text("Importo"));
        for (var row = 1; row <= rows; row++)
        {
            source.SetValue(new CellAddress(row, 0), CellValue.Text("k" + row));
            source.SetValue(new CellAddress(row, 1), CellValue.Number(row));
        }
        workbook.AddSheet("Pivot");
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p1",
            Name = "P",
            SourceSheet = source.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(rows, 1)),
            DestinationSheet = "Pivot",
            Destination = new CellAddress(0, 0),
            Fields =
            [
                new PivotField { SourceName = "Codice", SourceColumnIndex = 0, Area = PivotArea.Row },
                new PivotField { SourceName = "Importo", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        });
        return workbook;
    }

    [Fact]
    public void A_pivot_data_field_index_beyond_int_does_not_reject_the_workbook()
    {
        using var buffer = new MemoryStream();
        new XlsxWriter().Write(PivotBook(3), buffer);
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            var table = document.WorkbookPart!.WorksheetParts.SelectMany(part => part.PivotTableParts).Single();
            foreach (var field in table.PivotTableDefinition!.Descendants<DocumentFormat.OpenXml.Spreadsheet.DataField>())
                field.Field = uint.MaxValue;
            table.PivotTableDefinition.Save();
        }
        buffer.Position = 0;

        Assert.True(new XlsxReader().Read(buffer).Succeeded);
    }

    [Fact]
    public void A_pivot_over_many_distinct_items_is_written_quickly()
    {
        var workbook = PivotBook(60_000);
        using var buffer = new MemoryStream();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        new XlsxWriter().Write(workbook, buffer);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), watch.Elapsed.ToString());
    }

    [Fact]
    public void A_defined_name_with_a_sheet_index_beyond_int_does_not_reject_the_workbook()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.DefineName(new PulseOffice.Sheets.DefinedName("Zona", "$A$1", workbook.Sheets[0].Name));
        using var buffer = new MemoryStream();
        new XlsxWriter().Write(workbook, buffer);
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            foreach (var name in document.WorkbookPart!.Workbook!.Descendants<DocumentFormat.OpenXml.Spreadsheet.DefinedName>())
                name.LocalSheetId = uint.MaxValue;
            document.WorkbookPart.Workbook.Save();
        }
        buffer.Position = 0;

        Assert.True(new XlsxReader().Read(buffer).Succeeded);
    }

    [Fact]
    public void A_duplicate_sheet_renamed_onto_an_existing_name_still_opens()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        workbook.Sheets[0].Name = "A";
        workbook.AddSheet("A_3");
        workbook.AddSheet("B");
        using var buffer = new MemoryStream();
        new XlsxWriter().Write(workbook, buffer);
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            document.WorkbookPart!.Workbook!.Descendants<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Last().Name = "A";
            document.WorkbookPart.Workbook.Save();
        }
        buffer.Position = 0;

        var read = new XlsxReader().Read(buffer);

        Assert.True(read.Succeeded);
        Assert.Equal(3, read.Value!.Sheets.Select(sheet => sheet.Name).Distinct().Count());
    }

    [Fact]
    public void A_scenario_value_with_a_thousands_separator_stays_text()
    {
        var sheet = PulseOffice.Sheets.Workbook.CreateDefault().Sheets[0];
        var scenarios = new DocumentFormat.OpenXml.Spreadsheet.Scenarios(
            new DocumentFormat.OpenXml.Spreadsheet.Scenario(
                new DocumentFormat.OpenXml.Spreadsheet.InputCells { CellReference = "A1", Val = "1,234" }) { Name = "s" });

        new XlsxScenarioMapper().Read(scenarios, sheet);

        Assert.Equal(CellValue.Text("1,234"), Assert.Single(sheet.Scenarios).Values[CellAddress.First]);
    }

    [Fact]
    public void A_long_run_of_implicit_intersection_operators_is_exported_without_deep_recursion()
    {
        string? exported = null;
        var thread = new Thread(() => exported = new XlsxSingleValueSyntax().Export(new string('@', 20_000) + "A1"), 256 * 1024);
        thread.Start();
        thread.Join();

        Assert.Equal("_xlfn.SINGLE(A1)", exported);

        var nested = new Thread(() => exported = new XlsxSingleValueSyntax().Export(string.Concat(Enumerable.Repeat("@(", 20_000)) + "A1" + new string(')', 20_000)), 256 * 1024);
        nested.Start();
        nested.Join();

        Assert.StartsWith("_xlfn.SINGLE(", exported);
    }

    [Fact]
    public void A_vector_colour_that_is_not_hexadecimal_is_not_written_as_an_invalid_rgb_value()
    {
        var paint = new PulseOffice.Drawing.Shapes.VectorPaint { FillHex = "#rosso", StrokeHex = "blu" };

        var colours = new OoxmlVectorPaths().Paint(paint, 100, 100)
            .SelectMany(element => element.Descendants<D.RgbColorModelHex>()).Select(colour => colour.Val!.Value!).ToList();

        Assert.Equal(2, colours.Count);
        Assert.All(colours, colour => Assert.Matches("^[0-9A-F]{6}$", colour));
    }
}
