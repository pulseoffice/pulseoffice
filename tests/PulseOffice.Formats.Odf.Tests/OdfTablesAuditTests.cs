using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfTablesAuditTests
{
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;
    private readonly XNamespace _office = OdfPackage.Namespaces.Office;

    [Fact]
    public void A_sheet_with_many_distinct_rows_reads_in_linear_time()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        for (var row = 0; row < 60_000; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new OdsReader().Read(stream);

        Assert.True(watch.ElapsedMilliseconds < 10_000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal(59_999, result.Value!.Sheets[0].GetValue(new CellAddress(59_999, 0)).AsNumber());
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1,000")]
    [InlineData("(5)")]
    public void Un_valore_numerico_non_valido_non_entra_nello_scenario(string value)
    {
        var workbook = Reopened(WithScenario(), content =>
        {
            var cell = content.Descendants(_table + "table").Single(table => table.Element(_table + "scenario") is not null)
                .Descendants(_table + "table-cell").First(cell => (string?)cell.Attribute(_office + "value") == "10");
            cell.SetAttributeValue(_office + "value", value);
        });

        var stored = workbook.Sheets[0].Scenarios[0].Values.GetValueOrDefault(CellAddress.First);
        Assert.True(stored.IsEmpty || double.IsFinite(stored.AsNumber()) && stored.AsNumber() != 1000 && stored.AsNumber() != -5);
    }

    [Fact]
    public void Gruppi_di_righe_annidati_senza_fine_non_fanno_traboccare_la_pila()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").SetValue(CellAddress.First, CellValue.Number(1));

        var reopened = Reopened(workbook, content =>
        {
            var table = content.Descendants(_table + "table").First();
            var group = new XElement(_table + "table-row-group", new XElement(_table + "table-row", new XElement(_table + "table-cell")));
            for (var depth = 0; depth < 50_000; depth++) group = new XElement(_table + "table-row-group", group);
            table.Add(group);
        });

        Assert.Equal(1, reopened.Sheets[0].GetValue(CellAddress.First).AsNumber());
    }

    [Theory]
    [InlineData(@"C:\documenti\a.ods")]
    [InlineData(@"\\server\condivisa\a.ods")]
    [InlineData("/Users/mario/a.ods")]
    public void Un_percorso_assoluto_di_qualunque_sistema_non_diventa_relativo(string link)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati")[CellAddress.First] = new Cell { Value = CellValue.Text("vai"), Hyperlink = link };
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var input = archive.GetEntry("content.xml")!.Open();
        var href = XDocument.Load(input).Descendants().Attributes(OdfPackage.Namespaces.Xlink + "href").Select(attribute => attribute.Value).Single();

        Assert.DoesNotContain("../", href, StringComparison.Ordinal);
    }

    private Workbook WithScenario()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.Scenarios.Add(new Scenario
        {
            Name = "Prova",
            Range = new CellRange(CellAddress.First, new CellAddress(1, 0)),
            Values = new Dictionary<CellAddress, CellValue> { [CellAddress.First] = CellValue.Number(10) },
        });
        return workbook;
    }

    private Workbook Reopened(Workbook workbook, Action<XDocument> change)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            change(content);
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            content.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }
}
