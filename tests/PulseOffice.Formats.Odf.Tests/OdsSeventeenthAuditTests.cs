using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsSeventeenthAuditTests
{
    private static readonly XNamespace Number = "urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    private static CellAddress At(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static byte[] Write(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        return stream.ToArray();
    }

    private static Workbook Read(byte[] package)
    {
        var result = new OdsReader().Read(new MemoryStream(package));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static string Entry(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    private static byte[] Patch(byte[] package, Func<string, string> content)
    {
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        using (var source = new ZipArchive(new MemoryStream(package)))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var stream = copy.Open();
                if (entry.FullName == "content.xml")
                {
                    using var reader = new StreamReader(entry.Open());
                    using var writer = new StreamWriter(stream);
                    writer.Write(content(reader.ReadToEnd()));
                }
                else entry.Open().CopyTo(stream);
            }
        }
        return output.ToArray();
    }

    private static string CodeAt(Workbook workbook, string a1) =>
        workbook.NumberFormats.CodeOf(workbook.Styles.Get(workbook.Sheets[0].StyleIdAt(At(a1))).NumberFormatId);

    [Fact]
    public void GliStiliNumericiDiStylesEContentNonHannoLoStessoNome()
    {
        var workbook = Workbook.CreateDefault();
        var currency = workbook.NumberFormats.Register("#,##0.00 [$€-410]");
        var date = workbook.NumberFormats.Register("dd/mm/yyyy");
        workbook.NamedStyles.Define("Soldi", new CellStyle { NumberFormatId = currency });
        workbook.Sheets[0].SetValue(At("A1"), CellValue.Number(42212));
        workbook.Sheets[0].SetStyle(At("A1"), workbook.Styles.Intern(new CellStyle { NumberFormatId = date }));

        var package = Write(workbook);
        string[] Names(string part) =>
        [
            .. XDocument.Parse(Entry(package, part)).Descendants()
                .Where(element => element.Name.Namespace == Number && element.Name.LocalName.EndsWith("-style", StringComparison.Ordinal))
                .Select(element => (string)element.Attribute(Style + "name")!),
        ];

        Assert.Empty(Names("styles.xml").Intersect(Names("content.xml")));
        Assert.Equal("dd/mm/yyyy", CodeAt(Read(package), "A1"));
    }

    [Fact]
    public void UnNumeroSenzaDecimaliDichiaratiEGenerale()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(At("A1"), CellValue.Number(1.5));
        workbook.Sheets[0].SetStyle(At("A1"), workbook.Styles.Intern(new CellStyle { NumberFormatId = workbook.NumberFormats.Register("0") }));

        var patched = Patch(Write(workbook), content => content.Replace(
            "<number:number number:decimal-places=\"0\" number:min-decimal-places=\"0\" number:min-integer-digits=\"1\" />",
            "<number:number number:min-integer-digits=\"1\" />", StringComparison.Ordinal));

        Assert.Equal(0, Read(patched).Styles.Get(Read(patched).Sheets[0].StyleIdAt(At("A1"))).NumberFormatId);
    }

    [Theory]
    [InlineData("0.00;[RED]-0.00;\"zero\"")]
    [InlineData("#,##0.00 [$€-410];[RED]-#,##0.00 [$€-410]")]
    [InlineData("[$-407]#,##0.00")]
    [InlineData("General\" kg\"")]
    public void LeSezioniELaLinguaDelFormatoSopravvivono(string code)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(At("A1"), CellValue.Number(-2));
        workbook.Sheets[0].SetStyle(At("A1"), workbook.Styles.Intern(new CellStyle { NumberFormatId = workbook.NumberFormats.Register(code) }));

        Assert.Equal(code, CodeAt(Read(Write(workbook)), "A1"));
    }

    [Fact]
    public void LaMappaEsternoConLaSezionePositivaSiLegge()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(At("A1"), CellValue.Number(-2));
        workbook.Sheets[0].SetStyle(At("A1"), workbook.Styles.Intern(new CellStyle { NumberFormatId = workbook.NumberFormats.Register("0.00;[RED]-0.00") }));

        var content = Entry(Write(workbook), "content.xml");
        Assert.Contains("value()&gt;=0", content, StringComparison.Ordinal);
    }

    [Fact]
    public void UnPercentoLetteraleNonDiventaUnaPercentuale()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(At("A1"), CellValue.Number(5));
        workbook.Sheets[0].SetStyle(At("A1"), workbook.Styles.Intern(new CellStyle { NumberFormatId = workbook.NumberFormats.Register("0.00\" kg\"") }));

        var patched = Patch(Write(workbook), content => content.Replace("<number:text> kg</number:text>", "<number:text> %</number:text>", StringComparison.Ordinal));

        Assert.Equal("0.00\" %\"", CodeAt(Read(patched), "A1"));
    }

    [Theory]
    [InlineData("8", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void IlValoreLogicoNumericoSiLegge(string stored, bool expected)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(At("A1"), CellValue.Boolean(false));

        var patched = Patch(Write(workbook), content => content.Replace("office:boolean-value=\"false\"", $"office:boolean-value=\"{stored}\"", StringComparison.Ordinal));

        Assert.Equal(expected, Read(patched).Sheets[0][At("A1")].Value.AsBoolean());
    }

    [Fact]
    public void IRiferimentiTridimensionaliSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Altro");
        workbook.Sheets[0].SetFormula(At("C1"), "=SUM(Sheet1.A1:Altro.B2)");

        var package = Write(workbook);

        Assert.Contains("[Sheet1.A1:Altro.B2]", Entry(package, "content.xml"), StringComparison.Ordinal);
        Assert.Equal("=SUM(Sheet1.A1:Altro.B2)", Read(package).Sheets[0][At("C1")].Formula);
    }

    [Theory]
    [InlineData("of:=SUM([.C:.C])", "=SUM(C:C)")]
    [InlineData("of:=SUM([.2:.2])", "=SUM(2:2)")]
    [InlineData("of:=SUM([Dati.$C:.$D])", "=SUM(Dati.$C:$D)")]
    [InlineData("of:=SUM(_xlfn.ANCHORARRAY([.B2]))", "=SUM(B2#)")]
    [InlineData("of:=SUM(#REF!)", "=SUM(#REF!)")]
    [InlineData("of:=SUM([.A1:.A2]~[.B1:.B2])", "=SUM(A1:A2~B1:B2)")]
    [InlineData("of:=SUM([Primo.A1:Terzo.A1])", "=SUM(Primo.A1:Terzo.A1)")]
    public void LeColonneELeRigheIntereSopravvivono(string odf, string internalFormula)
    {
        var syntax = new OdfFormulaSyntax();
        Assert.Equal(internalFormula, syntax.ToInternal(odf));
        Assert.Equal(odf, syntax.ToOdf(internalFormula));
    }

    [Fact]
    public void UnRiferimentoCancellatoEsternoDiventaUnErrore() =>
        Assert.Equal("=A1+#REF!", new OdfFormulaSyntax().ToInternal("of:=[.A1]+[#REF!]"));

    [Fact]
    public void UnErroreScrittoSoloNelTestoDiUnaFormulaSiRiconosce()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetFormula(At("A1"), "=1/0");
        workbook.Sheets[0][At("A1")] = workbook.Sheets[0][At("A1")] with { Value = CellValue.Number(0) };

        var patched = Patch(Write(workbook), content => content.Replace(
            "office:value=\"0\"><text:p>0</text:p>", "office:value=\"0\"><text:p>#DIV/0!</text:p>", StringComparison.Ordinal));

        Assert.Equal(CellError.DivideByZero, Read(patched).Sheets[0][At("A1")].Value.Error);
    }

    [Fact]
    public void LeFunzioniDegliAddInEsternoHannoIlLoroPrefisso()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetFormula(At("A1"), "=ROT13(\"bacon\")");
        workbook.Sheets[0].SetFormula(At("A2"), "=DAYSINMONTH(A3)");

        var package = Write(workbook);
        var content = Entry(package, "content.xml");

        Assert.Contains("ORG.OPENOFFICE.ROT13(", content, StringComparison.Ordinal);
        Assert.Contains("ORG.OPENOFFICE.DAYSINMONTH(", content, StringComparison.Ordinal);
        Assert.Equal("=ROT13(\"bacon\")", Read(package).Sheets[0][At("A1")].Formula);
    }

    [Fact]
    public void LeCelleDiUnaMatriceInErroreNonDiventanoFormule()
    {
        var workbook = Workbook.CreateDefault();
        Assert.True(CellRange.TryParseA1("A1:A3", out var range));
        workbook.Sheets[0].SetArrayFormula(range, "=SMALL(B1:B3;C1:C3)");
        foreach (var address in range.Cells())
            workbook.Sheets[0][address] = workbook.Sheets[0][address] with { Value = CellValue.FromError(CellError.Value) };

        var cells = XDocument.Parse(Entry(Write(workbook), "content.xml")).Descendants(Table + "table-cell")
            .Where(cell => cell.Attribute(Table + "formula") is not null).ToList();

        Assert.Single(cells);
    }

    [Fact]
    public void UnaCellaCheEreditaLoStileDellaColonnaNonLoRipete()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetColumnStyle(2, workbook.Styles.Intern(new CellStyle { Font = CellFont.Default with { Bold = true } }));
        sheet.SetFormula(At("C1"), "=A1-B1");

        var cell = XDocument.Parse(Entry(Write(workbook), "content.xml")).Descendants(Table + "table-cell")
            .Single(element => element.Attribute(Table + "formula") is not null);

        Assert.Null(cell.Attribute(Table + "style-name"));
    }

    [Fact]
    public void UnaFormulaConRisultatoDataNonRiceveUnFormatoImplicito()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetFormula(At("A1"), "=B1");
        workbook.Sheets[0][At("A1")] = workbook.Sheets[0][At("A1")] with { Value = CellValue.Number(42212) };

        var patched = Patch(Write(workbook), content => content.Replace(
            "office:value-type=\"float\" office:value=\"42212\"",
            "office:value-type=\"date\" office:date-value=\"2015-07-27\"", StringComparison.Ordinal));

        Assert.Equal(0, Read(patched).Styles.Get(Read(patched).Sheets[0].StyleIdAt(At("A1"))).NumberFormatId);
    }

    [Fact]
    public void LeMacroBasicScartateSonoDichiarate()
    {
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        using (var source = new ZipArchive(new MemoryStream(Write(Workbook.CreateDefault()))))
        {
            foreach (var entry in source.Entries)
            {
                using var stream = target.CreateEntry(entry.FullName).Open();
                entry.Open().CopyTo(stream);
            }
            using var basic = new StreamWriter(target.CreateEntry("Basic/script-lc.xml").Open());
            basic.Write("<library:libraries xmlns:library=\"http://openoffice.org/2000/library\"/>");
        }

        var result = new OdsReader().Read(new MemoryStream(output.ToArray()));

        Assert.True(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "ODF041");
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("2000000000")]
    public void UnNumeroDiCifreFuoriMisuraNelFormatoNonFermaLaLettura(string digits)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(At("A1"), CellValue.Number(1.5));
        workbook.Sheets[0].SetStyle(At("A1"), workbook.Styles.Intern(new CellStyle { NumberFormatId = workbook.NumberFormats.Register("0.00") }));

        var patched = Patch(Write(workbook), content => content.Replace(
            "number:min-integer-digits=\"1\"", $"number:min-integer-digits=\"{digits}\"", StringComparison.Ordinal));

        Assert.True(CodeAt(Read(patched), "A1").Length < 200);
    }
}
