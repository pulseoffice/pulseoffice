using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round152TrackedChangeTextTests
{
    private const string Before = "first\nsecond  line\tend";
    private const string Comment = "why\nbecause  so";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    [Fact]
    public void Previous_cell_text_and_comment_of_a_tracked_change_are_written_as_odf_paragraph_markup_and_read_back()
    {
        Assert.True(CellAddress.TryParseA1("B2", out var b2));
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(b2, CellValue.Text("now"));
        source.Changes.Import([new TrackedChange
        {
            Id = "ct1",
            SheetIndex = 0,
            Address = b2,
            Before = CellValue.Text(Before),
            After = CellValue.Text("now"),
            Author = "Ada",
            Comment = Comment,
            At = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
        }]);

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var content = archive.GetEntry("content.xml")!.Open();
            var changes = XDocument.Load(content).Descendants(Table + "tracked-changes").Single();
            Assert.Equal(4, changes.Descendants(Text + "p").Count());
            Assert.Single(changes.Descendants(Text + "tab"));
            Assert.Equal(2, changes.Descendants(Text + "s").Count());
            Assert.DoesNotContain(changes.DescendantNodes().OfType<XText>(), text => text.Value.Contains('\n') || text.Value.Contains("  "));
        }

        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var change = Assert.Single(result.Value!.Changes.Changes);
        Assert.Equal(Before, change.Before.ToString());
        Assert.Equal(Comment, change.Comment);
    }
}
