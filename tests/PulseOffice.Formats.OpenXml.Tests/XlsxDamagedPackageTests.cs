using System.IO.Compression;
using System.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxDamagedPackageTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void UnPacchettoConXmlRottoSiApreConCioCheSiSalva()
    {
        using var stream = new MemoryStream(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Excel", "forcepoint107.xlsx")));

        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.NotEmpty(result.Value!.Sheets);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code is "XLSX049" or "XLSX050");
    }

    [Fact]
    public void UnaRigaRottaSiPerdeMaLeAltreRestano()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.SetValue(A("A2"), CellValue.Number(2));
        sheet.SetValue(A("A3"), CellValue.Number(3));
        using var buffer = new MemoryStream();
        new XlsxWriter().Write(workbook, buffer);

        using var damaged = Damage(buffer, xml => xml.Replace("r=\"A2\"", "r=\"A2 s=", StringComparison.Ordinal));

        var result = new XlsxReader().Read(damaged);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var back = result.Value!.Sheets[0];
        Assert.Equal(CellValue.Number(1), back.GetValue(A("A1")));
        Assert.Equal(CellValue.Number(3), back.GetValue(A("A3")));
        Assert.Equal("XLSX049", Assert.Single(result.Diagnostics, d => d.Code.StartsWith("XLSX04", StringComparison.Ordinal) || d.Code == "XLSX050").Code);
    }

    private static MemoryStream Damage(MemoryStream source, Func<string, string> change)
    {
        source.Position = 0;
        var output = new MemoryStream();
        using (var input = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var reader = entry.Open();
                using var copy = new MemoryStream();
                reader.CopyTo(copy);
                var bytes = entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes(change(Encoding.UTF8.GetString(copy.ToArray()))) : copy.ToArray();
                using var written = target.CreateEntry(entry.FullName).Open();
                written.Write(bytes);
            }
        }

        output.Position = 0;
        return output;
    }
}
