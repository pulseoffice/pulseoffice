using System.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsVersionRoundTripTests
{
    private static Workbook WithVersions()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        CellAddressOf("A1", out var address);
        sheet.SetValue(address, CellValue.Number(42));

        workbook.Versions.Add(Encoding.UTF8.GetBytes("prima stesura"), "ada", "Bozza di partenza");
        workbook.Versions.Add(Encoding.UTF8.GetBytes("seconda stesura"), "rossi", "Rivista");

        return workbook;
    }

    private static void CellAddressOf(string text, out Sheets.Addressing.CellAddress address) =>
        Assert.True(Sheets.Addressing.CellAddress.TryParseA1(text, out address));

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
    public void LeVersioniSopravvivonoAlGiro()
    {
        var read = RoundTrip(WithVersions());

        Assert.Equal(2, read.Versions.Count);
        Assert.Equal("Version1", read.Versions.All[0].Name);
        Assert.Equal("Bozza di partenza", read.Versions.All[0].Comment);
        Assert.Equal("rossi", read.Versions.All[1].Author);
    }

    [Fact]
    public void IlContenutoDiUnaVersioneTornaByteEsatti()
    {
        var read = RoundTrip(WithVersions());

        Assert.Equal("prima stesura", Encoding.UTF8.GetString(read.Versions.All[0].Content));
    }

    [Fact]
    public void OgniVoceDelleVersioniEDichiarataNelManifest()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(WithVersions(), stream);

        stream.Position = 0;
        using var archive = new System.IO.Compression.ZipArchive(stream);

        var manifest = new StreamReader(
            archive.GetEntry("META-INF/manifest.xml")!.Open()).ReadToEnd();

        foreach (var entry in archive.Entries.Select(item => item.FullName))
            if (entry.StartsWith("Versions/", StringComparison.Ordinal))
                Assert.Contains($"manifest:full-path=\"{entry}\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void UnDocumentoSenzaVersioniNonScriveNulla()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");

        Assert.Equal(0, RoundTrip(workbook).Versions.Count);
    }
}
