using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsExternalPivotTests
{
    private Workbook Patched(string replacement)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Regione"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Importo"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Nord"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(4));
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p",
            Name = "Riepilogo",
            SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(1, 1)),
            DestinationSheet = sheet.Name,
            Destination = new CellAddress(0, 4),
            OutputRange = new CellRange(new CellAddress(0, 4), new CellAddress(2, 5)),
            Fields =
            [
                new PivotField { SourceName = "Regione", SourceColumnIndex = 0, Area = PivotArea.Row },
                new PivotField { SourceName = "Importo", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            var start = content.IndexOf("<table:source-cell-range", StringComparison.Ordinal);
            var end = content.IndexOf('>', start);
            var single = content[end - 1] == '/';
            var close = single ? end + 1 : content.IndexOf("</table:source-cell-range>", start, StringComparison.Ordinal) + "</table:source-cell-range>".Length;
            content = content[..start] + replacement + content[close..];
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }

        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ODF080" && !replacement.Contains("password", StringComparison.Ordinal));
        _last = result.Diagnostics;
        return result.Value!;
    }

    private IReadOnlyList<PulseOffice.Foundation.Diagnostics.Diagnostic> _last = [];

    private Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!;
    }

    [Fact]
    public void LaPivotSuUnaQueryDiDatabaseSiConservaConLaSuaOrigine()
    {
        var workbook = Patched("<table:database-source-query table:database-name=\"Archivio\" table:query-name=\"Vendite\"/>");

        var pivot = Assert.Single(workbook.Pivots);
        Assert.Equal(PivotExternalKind.Query, pivot.ExternalSource!.Kind);
        Assert.Equal("Archivio", pivot.ExternalSource.DatabaseName);
        Assert.Equal("Vendite", pivot.ExternalSource.Command);
        Assert.False(pivot.ExternalSource.Cache.HasRecords);

        var again = Assert.Single(RoundTrip(workbook).Pivots);
        Assert.Equal(pivot.ExternalSource, again.ExternalSource);
        Assert.Equal(new CellRange(new CellAddress(0, 4), new CellAddress(2, 5)), again.OutputRange);
    }

    [Fact]
    public void LeAltreOriginiDiDatabaseSiConservano()
    {
        var sql = Assert.Single(Patched("<table:database-source-sql table:database-name=\"Archivio\" table:sql-statement=\"SELECT * FROM v\" table:parse-sql-statement=\"false\"/>").Pivots).ExternalSource!;
        Assert.Equal((PivotExternalKind.Sql, "SELECT * FROM v", false), (sql.Kind, sql.Command, sql.ParseSql));

        var table = Assert.Single(Patched("<table:database-source-table table:database-name=\"Archivio\" table:database-table-name=\"vendite\"/>").Pivots).ExternalSource!;
        Assert.Equal((PivotExternalKind.Table, "vendite"), (table.Kind, table.Command));

        var workbook = Patched("<table:source-service table:name=\"com.example.Source\" table:source-name=\"Cubo\" table:object-name=\"Vendite\" table:user-name=\"anna\" table:password=\"segreta\"/>");
        var service = Assert.Single(workbook.Pivots).ExternalSource!;
        Assert.Equal((PivotExternalKind.Service, "com.example.Source", "Cubo", "Vendite", "anna"),
            (service.Kind, service.ServiceName, service.ServiceSourceName, service.ServiceObjectName, service.ServiceUserName));
        Assert.Contains(_last, diagnostic => diagnostic.Code == "ODF080");
        Assert.Equal(service, Assert.Single(RoundTrip(workbook).Pivots).ExternalSource);
    }

    [Fact]
    public void IRecordInCacheAttraversanoIlSalvataggio()
    {
        var workbook = Patched("<table:database-source-table table:database-name=\"Archivio\" table:database-table-name=\"vendite\"/>");
        var cache = new PivotRecordCache
        {
            Headers = ["Regione", "Importo"],
            Records =
            [
                [CellValue.Text("Nord"), CellValue.Number(4.5)],
                [CellValue.Empty, CellValue.Boolean(true)],
                [CellValue.FromError(CellError.NotAvailable), CellValue.Text("<&>")],
            ],
        };
        workbook.Pivots[0] = workbook.Pivots[0] with { ExternalSource = workbook.Pivots[0].ExternalSource! with { Cache = cache, ConnectionId = "vendite-db" } };

        var read = Assert.Single(RoundTrip(workbook).Pivots).ExternalSource!;

        Assert.Equal("vendite-db", read.ConnectionId);
        Assert.Equal(cache.Headers, read.Cache.Headers);
        Assert.Equal(cache.Records.Select(record => record.ToList()), read.Cache.Records.Select(record => record.ToList()));
    }

    [Fact]
    public void UnTestoInCacheConCaratteriDiControlloNonImpedisceIlSalvataggio()
    {
        var workbook = Patched("<table:database-source-table table:database-name=\"Archivio\" table:database-table-name=\"vendite\"/>");
        var cache = new PivotRecordCache { Headers = ["Re\u0001gione"], Records = [[CellValue.Text("No\u0002rd")]] };
        workbook.Pivots[0] = workbook.Pivots[0] with { ExternalSource = workbook.Pivots[0].ExternalSource! with { Cache = cache } };

        var read = Assert.Single(RoundTrip(workbook).Pivots).ExternalSource!;

        Assert.Single(read.Cache.Records);
    }

    [Fact]
    public void UnErroreInCacheSconosciutoDiventaValore()
    {
        var workbook = Patched("<table:database-source-table table:database-name=\"Archivio\" table:database-table-name=\"vendite\"/>"
            + "<pulsepivot:cache xmlns:pulsepivot=\"urn:pulseoffice:pivot\"><pulsepivot:field pulsepivot:name=\"A\"/><pulsepivot:record><pulsepivot:value pulsepivot:error=\"9999\"/></pulsepivot:record></pulsepivot:cache>");

        var value = Assert.Single(Assert.Single(Assert.Single(workbook.Pivots).ExternalSource!.Cache.Records));

        Assert.Equal(CellValue.FromError(CellError.Value), value);
    }

    [Fact]
    public void LaPivotSuUnAltraCartellaSiConservaComeIntervalloEsterno()
    {
        var workbook = Patched("<table:source-cell-range table:cell-range-address=\"'file:///dati/vendite.ods'#$Foglio1.A1:Foglio1.B9\"/>");

        var pivot = Assert.Single(workbook.Pivots);
        Assert.Equal(PivotExternalKind.Workbook, pivot.ExternalSource!.Kind);
        Assert.Equal("file:///dati/vendite.ods", pivot.ExternalSource.DatabaseName);
        Assert.Equal("Foglio1", pivot.SourceSheet);
        Assert.Equal(new CellRange(CellAddress.First, new CellAddress(8, 1)), pivot.SourceRange);

        var again = Assert.Single(RoundTrip(workbook).Pivots);
        Assert.Equal((pivot.ExternalSource, pivot.SourceSheet, pivot.SourceRange), (again.ExternalSource, again.SourceSheet, again.SourceRange));
    }

    [Fact]
    public void IlFiltroStandardDiUnAreaDiDatabaseConNomeSiDichiara()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Text("Valore"));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Archivio", SheetIndex = 0, Range = new CellRange(CellAddress.First, new CellAddress(9, 1)) });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            var start = content.IndexOf("table:name=\"Archivio\"", StringComparison.Ordinal);
            var end = content.IndexOf("/>", start, StringComparison.Ordinal);
            content = content[..end] + "><table:filter><table:filter-condition table:field-number=\"0\" table:value=\"6\" table:operator=\"&lt;\" table:data-type=\"number\"/></table:filter></table:database-range>" + content[(end + 2)..];
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded);
        var range = result.Value!.DatabaseRanges.Find("Archivio")!;
        Assert.Equal("<6", Assert.Single(range.Filters).Criteria);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "ODF081");
    }
}
