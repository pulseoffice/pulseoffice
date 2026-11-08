using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class PictureBulletLayoutTests
{
    private readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private GlyphRun Label(ListBulletPicture? picture)
    {
        var f = new LayoutFixture(width: 400, height: 400);
        f.Document.Styles.List.Add(new ListStyle("Pictured", [new ListLevel(NumberFormat.Bullet) { Bullet = "•", Indent = 0, Hanging = 30, Picture = picture }]));
        f.Add("item", p => p with { ListStyleName = "Pictured", BulletLevel = 0, Bullet = true });
        return f.Lines(f.Layout()).SelectMany(line => line.Line.Runs).Single(run => run.Kind == GlyphRunKind.Label);
    }

    [Fact]
    public void AFixedSizePictureBulletIsCarriedToTheLabelRunWithItsOwnSize()
    {
        var run = Label(new ListBulletPicture(_png, "image/png", 20, 10));

        Assert.NotNull(run.Picture);
        Assert.Equal(20, run.Picture!.Width, 3);
        Assert.Equal(10, run.Picture.Height, 3);
        Assert.Equal(20, run.Width, 3);
    }

    [Fact]
    public void AWordPictureBulletScalesWithTheFontKeepingItsProportion()
    {
        var run = Label(new ListBulletPicture(_png, "image/png", 9, 18) { ScalesWithFont = true });

        Assert.Equal(run.Font.Size, run.Picture!.Height, 3);
        Assert.Equal(run.Font.Size / 2, run.Picture.Width, 3);
    }

    [Fact]
    public void AnAbsurdPictureSizeIsBounded()
    {
        var run = Label(new ListBulletPicture(_png, "image/png", double.NaN, 1e9));

        Assert.InRange(run.Picture!.Width, 1, 144);
        Assert.InRange(run.Picture.Height, 1, 144);
    }

    [Fact]
    public void ABulletWithoutPictureStaysACharacter()
    {
        var run = Label(null);

        Assert.Null(run.Picture);
        Assert.Equal("•", run.Text);
    }

    [Fact]
    public void ChangingThePictureOfTheListStyleRelaysTheLabel()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        f.Document.Styles.List.Add(new ListStyle("Pictured", [new ListLevel(NumberFormat.Bullet) { Hanging = 30, Picture = new ListBulletPicture(_png, "image/png", 8, 8) }]));
        f.Add("item", p => p with { ListStyleName = "Pictured", BulletLevel = 0, Bullet = true });
        f.Layout();
        f.Document.Styles.List.Find("Pictured")!.Levels = [new ListLevel(NumberFormat.Bullet) { Hanging = 30, Picture = new ListBulletPicture(new byte[] { 9, 9 }, "image/png", 16, 16) }];

        var run = f.Lines(f.Layout()).SelectMany(line => line.Line.Runs).Single(item => item.Kind == GlyphRunKind.Label);

        Assert.Equal(16, run.Picture!.Width, 3);
    }
}
