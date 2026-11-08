using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxPictureBulletTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Picture_bullets_cross_the_docx()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var picture = new ListBulletPicture(Png, "image/png", 12, 10);
        document.Styles.List.Replace(new ListStyle("Pictured", [new ListLevel(NumberFormat.Bullet) { Bullet = "o", Picture = picture }, new ListLevel(NumberFormat.Arabic)]));
        document.Body.Replace([new ParagraphBlock("p1", "Item")]);

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX173");
        var level = read.Value!.Styles.List.All.Single(list => list.Name == "Pictured").Levels[0];
        Assert.Equal(NumberFormat.Bullet, level.Format);
        Assert.Equal("o", level.Bullet);
        Assert.Equal(Png, level.Picture!.Data.ToArray());
        Assert.Equal("image/png", level.Picture.MediaType);
        Assert.Equal(12, level.Picture.Width, 1);
        Assert.Equal(10, level.Picture.Height, 1);
    }
}
