using Avalonia;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class RichTextPictureBulletTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAAEUlEQVR4nGP4z8DwHwyBNAMAP+IF+0xnmpAAAAAASUVORK5CYII=");

    private sealed class PlainTheme : ITextTheme
    {
        public uint Resolve(ColorRole role) => 0xFF000000;

        public string MajorFont => "Inter";

        public string MinorFont => "Inter";
    }

    private ComposedTextFrame Compose(BulletSymbol symbol, double? indent = -18) =>
        new TextFrameComposer(new ListLabels()).Compose(
            new RichText(PlaceholderRole.Body, [new TextParagraph("voce") { Bullet = true, BulletSymbol = symbol, FontSize = 20, MarginLeft = 18, Indent = indent }]),
            new Rect(0, 0, 20000, 8000), new PlainTheme(), new TextFields(), 1.0);

    [Fact]
    public Task A_fixed_picture_replaces_the_hanging_label_at_its_size() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var line = Compose(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 16, 8) }).Lines[0];

        Assert.Null(line.Label);
        Assert.NotNull(line.Picture);
        Assert.Equal(16 * ParagraphTypesetter.PointToTwip, line.Picture!.Box.Width, 3);
        Assert.Equal(8 * ParagraphTypesetter.PointToTwip, line.Picture.Box.Height, 3);
        Assert.Equal(0, line.Picture.Box.X, 3);
    }, CancellationToken.None);

    [Fact]
    public Task A_font_relative_picture_follows_the_text_and_its_percentage() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var line = Compose(new BulletSymbol("•") { RelativeSize = 0.5, Picture = new ListBulletPicture(Png, "image/png", 24, 12) { ScalesWithFont = true } }).Lines[0];

        Assert.Equal(10 * ParagraphTypesetter.PointToTwip, line.Picture!.Box.Height, 3);
        Assert.Equal(20 * ParagraphTypesetter.PointToTwip, line.Picture.Box.Width, 3);
    }, CancellationToken.None);

    [Fact]
    public Task An_inline_label_keeps_room_for_the_picture() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var line = Compose(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 12, 12) }, indent: null).Lines[0];

        Assert.NotNull(line.Picture);
        Assert.True(line.Offset > 0);
        Assert.Equal(18 * ParagraphTypesetter.PointToTwip, line.Picture!.Box.X, 3);
    }, CancellationToken.None);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task The_picture_is_painted_and_an_unreadable_one_leaves_a_frame(bool readable) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var data = readable ? Png : [1, 2, 3];
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("voce") { Bullet = true, BulletSymbol = new BulletSymbol("•") { Picture = new ListBulletPicture(data, "image/png", 12, 12) }, MarginLeft = 18, Indent = -18 }]);
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(80, 40));
        using (var context = bitmap.CreateDrawingContext())
            new RichTextFramePainter(new TextFrameComposer(new ListLabels())).Paint(new RichTextScene(context, new Rect(0, 0, 80, 40), 0.1, new PlainTheme(), new TextFields()), text);
    }, CancellationToken.None);

    [Fact]
    public void An_empty_picture_decodes_to_nothing()
    {
        var bitmaps = new BulletPictureBitmaps();

        Assert.Null(bitmaps.For(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public Task A_picture_taller_than_the_line_grows_the_line_and_sits_on_the_baseline() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var plain = Compose(new BulletSymbol("•")).Lines[0];
        var line = Compose(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 120, 60) }).Lines[0];

        var picture = line.Picture!.Box;
        Assert.Equal(0, picture.Y, 3);
        Assert.True(line.Height >= picture.Bottom);
        Assert.True(line.Height > plain.Height);
        Assert.Equal(picture.Bottom, line.Segments[0].Y + line.Segments[0].Text.Baseline, 3);
    }, CancellationToken.None);

    [Fact]
    public Task A_huge_picture_is_bounded_and_a_picture_without_data_keeps_the_label() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var huge = Compose(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 1e9, double.NaN) }).Lines[0];
        Assert.Equal(144 * ParagraphTypesetter.PointToTwip, huge.Picture!.Box.Width, 3);

        var empty = Compose(new BulletSymbol("•") { Picture = new ListBulletPicture(Array.Empty<byte>(), "image/png", 12, 12) }).Lines[0];
        Assert.Null(empty.Picture);
        Assert.NotNull(empty.Label);
    }, CancellationToken.None);

    [Fact]
    public Task A_picture_that_fits_the_line_stays_centred_and_does_not_move_the_text() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var line = Compose(new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 4, 4) }).Lines[0];

        Assert.Equal(0, line.Segments[0].Y, 3);
        Assert.True(line.Picture!.Box.Y > 0);
    }, CancellationToken.None);
}
