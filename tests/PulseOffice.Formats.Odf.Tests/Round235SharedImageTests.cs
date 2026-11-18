using System.IO.Compression;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round235SharedImageTests
{
    [Fact]
    public void The_same_image_inserted_twice_is_stored_once()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        foreach (var id in new[] { "a", "b" })
            document.Objects.Add(new AnchoredObject(id, new ParagraphAnchor("p1"), new EmbeddedContent(new RasterImageObject { Id = id, Data = new byte[] { 1, 2 }, MediaType = "image/png" }),
                new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20))));
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        var bytes = stream.ToArray();

        using (var zip = new ZipArchive(new MemoryStream(bytes)))
            Assert.Single(zip.Entries, entry => entry.FullName.StartsWith("Pictures/", StringComparison.Ordinal));
        var read = new OdtReader().Read(new MemoryStream(bytes)).Value!;
        Assert.Equal(2, read.Objects.All.Count);
        Assert.All(read.Objects.All, item => Assert.Equal(new byte[] { 1, 2 }, ((RasterImageObject)((EmbeddedContent)item.Content).Value).Data.ToArray()));
    }
}
