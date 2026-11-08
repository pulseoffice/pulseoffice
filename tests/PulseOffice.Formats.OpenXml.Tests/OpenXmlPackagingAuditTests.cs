using System.IO.Compression;
using System.Text;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlPackagingAuditTests
{
    private static byte[] Workbook(params string[] extraEntries)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
            foreach (var name in extraEntries)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(new byte[64]);
            }

        return stream.ToArray();
    }

    private static void DeclareSize(byte[] package, string name, uint size)
    {
        var encoded = Encoding.ASCII.GetBytes(name);
        for (var offset = 0; offset + 46 < package.Length; offset++)
        {
            if (BitConverter.ToUInt32(package, offset) != 0x02014b50) continue;
            var nameLength = BitConverter.ToUInt16(package, offset + 28);
            if (nameLength != encoded.Length || !package.AsSpan(offset + 46, nameLength).SequenceEqual(encoded)) continue;
            BitConverter.GetBytes(size).CopyTo(package, offset + 24);
        }
    }

    [Fact]
    public void A_package_whose_parts_expand_beyond_the_budget_is_refused_before_decompression()
    {
        var package = Workbook("junk/a.bin", "junk/b.bin");
        DeclareSize(package, "junk/a.bin", 0xA0000000);
        DeclareSize(package, "junk/b.bin", 0xA0000000);

        var result = new XlsxReader().Read(new MemoryStream(package));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void A_package_within_the_budget_still_opens()
    {
        var package = Workbook("junk/a.bin");

        var result = new XlsxReader().Read(new MemoryStream(package));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void A_part_whose_override_differs_only_in_case_gets_its_content_type_repaired()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), PulseOffice.Sheets.Values.CellValue.Text("testo"));
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var shared = archive.Entries.Single(entry => entry.FullName.EndsWith("sharedStrings.xml", StringComparison.Ordinal));
            var lowered = shared.FullName.Replace("sharedStrings.xml", "sharedstrings.xml", StringComparison.Ordinal);
            byte[] content;
            using (var source = shared.Open()) { using var copy = new MemoryStream(); source.CopyTo(copy); content = copy.ToArray(); }
            shared.Delete();
            using (var target = archive.CreateEntry(lowered).Open()) target.Write(content);

            var rels = archive.GetEntry("xl/_rels/workbook.xml.rels")!;
            string text;
            using (var reader = new StreamReader(rels.Open())) text = reader.ReadToEnd();
            rels.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("xl/_rels/workbook.xml.rels").Open());
            writer.Write(text.Replace("sharedStrings.xml", "sharedstrings.xml", StringComparison.Ordinal));
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded);
        Assert.Equal("testo", result.Value!.Sheets[0].GetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0)).AsText());
    }
}
