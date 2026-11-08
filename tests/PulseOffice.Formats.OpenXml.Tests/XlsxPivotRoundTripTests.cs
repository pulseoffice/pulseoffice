using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxPivotRoundTripTests
{
    private static Workbook WithPivot(params PivotField[] fields)
    {
        var workbook = Workbook.CreateDefault();
        var source = workbook.Sheets[0];
        source.Name = "Dati vendite";
        string[] regions = ["Nord", "Sud", "Nord", "Centro", "Sud"];
        string[] months = ["Gen", "Gen", "Feb", "Feb", "Mar"];
        source.SetValue(new CellAddress(0, 0), CellValue.Text("Regione"));
        source.SetValue(new CellAddress(0, 1), CellValue.Text("Importo"));
        source.SetValue(new CellAddress(0, 2), CellValue.Text("Mese"));
        for (var row = 0; row < regions.Length; row++)
        {
            source.SetValue(new CellAddress(row + 1, 0), CellValue.Text(regions[row]));
            source.SetValue(new CellAddress(row + 1, 1), CellValue.Number(10 * (row + 1)));
            source.SetValue(new CellAddress(row + 1, 2), CellValue.Text(months[row]));
        }

        workbook.AddSheet("Pivot 1");
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p1",
            Name = "Riepilogo",
            SourceSheet = source.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(5, 2)),
            DestinationSheet = "Pivot 1",
            Destination = new CellAddress(2, 1),
            OutputRange = new CellRange(new CellAddress(2, 1), new CellAddress(7, 4)),
            Layout = PivotLayout.Tabular,
            ShowGrandTotalRows = false,
            EmptyCellText = "-",
            Fields = fields,
        });

        return workbook;
    }

    private static PivotField[] Typical() =>
    [
        new PivotField
        {
            SourceName = "Regione", SourceColumnIndex = 0, Area = PivotArea.Row, AreaOrder = 0,
            ShowSubtotals = false, SortDescending = true, ExcludedValues = ["Sud"], DisplayName = "Zona",
        },
        new PivotField { SourceName = "Mese", SourceColumnIndex = 2, Area = PivotArea.Column, AreaOrder = 0 },
        new PivotField
        {
            SourceName = "Importo", SourceColumnIndex = 1, Area = PivotArea.Value,
            Aggregation = PivotAggregation.Average, DisplayMode = ValueDisplayMode.PercentOfTotal, DisplayName = "Media",
        },
    ];

    private static Workbook ThroughXlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void LaDefinizioneDellaPivotAttraversaXlsx()
    {
        var pivot = Assert.Single(ThroughXlsx(WithPivot(Typical())).Pivots);

        Assert.Equal("Riepilogo", pivot.Name);
        Assert.Equal("Dati vendite", pivot.SourceSheet);
        Assert.Equal(new CellRange(CellAddress.First, new CellAddress(5, 2)), pivot.SourceRange);
        Assert.Equal("Pivot 1", pivot.DestinationSheet);
        Assert.Equal(new CellAddress(2, 1), pivot.Destination);
        Assert.Equal(PivotLayout.Tabular, pivot.Layout);
        Assert.False(pivot.ShowGrandTotalRows);
        Assert.True(pivot.ShowGrandTotalColumns);
        Assert.Equal("-", pivot.EmptyCellText);

        var row = Assert.Single(pivot.In(PivotArea.Row));
        Assert.Equal(("Regione", "Zona", false, true), (row.SourceName, row.DisplayName, row.ShowSubtotals, row.SortDescending));
        Assert.Equal(["Sud"], row.ExcludedValues);
        Assert.Equal("Mese", Assert.Single(pivot.In(PivotArea.Column)).SourceName);

        var value = Assert.Single(pivot.In(PivotArea.Value));
        Assert.Equal(("Importo", 1, PivotAggregation.Average, ValueDisplayMode.PercentOfTotal, "Media"),
            (value.SourceName, value.SourceColumnIndex, value.Aggregation, value.DisplayMode, value.DisplayName));
    }

    [Fact]
    public void IlFileHaCacheRecordEDefinizione()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(WithPivot(Typical()), stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);

        var cache = Assert.Single(document.WorkbookPart!.PivotTableCacheDefinitionParts);
        Assert.Equal(5u, cache.PivotTableCacheRecordsPart!.PivotCacheRecords!.Count!.Value);
        Assert.Single(document.WorkbookPart.Workbook!.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.PivotCaches>()!.ChildElements);
        var table = Assert.Single(document.WorkbookPart.WorksheetParts.SelectMany(part => part.PivotTableParts));
        Assert.Same(cache, table.PivotTableCacheDefinitionPart);
    }

    [Fact]
    public void UnaPivotDescrivibileNonDichiaraPerdite()
    {
        var losses = new XlsxSpreadsheetWriter().CheckFidelity(WithPivot(Typical())).Losses;

        Assert.DoesNotContain(losses, loss => loss.Code is "XLSX030" or "XLSX063");
    }

    [Fact]
    public void RaggruppamentiEMedianaSiDichiarano()
    {
        var fields = Typical();
        fields[1] = fields[1] with { DateGrouping = DateGrouping.Months };
        fields[2] = fields[2] with { Aggregation = PivotAggregation.Median };

        var loss = Assert.Single(new XlsxSpreadsheetWriter().CheckFidelity(WithPivot(fields)).Losses, loss => loss.Code == "XLSX030");
        Assert.Equal(2, loss.Arguments[0]);
    }
    private static OperationResultView ReadAltered(Action<WorkbookPart, DocumentFormat.OpenXml.Spreadsheet.WorksheetSource> alter)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(WithPivot(Typical()), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var book = document.WorkbookPart!;
            var cache = book.PivotTableCacheDefinitionParts.Single().PivotCacheDefinition!;
            alter(book, cache.CacheSource!.WorksheetSource!);
            cache.Save();
            book.Workbook!.Save();
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);
        return new OperationResultView(result.Value!, [.. result.Diagnostics.Select(diagnostic => diagnostic.Code)]);
    }

    private sealed record OperationResultView(Workbook Workbook, IReadOnlyList<string> Codes);

    [Fact]
    public void LaSorgenteIndicataPerNomeSiRisolve()
    {
        var read = ReadAltered((book, source) =>
        {
            source.Reference = null;
            source.Sheet = null;
            source.Name = "Vendite";
            var names = book.Workbook!.DefinedNames ?? book.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.DefinedNames());
            names.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.DefinedName { Name = "Vendite", Text = "'Dati vendite'!$A$1:$C$6" });
        });

        var pivot = Assert.Single(read.Workbook.Pivots);
        Assert.Equal("Dati vendite", pivot.SourceSheet);
        Assert.Equal(new CellRange(CellAddress.First, new CellAddress(5, 2)), pivot.SourceRange);
    }

    [Fact]
    public void LaPivotSuUnAltraCartellaSiDichiara()
    {
        var read = ReadAltered((_, source) => source.Id = "rIdEsterno");

        Assert.Empty(read.Workbook.Pivots);
        Assert.Contains("XLSX081", read.Codes);
    }
}
