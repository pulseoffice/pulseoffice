using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsSpreadsheetAuditTests
{
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;

    private Workbook Reopened(Workbook workbook, Action<XDocument> change, string part = "content.xml")
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(part)!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            change(content);
            entry.Delete();
            using var output = archive.CreateEntry(part).Open();
            content.Save(output);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return result.Value!;
    }

    [Theory]
    [InlineData("row", "5000000")]
    [InlineData("column", "2000000000")]
    public void Una_modifica_registrata_fuori_dal_foglio_non_entra_nel_registro(string attribute, string value)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(new CellAddress(1, 1), CellValue.Number(20));
        workbook.Changes.Import([new TrackedChange
        {
            Id = "ct1",
            SheetIndex = 0,
            Address = new CellAddress(1, 1),
            Before = CellValue.Number(10),
            After = CellValue.Number(20),
            At = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
        }]);

        var back = Reopened(workbook, content =>
        {
            foreach (var address in content.Descendants(_table + "cell-address")) address.SetAttributeValue(_table + attribute, value);
        });

        Assert.Empty(back.Changes.Changes);
    }

    [Theory]
    [InlineData("Infinity%", 4.0)]
    [InlineData("1e300%", 4.0)]
    [InlineData("1%", 0.1)]
    public void La_scala_di_stampa_letta_resta_fra_dieci_e_quattrocento_per_cento(string scale, double expected)
    {
        var back = Reopened(Workbook.CreateDefault(), styles =>
        {
            foreach (var properties in styles.Descendants(OdfPackage.Namespaces.Style + "page-layout-properties"))
                properties.SetAttributeValue(OdfPackage.Namespaces.Style + "scale-to", scale);
        }, "styles.xml");

        Assert.Equal(expected, back.PageStyles.Default.Setup.Scale);
    }

    private XElement Table(XDocument content) => content.Descendants(_table + "table").First();

    [Fact]
    public void Righe_di_intestazione_ripetute_a_dismisura_non_fermano_la_lettura()
    {
        var back = Reopened(Workbook.CreateDefault(), content =>
        {
            var table = Table(content);
            var header = new XElement(_table + "table-header-rows",
                new XElement(_table + "table-row", new XAttribute(_table + "number-rows-repeated", "2000000000"), new XElement(_table + "table-cell")),
                new XElement(_table + "table-row", new XAttribute(_table + "number-rows-repeated", "2000000000"), new XElement(_table + "table-cell")));
            var firstRow = table.Elements().FirstOrDefault(element => element.Name == _table + "table-row");
            if (firstRow is null) table.Add(header);
            else firstRow.AddBeforeSelf(header);
        });

        Assert.InRange(back.Sheets[0].RepeatedPrintRows, 0, CellAddress.MaxRows);
    }

    [Fact]
    public void Una_formula_matrice_in_fondo_al_foglio_resta_dentro_il_foglio()
    {
        var workbook = Workbook.CreateDefault();
        var last = new CellAddress(CellAddress.MaxRows - 1, 0);
        workbook.Sheets[0].SetFormula(last, "=1+1");

        var back = Reopened(workbook, content =>
        {
            var cell = content.Descendants(_table + "table-cell").First(element => element.Attribute(_table + "formula") is not null);
            cell.SetAttributeValue(_table + "number-matrix-rows-spanned", "10");
            cell.SetAttributeValue(_table + "number-matrix-columns-spanned", "1");
        });

        var range = back.Sheets[0][last].ArrayFormulaRange;
        Assert.NotNull(range);
        Assert.True(range!.Value.End.IsValid);
    }
}
