using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Styling = PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxRoundTripTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void INumeriSopravvivonoAlGiroCompleto()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(42));
        source.Sheets[0].SetValue(A("B2"), CellValue.Number(-3.5));

        var result = RoundTrip(source);

        Assert.Equal(42, result.Sheets[0][A("A1")].Value.AsNumber());
        Assert.Equal(-3.5, result.Sheets[0][A("B2")].Value.AsNumber());
    }

    [Fact]
    public void IlTestoPassaDallaTabellaCondivisa()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Text("Ciao"));
        source.Sheets[0].SetValue(A("A2"), CellValue.Text("Ciao"));
        source.Sheets[0].SetValue(A("A3"), CellValue.Text("Mondo"));

        var result = RoundTrip(source);

        Assert.Equal("Ciao", result.Sheets[0][A("A1")].Value.AsText());
        Assert.Equal("Ciao", result.Sheets[0][A("A2")].Value.AsText());
        Assert.Equal("Mondo", result.Sheets[0][A("A3")].Value.AsText());
    }

    [Fact]
    public void LeStringheUgualiCondividonoUnaSolaVoce()
    {
        var source = Workbook.CreateDefault();
        for (var i = 0; i < 50; i++)
            source.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Text("ripetuta"));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        Assert.True(stream.Length < 12_000, $"Il file pesa {stream.Length} byte.");
    }

    [Fact]
    public void IBooleaniMantengonoIlLoroTipo()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Boolean(true));
        source.Sheets[0].SetValue(A("A2"), CellValue.Boolean(false));

        var result = RoundTrip(source);

        Assert.True(result.Sheets[0][A("A1")].Value.AsBoolean());
        Assert.False(result.Sheets[0][A("A2")].Value.AsBoolean());
        Assert.Equal(ValueKind.Boolean, result.Sheets[0][A("A2")].Value.Kind);
    }

    [Fact]
    public void LeFormuleSopravvivonoAlGiroCompleto()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetFormula(A("C1"), "=SUM(A1:A3)");

        var result = RoundTrip(source);
        Assert.Equal("=SUM(A1:A3)", result.Sheets[0][A("C1")].Formula);
    }

    [Fact]
    public void IRiferimentiAdAltriFogliCambianoSeparatore()
    {
        Assert.Equal("Dati!B2*2", new XlsxFormulaSyntax().ToOpenXml("=Dati.B2*2"));
        Assert.Equal("=Dati.B2*2", new XlsxFormulaSyntax().ToInternal("Dati!B2*2"));
    }

    [Fact]
    public void GliArgomentiCambianoSeparatore()
    {
        Assert.Equal("SUM(1,2,3)", new XlsxFormulaSyntax().ToOpenXml("=SUM(1;2;3)"));
        Assert.Equal("=SUM(1;2;3)", new XlsxFormulaSyntax().ToInternal("SUM(1,2,3)"));
    }

    [Fact]
    public void IlTestoDentroLaFormulaNonVieneToccato()
    {
        var converted = new XlsxFormulaSyntax().ToOpenXml("=IF(A1;\"1,50 euro\";\"\")");

        Assert.Contains("\"1,50 euro\"", converted, StringComparison.Ordinal);
        Assert.Contains("IF(A1,", converted, StringComparison.Ordinal);
    }

    [Fact]
    public void PiuFogliMantengonoOrdineENomi()
    {
        var source = new Workbook();
        source.AddSheet("Primo");
        source.AddSheet("Secondo");

        var result = RoundTrip(source);
        Assert.Equal(["Primo", "Secondo"], result.Sheets.Select(s => s.Name));
    }

    [Fact]
    public void GliErroriMantengonoIlLoroCodice()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.FromError(CellError.DivideByZero));

        var result = RoundTrip(source);
        Assert.Equal(CellError.DivideByZero, result.Sheets[0][A("A1")].Value.Error);
    }

    [Fact]
    public void IlRiconoscimentoDelFormatoGuardaIlContenuto()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;

        Assert.True(new XlsxSpreadsheetReader().CanRead(stream));

        using var other = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(
            other, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            archive.CreateEntry("qualcosa.txt");

        other.Position = 0;
        Assert.False(new XlsxSpreadsheetReader().CanRead(other));
    }

    [Fact]
    public void CioCheIlFormatoNonReggeVieneDichiarato()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].QueryBindings.AddOrUpdate(new QueryBinding
        {
            Id = "q1",
            DisplayName = "Vendite",
            ConnectionId = "c1",
            Query = new PulseOffice.Data.QueryDefinition { Sql = "SELECT 1" },
            Anchor = A("A1"),
        });

        var report = new XlsxSpreadsheetWriter().CheckFidelity(source);

        Assert.False(report.IsPerfect);
        Assert.Contains(report.Losses, d => d.Code == "XLSX010");
    }

    [Fact]
    public void LeAreeDiDatiEsterniSiScrivonoComeTabelleDiQueryESiRileggono()
    {
        var source = Workbook.CreateDefault();
        var refreshed = new DateTimeOffset(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);
        source.Sheets[0].QueryBindings.AddOrUpdate(new QueryBinding
        {
            Id = "q1",
            DisplayName = "Vendite",
            ConnectionId = "c1",
            Query = new PulseOffice.Data.QueryDefinition { Sql = "SELECT nome, totale FROM vendite", MaxRows = 500 },
            Anchor = A("B2"),
            Range = new CellRange(A("B2"), A("C9")),
            Policy = RefreshPolicy.Periodic,
            RefreshInterval = TimeSpan.FromMinutes(15),
            RowShift = RowShiftBehavior.Overwrite,
            PreserveCellFormatting = false,
            LastRowCount = 7,
            LastRefresh = refreshed,
        });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        using (var package = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, false))
        {
            var connection = Assert.Single(package.WorkbookPart!.ConnectionsPart!.Connections!.Elements<DocumentFormat.OpenXml.Spreadsheet.Connection>());
            Assert.Equal("SELECT nome, totale FROM vendite", connection.DatabaseProperties!.Command!.Value);
            Assert.Equal(15u, connection.Interval!.Value);
            var table = Assert.Single(package.WorkbookPart.WorksheetParts.Single().QueryTableParts).QueryTable!;
            Assert.Equal(DocumentFormat.OpenXml.Spreadsheet.GrowShrinkValues.OverwriteClear, table.GrowShrinkType!.Value);
            var name = Assert.Single(package.WorkbookPart.Workbook!.DefinedNames!.Elements<DocumentFormat.OpenXml.Spreadsheet.DefinedName>());
            Assert.Equal(table.Name!.Value, name.Name!.Value);
            Assert.Equal("'Sheet1'!$B$2:$C$9", name.Text);
            var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365).Validate(package, TestContext.Current.CancellationToken)
                .Where(error => error.Part?.Uri.OriginalString is { } uri && (uri.Contains("connections", StringComparison.Ordinal) || uri.Contains("queryTable", StringComparison.Ordinal) || uri.EndsWith("workbook.xml", StringComparison.Ordinal))).Select(error => error.Description).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;
        var binding = Assert.Single(read.Sheets[0].QueryBindings.All);
        Assert.Empty(read.DefinedNames);
        Assert.Equal("q1", binding.Id);
        Assert.Equal("Vendite", binding.DisplayName);
        Assert.Equal("c1", binding.ConnectionId);
        Assert.Equal("SELECT nome, totale FROM vendite", binding.Query.Sql);
        Assert.Equal(500, binding.Query.MaxRows);
        Assert.Equal(A("B2"), binding.Anchor);
        Assert.Equal(new CellRange(A("B2"), A("C9")), binding.Range);
        Assert.Equal(RefreshPolicy.Periodic, binding.Policy);
        Assert.Equal(TimeSpan.FromMinutes(15), binding.RefreshInterval);
        Assert.Equal(RowShiftBehavior.Overwrite, binding.RowShift);
        Assert.False(binding.PreserveCellFormatting);
        Assert.Equal(7, binding.LastRowCount);
        Assert.Equal(refreshed, binding.LastRefresh);
    }
}

public class XlsxStyleTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void IlCarattereSopravviveAlGiroCompleto()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new Styling.CellStyle
        {
            Font = new Styling.CellFont
            {
                Family = "Arial",
                Size = 14,
                Bold = true,
                Italic = true,
                ColorHex = "#FF0000",
            },
        });

        source.Sheets[0].SetValue(A("A1"), CellValue.Text("Titolo"));
        source.Sheets[0].SetStyle(A("A1"), style);

        var result = RoundTrip(source);
        var font = result.Styles.Get(result.Sheets[0][A("A1")].StyleId).Font;

        Assert.Equal("Arial", font.Family);
        Assert.Equal(14, font.Size);
        Assert.True(font.Bold);
        Assert.True(font.Italic);
        Assert.Equal("#FF0000", font.ColorHex);
    }

    [Fact]
    public void LoSfondoSopravviveAlGiroCompleto()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new Styling.CellStyle { BackgroundHex = "#FFFF00" });

        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1));
        source.Sheets[0].SetStyle(A("A1"), style);

        var result = RoundTrip(source);
        Assert.Equal("#FFFF00", result.Styles.Get(result.Sheets[0][A("A1")].StyleId).BackgroundHex);
    }

    [Fact]
    public void LAllineamentoSopravviveAlGiroCompleto()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new Styling.CellStyle
        {
            Horizontal = Styling.CellHorizontalAlignment.Center,
            WrapText = true,
        });

        source.Sheets[0].SetValue(A("A1"), CellValue.Text("Centrato"));
        source.Sheets[0].SetStyle(A("A1"), style);

        var result = RoundTrip(source);
        var read = result.Styles.Get(result.Sheets[0][A("A1")].StyleId);

        Assert.Equal(Styling.CellHorizontalAlignment.Center, read.Horizontal);
        Assert.True(read.WrapText);
    }

    [Fact]
    public void CelleConLoStessoAspettoCondividonoUnSoloStile()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new Styling.CellStyle
        {
            Font = new Styling.CellFont { Bold = true },
        });

        for (var i = 0; i < 100; i++)
        {
            source.Sheets[0].SetValue(new CellAddress(i, 0), CellValue.Number(i));
            source.Sheets[0].SetStyle(new CellAddress(i, 0), style);
        }

        var distinct = Enumerable.Range(0, 100)
            .Select(row => source.Sheets[0][new CellAddress(row, 0)].StyleId)
            .Distinct();

        Assert.Single(distinct);

        var result = RoundTrip(source);
        var first = result.Sheets[0][A("A1")].StyleId;

        for (var i = 1; i < 100; i++)
            Assert.Equal(first, result.Sheets[0][new CellAddress(i, 0)].StyleId);
    }

    [Fact]
    public void UnaCellaVuotaMaFormattataNonVienePersa()
    {
        var source = Workbook.CreateDefault();
        var style = source.Styles.Intern(new Styling.CellStyle { BackgroundHex = "#00FF00" });

        source.Sheets[0].SetStyle(A("B2"), style);
        Assert.Equal(1, source.Sheets[0].CellCount);

        var result = RoundTrip(source);
        Assert.Equal("#00FF00", result.Styles.Get(result.Sheets[0][A("B2")].StyleId).BackgroundHex);
    }

    [Fact]
    public void IFormatiNumericiDellUtenteVengonoDichiaratiNelFile()
    {
        var source = Workbook.CreateDefault();
        var formatId = source.NumberFormats.Register("0.000 \"kg\"");

        var style = source.Styles.Intern(new Styling.CellStyle { NumberFormatId = formatId });
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1.5));
        source.Sheets[0].SetStyle(A("A1"), style);

        var result = RoundTrip(source);
        var read = result.Styles.Get(result.Sheets[0][A("A1")].StyleId);

        Assert.Equal(formatId, read.NumberFormatId);
    }
}
