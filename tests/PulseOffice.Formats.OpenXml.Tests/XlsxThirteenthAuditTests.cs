using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using Xunit;
using X = DocumentFormat.OpenXml.Spreadsheet;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxThirteenthAuditTests
{
    [Fact]
    public void Due_tabelle_con_filtro_sullo_stesso_foglio_si_conservano()
    {
        var workbook = Read(2).Value!;

        Assert.Equal(new PulseOffice.Sheets.Addressing.CellRange(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), new PulseOffice.Sheets.Addressing.CellAddress(2, 0)), workbook.Sheets[0].AutoFilter.Range);
        var second = workbook.DatabaseRanges.Find("Tabella2")!;
        Assert.True(second.ShowsFilterButtons);
    }

    [Fact]
    public void Le_unioni_sovrapposte_si_dichiarano()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            worksheet.InsertAfter(new X.MergeCells(new X.MergeCell { Reference = "B1:B3" }, new X.MergeCell { Reference = "A2:B2" }) { Count = 2 },
                worksheet.GetFirstChild<X.SheetData>());
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "XLSX181");
        Assert.Single(read.Value!.Sheets[0].Merges.All);
    }

    [Fact]
    public void I_collegamenti_sulle_forme_si_leggono()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "forma",
            Anchor = new PulseOffice.Sheets.Addressing.CellAddress(1, 1),
            Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(3), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "forma", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse },
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        Assert.DoesNotContain(new XlsxReader().Read(new MemoryStream(stream.ToArray())).Diagnostics, diagnostic => diagnostic.Code == "XLSX182");
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var drawing = document.WorkbookPart!.WorksheetParts.First().DrawingsPart!;
            var link = drawing.AddHyperlinkRelationship(new Uri("https://example.org/"), true);
            drawing.WorksheetDrawing!.Descendants<DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualDrawingProperties>().First()
                .Append(new DocumentFormat.OpenXml.Drawing.HyperlinkOnClick { Id = link.Id });
        }

        stream.Position = 0;
        Assert.Equal("https://example.org/", new XlsxReader().Read(stream).Value!.Sheets[0].Objects.Single().Hyperlink);
        Assert.DoesNotContain(new XlsxReader().Read(new MemoryStream(stream.ToArray())).Diagnostics, diagnostic => diagnostic.Code == "XLSX183");

        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
            document.WorkbookPart!.WorksheetParts.First().DrawingsPart!.WorksheetDrawing!.Descendants<DocumentFormat.OpenXml.Drawing.PresetGeometry>().First().Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Snip1Rectangle;
        stream.Position = 0;
        var read = new XlsxReader().Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "XLSX183");
        Assert.Contains(read.Value!.Sheets[0].Objects, placed =>
            placed.Content is PulseOffice.Drawing.Shapes.ShapeObject { Computed: PulseOffice.Drawing.Shapes.Geometry.PresetGeometry { Name: "snip1Rect" } });
    }

    [Fact]
    public void Una_forma_libera_resta_forma_con_il_colore_del_tema()
    {
        var workbook = new Workbook();
        var outline = new PulseOffice.Drawing.Shapes.ShapeOutline(
            [new PulseOffice.Drawing.Shapes.ShapeFigure([new PulseOffice.Drawing.Shapes.MoveSegment(new PulseOffice.Drawing.Shapes.ShapePoint(0, 0)),
                new PulseOffice.Drawing.Shapes.LineSegment(new PulseOffice.Drawing.Shapes.ShapePoint(1, 0)), new PulseOffice.Drawing.Shapes.LineSegment(new PulseOffice.Drawing.Shapes.ShapePoint(0.5, 1)),
                PulseOffice.Drawing.Shapes.CloseSegment.Instance])]);
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "libera",
            Anchor = new PulseOffice.Sheets.Addressing.CellAddress(1, 1),
            Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(3), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Content = new PulseOffice.Drawing.Freeform.FreeformObject { Id = "libera", Path = PulseOffice.Drawing.Freeform.FreeformPath.FromOutline(outline) },
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var properties = document.WorkbookPart!.WorksheetParts.First().DrawingsPart!.WorksheetDrawing!.Descendants<DocumentFormat.OpenXml.Drawing.Spreadsheet.ShapeProperties>().First();
            properties.RemoveAllChildren<DocumentFormat.OpenXml.Drawing.SolidFill>();
            properties.RemoveAllChildren<DocumentFormat.OpenXml.Drawing.NoFill>();
            properties.InsertAfter(new DocumentFormat.OpenXml.Drawing.SolidFill(new DocumentFormat.OpenXml.Drawing.SchemeColor { Val = DocumentFormat.OpenXml.Drawing.SchemeColorValues.Accent2 }),
                properties.GetFirstChild<DocumentFormat.OpenXml.Drawing.CustomGeometry>());
        }

        stream.Position = 0;
        var placed = Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].Objects);
        var freeform = Assert.IsType<PulseOffice.Drawing.Freeform.FreeformObject>(placed.Content);
        Assert.Equal("libera", freeform.Id);
        Assert.Equal("#C0504D", freeform.Style.Fill.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void Un_gruppo_con_una_forma_libera_fa_il_giro_in_XLSX()
    {
        var outline = new PulseOffice.Drawing.Shapes.ShapeOutline(
            [new PulseOffice.Drawing.Shapes.ShapeFigure([new PulseOffice.Drawing.Shapes.MoveSegment(new PulseOffice.Drawing.Shapes.ShapePoint(0, 0)),
                new PulseOffice.Drawing.Shapes.LineSegment(new PulseOffice.Drawing.Shapes.ShapePoint(1, 1)), PulseOffice.Drawing.Shapes.CloseSegment.Instance])]);
        var group = new PulseOffice.Drawing.Shapes.ShapeGroupObject
        {
            Id = "gruppo",
            Children =
            [
                new PulseOffice.Drawing.Shapes.GroupChild(new PulseOffice.Drawing.Freeform.FreeformObject { Id = "libera", Path = PulseOffice.Drawing.Freeform.FreeformPath.FromOutline(outline) }, 0, 0, 0.5, 1),
                new PulseOffice.Drawing.Shapes.GroupChild(new PulseOffice.Drawing.Shapes.ShapeObject { Id = "ovale", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse }, 0.5, 0, 0.5, 1),
            ],
        };
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "gruppo",
            Anchor = new PulseOffice.Sheets.Addressing.CellAddress(1, 1),
            Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Content = group,
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        var read = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(Assert.Single(new XlsxReader().Read(stream).Value!.Sheets[0].Objects).Content);
        Assert.Contains(read.Children, child => child.Content is PulseOffice.Drawing.Freeform.FreeformObject);
        Assert.Contains(read.Children, child => child.Content is PulseOffice.Drawing.Shapes.ShapeObject);
    }

    private PulseOffice.Foundation.Diagnostics.OperationResult<Workbook> Read(int tables)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var sheet = document.WorkbookPart!.WorksheetParts.First();
            var parts = new X.TableParts { Count = (uint)tables };
            for (var index = 1; index <= tables; index++)
            {
                var column = (char)('A' + (index - 1) * 2);
                var reference = $"{column}1:{column}3";
                var part = sheet.AddNewPart<TableDefinitionPart>();
                part.Table = new X.Table(new X.AutoFilter { Reference = reference },
                    new X.TableColumns(new X.TableColumn { Id = 1, Name = "Colonna" + index }) { Count = 1 })
                { Id = (uint)index, Name = "Tabella" + index, DisplayName = "Tabella" + index, Reference = reference };
                parts.Append(new X.TablePart { Id = sheet.GetIdOfPart(part) });
            }

            sheet.Worksheet!.Append(parts);
        }

        stream.Position = 0;
        return new XlsxReader().Read(stream);
    }
}
