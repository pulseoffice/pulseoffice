using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using PivotArea = PulseOffice.Sheets.Pivot.PivotArea;
using PivotField = PulseOffice.Sheets.Pivot.PivotField;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxExternalPivotTests
{
    private readonly PivotRecordCache _cache = new()
    {
        Headers = ["Region", "Amount"],
        Records =
        [
            [CellValue.Text("North"), CellValue.Number(100)],
            [CellValue.Text("South"), CellValue.Number(20.5)],
            [CellValue.Text("North"), CellValue.FromError(CellError.NotAvailable)],
            [CellValue.Empty, CellValue.Boolean(true)],
        ],
    };

    private Workbook With(PivotExternalSource source, string sourceSheet = "", CellRange sourceRange = default)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("North"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(100));
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "x",
            Name = "Sales",
            SourceSheet = sourceSheet,
            SourceRange = sourceRange,
            ExternalSource = source,
            DestinationSheet = sheet.Name,
            Destination = CellAddress.First,
            OutputRange = new CellRange(CellAddress.First, new CellAddress(3, 1)),
            Fields =
            [
                new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
                new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        });
        return workbook;
    }

    private (Workbook Workbook, IReadOnlyList<PulseOffice.Foundation.Diagnostics.Diagnostic> Diagnostics) ThroughXlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return (result.Value!, result.Diagnostics);
    }

    private Workbook ThroughOds(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    public static TheoryData<string> Kinds() => ["sql", "table", "query", "service"];

    private PivotExternalSource Source(string kind) => kind switch
    {
        "table" => new PivotExternalSource { Kind = PivotExternalKind.Table, DatabaseName = "Archive", Command = "sales", Cache = _cache },
        "query" => new PivotExternalSource { Kind = PivotExternalKind.Query, DatabaseName = "Archive", Command = "Sales by month", Cache = _cache, ConnectionId = "archive-db" },
        "service" => new PivotExternalSource { Kind = PivotExternalKind.Service, ServiceName = "com.example.Source", ServiceSourceName = "Cube", ServiceObjectName = "Sales", ServiceUserName = "anna", Cache = _cache },
        _ => new PivotExternalSource { Kind = PivotExternalKind.Sql, DatabaseName = "Archive", Command = "SELECT region AS \"Region\", amount AS \"Amount\" FROM sales", ParseSql = false, Cache = _cache },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AnExternalSourceAndItsCacheCrossBothFormatsBothWays(string kind)
    {
        var source = Source(kind);
        var original = With(source);

        var (xlsx, diagnostics) = ThroughXlsx(original);
        var ods = ThroughOds(original);
        var crossed = ThroughOds(xlsx);
        var (back, _) = ThroughXlsx(ods);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code == "XLSX081");
        foreach (var read in new[] { xlsx, ods, crossed, back })
        {
            var pivot = Assert.Single(read.Pivots);
            Assert.Equal(source, pivot.ExternalSource);
            Assert.Equal(new CellRange(CellAddress.First, new CellAddress(3, 1)), pivot.OutputRange);
            Assert.Equal([0, 1], pivot.Fields.Select(field => field.SourceColumnIndex));
        }
    }

    [Fact]
    public void APivotOnAnotherWorkbookKeepsPathRangeAndRecords()
    {
        var source = new PivotExternalSource { Kind = PivotExternalKind.Workbook, DatabaseName = "file:///data/sales.xlsx", Cache = _cache };
        var range = new CellRange(CellAddress.First, new CellAddress(4, 1));
        var original = With(source, "Data", range);

        using var stream = new MemoryStream();
        new XlsxWriter().Write(original, stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var cache = package.WorkbookPart!.PivotTableCacheDefinitionParts.Single();
            var worksheetSource = cache.PivotCacheDefinition!.CacheSource!.WorksheetSource!;
            Assert.Equal("file:///data/sales.xlsx", cache.ExternalRelationships.Single(relation => relation.Id == worksheetSource.Id!.Value).Uri.OriginalString);
            Assert.Equal(4u, cache.PivotCacheDefinition.RecordCount!.Value);
        }

        foreach (var read in new[] { ThroughXlsx(original).Workbook, ThroughOds(original), ThroughOds(ThroughXlsx(original).Workbook) })
        {
            var pivot = Assert.Single(read.Pivots);
            Assert.Equal(source, pivot.ExternalSource);
            Assert.Equal(("Data", range), (pivot.SourceSheet, pivot.SourceRange));
        }
    }

    [Fact]
    public void PivotConnectionsShareConnectionsXmlWithQueryTables()
    {
        var workbook = With(Source("sql"));
        workbook.Sheets[0].QueryBindings.AddOrUpdate(new QueryBinding
        {
            Id = "q1",
            DisplayName = "Customers",
            ConnectionId = "crm",
            Query = new PulseOffice.Data.QueryDefinition { Sql = "SELECT * FROM customers" },
            Anchor = new CellAddress(10, 5),
            Range = new CellRange(new CellAddress(10, 5), new CellAddress(12, 6)),
        });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var connections = package.WorkbookPart!.ConnectionsPart!.Connections!.Elements<Connection>().ToList();
            Assert.Equal(2, connections.Count);
            Assert.Equal(connections.Count, connections.Select(connection => connection.Id!.Value).Distinct().Count());
            Assert.Equal(connections.Count, connections.Select(connection => connection.Name!.Value).Distinct().Count());
            var cacheSource = package.WorkbookPart.PivotTableCacheDefinitionParts.Single().PivotCacheDefinition!.CacheSource!;
            Assert.Equal(SourceValues.External, cacheSource.Type!.Value);
            Assert.Contains(connections, connection => connection.Id!.Value == cacheSource.ConnectionId!.Value && connection.DatabaseProperties!.Command!.Value!.StartsWith("SELECT region", StringComparison.Ordinal));
        }

        var (read, _) = ThroughXlsx(workbook);
        Assert.NotNull(read.Sheets[0].QueryBindings.Find("q1"));
        Assert.Equal(Source("sql"), Assert.Single(read.Pivots).ExternalSource);
    }

    [Fact]
    public void AnExternalPivotWithoutCacheIsWrittenWithoutRecords()
    {
        var source = new PivotExternalSource { Kind = PivotExternalKind.Table, DatabaseName = "Archive", Command = "sales" };

        using var stream = new MemoryStream();
        new XlsxWriter().Write(With(source), stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var cache = package.WorkbookPart!.PivotTableCacheDefinitionParts.Single();
            Assert.Null(cache.PivotTableCacheRecordsPart);
            Assert.False(cache.PivotCacheDefinition!.SaveData!.Value);
            Assert.Equal(["Region", "Amount"], cache.PivotCacheDefinition.CacheFields!.Elements<CacheField>().Select(field => field.Name!.Value));
        }

        stream.Position = 0;
        var pivot = Assert.Single(new XlsxReader().Read(stream).Value!.Pivots);
        Assert.Equal(source, pivot.ExternalSource);
    }

    [Fact]
    public void AWorkbookPathThatIsNoValidUriIsStillSaved()
    {
        var source = new PivotExternalSource { Kind = PivotExternalKind.Workbook, DatabaseName = "http://[broken", Cache = _cache };
        var original = With(source, "Data", new CellRange(CellAddress.First, new CellAddress(4, 1)));

        var (read, _) = ThroughXlsx(original);

        Assert.Equal(PivotExternalKind.Workbook, Assert.Single(read.Pivots).ExternalSource!.Kind);
    }

    [Fact]
    public void CachedTextWithControlCharactersIsSavedAndReadBack()
    {
        var cache = new PivotRecordCache
        {
            Headers = ["Re\u0001gion", "Amount"],
            Records = [[CellValue.Text("No\u0002rth_x0041_"), CellValue.Number(1)], [CellValue.Text("South"), CellValue.Number(2)]],
        };
        var source = new PivotExternalSource { Kind = PivotExternalKind.Table, DatabaseName = "Archive", Command = "sales", Cache = cache };

        var (read, _) = ThroughXlsx(With(source));

        Assert.Equal(cache, Assert.Single(read.Pivots).ExternalSource!.Cache);
    }

    [Fact]
    public void EmptyRecordsOfAWideCacheDoNotAllocateEveryField()
    {
        var source = new PivotExternalSource { Kind = PivotExternalKind.Table, DatabaseName = "Archive", Command = "sales", Cache = _cache };
        using var stream = new MemoryStream();
        new XlsxWriter().Write(With(source), stream);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var cache = package.WorkbookPart!.PivotTableCacheDefinitionParts.Single();
            var fields = cache.PivotCacheDefinition!.CacheFields!;
            for (var index = 0; index < 5000; index++) fields.AppendChild(new CacheField { Name = "Wide" + index, NumberFormatId = 0 });
            fields.Count = (uint)fields.ChildElements.Count;
            cache.PivotCacheDefinition.Save();
            var stored = cache.PivotTableCacheRecordsPart!.PivotCacheRecords!;
            for (var index = 0; index < 1000; index++) stored.AppendChild(new PivotCacheRecord());
            stored.Count = (uint)stored.ChildElements.Count;
            stored.Save();
        }
        stream.Position = 0;

        var pivot = Assert.Single(new XlsxReader().Read(stream).Value!.Pivots);

        var records = pivot.ExternalSource!.Cache.Records;
        Assert.Equal(1004, records.Count);
        Assert.All(records.Skip(4), record => Assert.Empty(record));
    }
}
