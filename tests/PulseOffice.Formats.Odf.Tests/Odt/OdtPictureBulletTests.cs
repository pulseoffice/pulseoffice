using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtPictureBulletTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    [Fact]
    public void Picture_bullets_cross_the_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var picture = new ListBulletPicture(Png, "image/png", 12, 10);
        document.Styles.List.Replace(new ListStyle("Pictured", [new ListLevel(NumberFormat.Bullet) { Bullet = "o", Picture = picture }, new ListLevel(NumberFormat.Arabic)]));
        document.Body.Replace([new ParagraphBlock("p1", "Item")]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream);

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF178");
        var level = read.Value!.Styles.List.All.Single(list => list.Name == "Pictured").Levels[0];
        Assert.Equal(NumberFormat.Bullet, level.Format);
        Assert.Equal("o", level.Bullet);
        Assert.Equal(Png, level.Picture!.Data.ToArray());
        Assert.Equal(12, level.Picture.Width, 1);
        Assert.Equal(10, level.Picture.Height, 1);
    }

    [Fact]
    public void List_styles_whose_names_differ_only_in_punctuation_keep_their_own_pictures()
    {
        var other = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.List.Replace(new ListStyle("Liste ü", [new ListLevel(NumberFormat.Bullet) { Bullet = "o", Picture = new ListBulletPicture(Png, "image/png", 12, 10) }]));
        document.Styles.List.Replace(new ListStyle("Liste ö", [new ListLevel(NumberFormat.Bullet) { Bullet = "o", Picture = new ListBulletPicture(other, "image/png", 12, 10) }]));
        document.Body.Replace([new ParagraphBlock("p1", "Item")]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var styles = new OdtReader().Read(stream).Value!.Styles.List;

        Assert.Equal(Png, styles.All.Single(list => list.Name == "Liste ü").Levels[0].Picture!.Data.ToArray());
        Assert.Equal(other, styles.All.Single(list => list.Name == "Liste ö").Levels[0].Picture!.Data.ToArray());
    }
}
