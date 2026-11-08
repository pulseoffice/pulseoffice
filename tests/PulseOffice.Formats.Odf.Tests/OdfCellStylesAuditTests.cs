using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfCellStylesAuditTests
{
    private readonly XNamespace _style = OdfPackage.Namespaces.Style;
    private readonly XNamespace _fo = OdfPackage.Namespaces.Fo;
    private readonly XNamespace _number = OdfPackage.Namespaces.Number;
    private readonly CellAddress _a1 = new(0, 0);

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Una_rotazione_non_finita_non_entra_nel_modello(string angle)
    {
        var style = ReopenedStyle(content => Cells(content).ForEach(cell => Properties(cell, "table-cell-properties").SetAttributeValue(_style + "rotation-angle", angle)));

        Assert.InRange(style.RotationDegrees, -180, 180);
    }

    [Theory]
    [InlineData("NaNpt")]
    [InlineData("Infinitypt")]
    [InlineData("1e300pt")]
    [InlineData("-5pt")]
    public void Lo_spessore_del_bordo_resta_nei_limiti(string width)
    {
        var style = ReopenedStyle(content => Cells(content).ForEach(cell => Properties(cell, "table-cell-properties").SetAttributeValue(_fo + "border", width + " solid #000000")));

        Assert.InRange(style.Borders.Left.Width.ToPoints(), 0, 72);
    }

    [Theory]
    [InlineData("Infinity%")]
    [InlineData("1e300%")]
    [InlineData("1e300pt")]
    public void La_dimensione_del_carattere_della_cella_ha_un_tetto(string size)
    {
        var style = ReopenedStyle(content => Cells(content).ForEach(cell => Properties(cell, "text-properties").SetAttributeValue(_fo + "font-size", size)));

        Assert.InRange(style.Font.Size, 1, 4000);
    }

    [Fact]
    public void Un_nome_di_stile_con_un_surrogato_non_blocca_l_apertura()
    {
        var reader = new OdfCellStyleReader();
        var content = new XDocument(new XElement(OdfPackage.Namespaces.Office + "document-content",
            new XElement(_style + "style", new XAttribute(_style + "name", "ce_d800_"), new XAttribute(_style + "family", "table-cell"))));

        var styles = reader.ReadStyles(content);

        Assert.True(styles.ContainsKey("ce_d800_"));
    }

    [Theory]
    [InlineData("Infinity")]
    [InlineData("1e300")]
    public void Un_fattore_di_scala_enorme_non_blocca_l_apertura(string factor)
    {
        var code = ReopenedCode(content => Numbers(content).ForEach(number => number.SetAttributeValue(_number + "display-factor", factor)));

        Assert.True(code.Length < 64, code);
    }

    [Fact]
    public void Il_denominatore_fisso_non_inietta_sezioni_nel_codice()
    {
        var code = ReopenedCode(content => Numbers(content).ForEach(number => number.ReplaceWith(new XElement(_number + "fraction",
            new XAttribute(_number + "min-integer-digits", "0"), new XAttribute(_number + "min-numerator-digits", "1"),
            new XAttribute(_number + "denominator-value", "4;[Red]0")))));

        Assert.DoesNotContain(";", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Il_simbolo_di_valuta_non_inietta_sezioni_nel_codice()
    {
        var code = ReopenedCode(content => Numbers(content).ForEach(number => number.AddAfterSelf(new XElement(_number + "currency-symbol", "x];0;[Red"))));

        Assert.DoesNotContain(";", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Due_ore_trascorse_nello_stesso_formato_si_salvano()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(_a1, CellValue.Number(1.5));
        sheet.SetStyle(_a1, workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("[h]:mm \"e\" [hh]") }));

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        Assert.True(new OdsReader().Read(stream).Succeeded);
    }

    private List<XElement> Cells(XDocument content) =>
        [.. content.Descendants(_style + "style").Where(style => (string?)style.Attribute(_style + "family") == "table-cell")];

    private List<XElement> Numbers(XDocument content) =>
        [.. content.Descendants(_number + "number").Where(number => number.Parent?.Name == _number + "number-style")];

    private XElement Properties(XElement style, string name)
    {
        if (style.Element(_style + name) is { } existing) return existing;
        var created = new XElement(_style + name);
        style.Add(created);
        return created;
    }

    private CellStyle ReopenedStyle(Action<XDocument> change)
    {
        var workbook = Reopened(change);
        return workbook.Styles.Get(workbook.Sheets[0].StyleIdAt(_a1));
    }

    private string ReopenedCode(Action<XDocument> change)
    {
        var workbook = Reopened(change);
        return workbook.NumberFormats.CodeOf(workbook.Styles.Get(workbook.Sheets[0].StyleIdAt(_a1)).NumberFormatId);
    }

    private Workbook Reopened(Action<XDocument> change)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(_a1, CellValue.Number(1234.5));
        sheet.SetStyle(_a1, workbook.Styles.Intern(workbook.Styles.Get(0) with
        {
            NumberFormatId = workbook.NumberFormats.Register("#,##0.000"),
            Font = CellFont.Default with { Bold = true },
        }));
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
            foreach (var part in new[] { "content.xml", "styles.xml" })
            {
                var entry = archive.GetEntry(part)!;
                XDocument content;
                using (var input = entry.Open()) content = XDocument.Load(input);
                change(content);
                entry.Delete();
                using var output = archive.CreateEntry(part).Open();
                content.Save(output, SaveOptions.DisableFormatting);
            }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }
}
