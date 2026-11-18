using System.IO.Compression;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round221OdsCommentLinkTests
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    [Fact]
    public void A_link_inside_a_cell_comment_does_not_become_the_cell_hyperlink()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Text("Value"));
        sheet.Comments[A("A1")] = new CellComment { Text = "notelink" };
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            content = content.Replace(
                ">notelink<",
                "><text:a xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xlink:type=\"simple\" xlink:href=\"https://example.test/\">notelink</text:a><",
                StringComparison.Ordinal);
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }
        stream.Position = 0;

        var read = new OdsReader().Read(stream).Value!.Sheets[0];

        Assert.Equal("notelink", read.Comments[A("A1")].Text);
        Assert.Null(read[A("A1")].Hyperlink);
    }
}
