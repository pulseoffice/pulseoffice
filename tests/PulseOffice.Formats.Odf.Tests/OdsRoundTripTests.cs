using System.IO.Compression;
using System.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsRoundTripTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void IlMimetypeELaPrimaVoceEnonECompresso()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Workbook.CreateDefault(), stream);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var first = archive.Entries[0];
        Assert.Equal("mimetype", first.Name);

        Assert.Equal(first.Length, first.CompressedLength);
    }

    [Fact]
    public void IlPacchettoContieneLePartiRichieste()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Workbook.CreateDefault(), stream);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("content.xml", names);
        Assert.Contains("styles.xml", names);
        Assert.Contains("meta.xml", names);
        Assert.Contains("META-INF/manifest.xml", names);
    }

    [Fact]
    public void INumeriSopravvivonoAlGiroCompleto()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(42));
        source.Sheets[0].SetValue(A("B2"), CellValue.Number(-3.5));
        source.Sheets[0].SetValue(A("C3"), CellValue.Number(1e-9));

        var result = RoundTrip(source);

        Assert.Equal(42, result.Sheets[0][A("A1")].Value.AsNumber());
        Assert.Equal(-3.5, result.Sheets[0][A("B2")].Value.AsNumber());
        Assert.Equal(1e-9, result.Sheets[0][A("C3")].Value.AsNumber(), 15);
    }

    [Fact]
    public void IlTestoSopravviveAncheConCaratteriParticolari()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Text("città & società <xml>"));

        var result = RoundTrip(source);
        Assert.Equal("città & società <xml>", result.Sheets[0][A("A1")].Value.AsText());
    }

    [Fact]
    public void IBooleaniMantengonoIlLoroTipo()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Boolean(true));

        var result = RoundTrip(source);
        var value = result.Sheets[0][A("A1")].Value;

        Assert.Equal(ValueKind.Boolean, value.Kind);
        Assert.True(value.AsBoolean());
    }

    [Fact]
    public void LeFormuleSiSalvanoNellaSintassiDiOdf()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetFormula(A("C1"), "=SUM(A1:A3)");

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);
        var content = reader.ReadToEnd();

        Assert.Contains("of:=SUM([.A1:.A3])", content, StringComparison.Ordinal);
    }

    [Fact]
    public void LoSpazioDeiNomiDelleFormuleEDichiarato()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetFormula(A("A1"), "=1+1");

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);
        var content = reader.ReadToEnd();

        Assert.Contains("urn:oasis:names:tc:opendocument:xmlns:of:1.2", content, StringComparison.Ordinal);
    }

    [Fact]
    public void LeFormuleTornanoNellaNostraSintassi()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetFormula(A("C1"), "=SUM(A1:A3)");

        var result = RoundTrip(source);
        Assert.Equal("=SUM(A1:A3)", result.Sheets[0][A("C1")].Formula);
    }

    [Fact]
    public void IRiferimentiAdAltriFogliSopravvivono()
    {
        var source = Workbook.CreateDefault();
        source.AddSheet("Dati");
        source.Sheets[0].SetFormula(A("A1"), "=Dati.B2*2");

        var result = RoundTrip(source);
        Assert.Equal("=Dati.B2*2", result.Sheets[0][A("A1")].Formula);
    }

    [Fact]
    public void PiuFogliMantengonoOrdineENomi()
    {
        var source = new Workbook();
        source.AddSheet("Primo");
        source.AddSheet("Secondo");
        source.AddSheet("Terzo");

        var result = RoundTrip(source);

        Assert.Equal(3, result.Sheets.Count);
        Assert.Equal(["Primo", "Secondo", "Terzo"], result.Sheets.Select(s => s.Name));
    }

    [Fact]
    public void LeCelleLontaneNonGeneranoUnFileEnorme()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1));
        source.Sheets[0].SetValue(new CellAddress(5000, 0), CellValue.Number(2));

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        Assert.True(stream.Length < 20_000, $"Il file pesa {stream.Length} byte.");

        stream.Position = 0;
        var result = new OdsReader().Read(stream).Value!;
        Assert.Equal(2, result.Sheets[0][new CellAddress(5000, 0)].Value.AsNumber());
    }

    [Fact]
    public void LeCelleVuoteInMezzoAllaRigaRestanoAlLoroPosto()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1));
        source.Sheets[0].SetValue(A("E1"), CellValue.Number(5));

        var result = RoundTrip(source);

        Assert.Equal(1, result.Sheets[0][A("A1")].Value.AsNumber());
        Assert.True(result.Sheets[0][A("C1")].Value.IsEmpty);
        Assert.Equal(5, result.Sheets[0][A("E1")].Value.AsNumber());
    }

    [Fact]
    public void UnDocumentoDiUnAltroTipoVieneRifiutato()
    {
        using var stream = new MemoryStream();

        using (var package = OdfPackage.Create(stream, OdfPackage.MimeTypes.Text))
            package.WriteXml("content.xml", new System.Xml.Linq.XDocument(
                new System.Xml.Linq.XElement("vuoto")));

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, d => d.Code == "ODF001");
    }

    [Fact]
    public void UnFoglioVuotoSiSalvaESiRilegge()
    {
        var result = RoundTrip(Workbook.CreateDefault());

        Assert.Single(result.Sheets);
        Assert.Equal(0, result.Sheets[0].CellCount);
    }
}

public class OdfFormulaSyntaxTests
{
    [Theory]
    [InlineData("=SUM(A1:A3)", "of:=SUM([.A1:.A3])")]
    [InlineData("=A1+B2", "of:=[.A1]+[.B2]")]
    [InlineData("=Dati.A1", "of:=[Dati.A1]")]
    [InlineData("=$A$1*2", "of:=[.$A$1]*2")]
    public void IRiferimentiSiConvertonoNeiDueVersi(string ours, string odf)
    {
        Assert.Equal(odf, new OdfFormulaSyntax().ToOdf(ours));
        Assert.Equal(ours, new OdfFormulaSyntax().ToInternal(odf));
    }

    [Fact]
    public void IlTestoDentroLaFormulaNonVieneToccato()
    {
        var converted = new OdfFormulaSyntax().ToOdf("=IF(A1;\"A1 non trovato\";0)");

        Assert.Contains("[.A1]", converted, StringComparison.Ordinal);
        Assert.Contains("\"A1 non trovato\"", converted, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaFormulaVuotaNonEUnaFormula() =>
        Assert.Null(new OdfFormulaSyntax().ToInternal(""));

    [Fact]
    public void INomiDiFoglioConSpaziVannoFraApici()
    {
        var internalForm = new OdfFormulaSyntax().ToInternal("of:=[Foglio con spazi.A1]");
        Assert.Equal("='Foglio con spazi'.A1", internalForm);
    }

    [Theory]
    [InlineData("of:=[$Dati.A1]+SUM([$Dati.B1:.B2])", "=Dati.A1+SUM(Dati.B1:B2)")]
    [InlineData("of:=[$'Foglio con spazi'.A1]", "='Foglio con spazi'.A1")]
    public void IlFoglioAssolutoEsternoSiLeggeComeFoglio(string odf, string ours) =>
        Assert.Equal(ours, new OdfFormulaSyntax().ToInternal(odf));
}

public class OdsStyleTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void IlCarattereSopravviveAlGiroCompleto()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle
        {
            Font = new PulseOffice.Sheets.Styling.CellFont
            {
                Family = "Arial",
                Size = 16,
                Bold = true,
                ColorHex = "#0000ff",
            },
        });

        source.Sheets[0].SetValue(A("A1"), CellValue.Text("Titolo"));
        source.Sheets[0].SetStyle(A("A1"), style);

        var font = RoundTrip(source).Styles.Get(RoundTrip(source).Sheets[0][A("A1")].StyleId).Font;

        Assert.Equal("Arial", font.Family);
        Assert.Equal(16, font.Size);
        Assert.True(font.Bold);
        Assert.Equal("#0000ff", font.ColorHex);
    }

    [Fact]
    public void LAllineamentoStaNelParagrafoNonNellaCella()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle
        {
            Horizontal = PulseOffice.Sheets.Styling.CellHorizontalAlignment.Center,
        });

        source.Sheets[0].SetValue(A("A1"), CellValue.Text("Centrato"));
        source.Sheets[0].SetStyle(A("A1"), style);

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);
        var content = reader.ReadToEnd();

        Assert.Contains("paragraph-properties", content, StringComparison.Ordinal);
        Assert.Contains("text-align=\"center\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void LoSfondoEIBordiSopravvivono()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle
        {
            BackgroundHex = "#ffff00",
            Borders = new PulseOffice.Sheets.Styling.CellBorders(
                new PulseOffice.Sheets.Styling.BorderEdge(
                    PulseOffice.Sheets.Styling.BorderStyle.Thin, "#ff0000",
                    PulseOffice.Foundation.Units.Twip.FromPoints(1)),
                PulseOffice.Sheets.Styling.BorderEdge.None,
                PulseOffice.Sheets.Styling.BorderEdge.None,
                PulseOffice.Sheets.Styling.BorderEdge.None),
        });

        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1));
        source.Sheets[0].SetStyle(A("A1"), style);

        var read = RoundTrip(source);
        var result = read.Styles.Get(read.Sheets[0][A("A1")].StyleId);

        Assert.Equal("#ffff00", result.BackgroundHex);
        Assert.True(result.Borders.Left.IsVisible);
        Assert.Equal("#ff0000", result.Borders.Left.ColorHex);
    }

    [Fact]
    public void LeDiagonaliSopravvivono()
    {
        var source = Workbook.CreateDefault();
        var pen = new PulseOffice.Sheets.Styling.BorderEdge(
            PulseOffice.Sheets.Styling.BorderStyle.Double, "#0000ff", PulseOffice.Foundation.Units.Twip.FromPoints(2.25));
        var style = source.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle
        {
            Borders = PulseOffice.Sheets.Styling.CellBorders.None with { DiagonalDown = pen },
        });

        source.Sheets[0].SetStyle(A("C3"), style);

        var read = RoundTrip(source);
        var result = read.Styles.Get(read.Sheets[0][A("C3")].StyleId);

        Assert.False(result.Borders.DiagonalUp.IsVisible);
        Assert.Equal(PulseOffice.Sheets.Styling.BorderStyle.Double, result.Borders.DiagonalDown.Style);
        Assert.Equal("#0000ff", result.Borders.DiagonalDown.ColorHex);
    }

    [Fact]
    public void UnaCellaVuotaMaFormattataNonVienePersa()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle { BackgroundHex = "#00ff00" });

        source.Sheets[0].SetStyle(A("B2"), style);

        var read = RoundTrip(source);
        Assert.Equal("#00ff00", read.Styles.Get(read.Sheets[0][A("B2")].StyleId).BackgroundHex);
    }

    [Fact]
    public void LoStilePredefinitoNonVieneDichiarato()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1));

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);

        Assert.DoesNotContain("style:family=\"table-cell\"", reader.ReadToEnd(), StringComparison.Ordinal);
    }
}
