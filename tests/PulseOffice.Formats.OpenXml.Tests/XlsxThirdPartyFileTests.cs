using System.IO.Compression;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxThirdPartyFileTests
{
    private const string AgileKey = "ooxml:SHA-512:1000:AAECAwQFBgcICQoLDA0ODw==:XP+meoEiGclvTC8aehpzzpvcoTw951qpsRqLTRr1K1UVwazUQjux08kWRBH0Rr+odjBG2LY+NWbFuLZ7vvdaLg==";

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Read(Stream stream)
    {
        var result = new XlsxSpreadsheetReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook Fixture(string name)
    {
        var copy = new MemoryStream(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Excel", name)));
        return Read(copy);
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        var stream = new MemoryStream();
        Assert.True(new XlsxSpreadsheetWriter().Write(workbook, stream).Succeeded);
        stream.Position = 0;
        return Read(stream);
    }

    [Theory]
    [InlineData("tdf100709.xlsx")]
    [InlineData("tdf124525.xlsx")]
    [InlineData("tdf76115.xlsx")]
    [InlineData("empty_ext_defined_name.xlsx")]
    [InlineData("xf_default_values.xlsx")]
    public void IPacchettiImperfettiCheCalcApreSiApronoAnche(string file)
    {
        var workbook = Fixture(file);

        Assert.NotEmpty(workbook.Sheets);
        Assert.Contains(workbook.Sheets, sheet => sheet.CellCount > 0);
    }

    [Fact]
    public void NomiConBarraRovesciaETipiInMinuscoloSiRiparano()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").SetValue(A("B2"), CellValue.Number(42));
        var original = new MemoryStream();
        new XlsxSpreadsheetWriter().Write(workbook, original);

        var broken = new MemoryStream();
        original.Position = 0;
        using (var source = new ZipArchive(original, ZipArchiveMode.Read, leaveOpen: true))
        using (var target = new ZipArchive(broken, ZipArchiveMode.Create, leaveOpen: true))
        {
            target.CreateEntry("xl\\");
            foreach (var entry in source.Entries)
            {
                var name = entry.FullName == "[Content_Types].xml" ? "[content_types].xml" : entry.FullName.Replace('/', '\\');
                using var input = entry.Open();
                using var output = target.CreateEntry(name).Open();
                input.CopyTo(output);
            }
        }

        broken.Position = 0;
        Assert.Equal(CellValue.Number(42), Read(broken).Sheets[0].GetValue(A("B2")));
    }

    [Fact]
    public void LaPasswordStoricaDiExcelSiVerificaESiConserva()
    {
        var workbook = Fixture("ProtecteSheet1234Pass.xlsx");
        var protection = workbook.Sheets[0].Protection;

        Assert.True(protection.IsEnabled);
        Assert.True(protection.Accepts("1234"));
        Assert.False(protection.Accepts("4321"));
        Assert.True(RoundTrip(workbook).Sheets[0].Protection.Accepts("1234"));
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX028");
    }

    [Fact]
    public void LaPasswordModernaDiExcelSiVerificaESiConserva()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Foglio1");
        sheet.Protection = sheet.Protection with { IsEnabled = true, PasswordHash = AgileKey };
        workbook.Protection = workbook.Protection with { Structure = true, PasswordHash = AgileKey };

        var reread = RoundTrip(workbook);

        Assert.Equal(AgileKey, reread.Sheets[0].Protection.PasswordHash);
        Assert.True(reread.Sheets[0].Protection.Accepts("Segreto"));
        Assert.False(reread.Sheets[0].Protection.Accepts("segreto"));
        Assert.True(reread.Protection.Accepts("Segreto"));
    }
}

public class XlsxControlCharacterTests
{
    [Fact]
    public void ICaratteriDiControlloSiConservanoConLEscapeDiExcel()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("A");
        var address = new CellAddress(0, 0);
        sheet.SetValue(address, CellValue.Text("x\u0016y_x0041_z"));

        var stream = new MemoryStream();
        Assert.True(new XlsxSpreadsheetWriter().Write(workbook, stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(CellValue.Text("x\u0016y_x0041_z"), new XlsxSpreadsheetReader().Read(stream).Value!.Sheets[0].GetValue(address));
    }
}

public class XlsxFunctionPrefixTests
{
    [Fact]
    public void IPrefissiDiExcelSiTolgonoInLetturaESiRimettonoInScrittura()
    {
        var syntax = new XlsxFormulaSyntax();

        Assert.Equal("_xlfn._xlws.FILTER(A1:A3,A1:A3>1)", syntax.ToOpenXml("=FILTER(A1:A3;A1:A3>1)"));
        Assert.Equal("=FILTER(A1:A3;A1:A3>1)", syntax.ToInternal("_xlfn._xlws.FILTER(A1:A3,A1:A3>1)"));
        Assert.Equal("=EASTERSUNDAY(2024)", syntax.ToInternal("_xlfn.ORG.OPENOFFICE.EASTERSUNDAY(2024)"));
    }
}
