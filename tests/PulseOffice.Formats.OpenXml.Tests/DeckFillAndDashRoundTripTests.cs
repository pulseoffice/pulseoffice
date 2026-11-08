using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckFillAndDashRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private readonly byte[] _png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 0x1F, 0x15, 0xC4, 0x89,
        0, 0, 0, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0xF0, 0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99, 0x3D, 0x1D,
        0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    private Presentation Deck(bool tiled) =>
        DeckWith(ShapeStyle.Default with { Fill = ShapeFill.Default with { Kind = ShapeFillKind.Bitmap, Opacity = 0.5, Bitmap = new ShapeBitmap { Data = _png, Tiled = tiled } } });

    private Presentation DeckWith(ShapeStyle style)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "figura",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
            Content = new ShapeObject
            {
                Id = "figura",
                Shape = ShapeKind.Ellipse,
                Style = style,
            },
        });
        deck.AddExisting(slide);
        return deck;
    }

    private ShapeFill Through(IPresentationWriter writer, IPresentationReader reader, Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(deck, stream).Succeeded);
        Assert.DoesNotContain(writer.CheckFidelity(deck).Losses, loss => loss.Message.Contains("fill", StringComparison.OrdinalIgnoreCase));
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded);
        var shape = read.Value!.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single();
        return shape.Style.Fill;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Il_riempimento_a_bitmap_di_una_forma_di_Deck_attraversa_ODP_e_PPTX(bool tiled)
    {
        foreach (var fill in new[]
                 {
                     Through(new OdpWriter(), new OdpReader(_themes, _masters), Deck(tiled)),
                     Through(new PptxWriter(), new PptxReader(_themes, _masters), Deck(tiled)),
                 })
        {
            Assert.Equal(ShapeFillKind.Bitmap, fill.Kind);
            Assert.Equal(_png, fill.Bitmap!.Data);
            Assert.Equal(tiled, fill.Bitmap.Tiled);
            Assert.Equal(0.5, fill.Opacity, 2);
        }
    }

    [Fact]
    public void Lo_sfondo_a_immagine_dello_schema_attraversa_ODP_e_PPTX()
    {
        foreach (var (writer, reader) in new (IPresentationWriter, IPresentationReader)[]
                 {
                     (new OdpWriter(), new OdpReader(_themes, _masters)),
                     (new PptxWriter(), new PptxReader(_themes, _masters)),
                 })
        {
            var deck = Deck(tiled: false);
            deck.Master = deck.Master with
            {
                BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "sfondo", Data = _png, MediaType = "image/png" },
            };
            using var stream = new MemoryStream();
            Assert.True(writer.Write(deck, stream).Succeeded);
            stream.Position = 0;
            var read = reader.Read(stream).Value!;
            Assert.Equal(_png, read.Master.BackgroundImage?.Data.ToArray());
            Assert.Null(read.Slides[0].BackgroundImage);
        }
    }

    [Fact]
    public void Un_tratteggio_personalizzato_attraversa_ODP_esatto_e_PPTX_in_larghezze_di_linea()
    {
        var pattern = new ShapeDashPattern { Dots1 = 3, Dots1Length = 1.97, Dots2 = 3, Dots2Length = 1, Distance = 1, Relative = true };
        var deck = DeckWith(ShapeStyle.Default with { Line = ShapeLine.Default with { Dash = pattern.NearestPreset(), CustomDash = pattern } });

        foreach (var (writer, reader) in new (IPresentationWriter, IPresentationReader)[]
                 {
                     (new OdpWriter(), new OdpReader(_themes, _masters)),
                     (new PptxWriter(), new PptxReader(_themes, _masters)),
                 })
        {
            using var stream = new MemoryStream();
            Assert.True(writer.Write(deck, stream).Succeeded);
            stream.Position = 0;
            var line = reader.Read(stream).Value!.Slides[0].Elements.OfType<ObjectElement>().Select(item => item.Content).OfType<ShapeObject>().Single().Style.Line;
            Assert.Equal(pattern, line.CustomDash);
            Assert.Equal(ShapeStrokeStyle.DashDotDot, line.Dash);
        }
    }

    [Fact]
    public void Il_tratteggio_si_disegna_con_le_misure_del_documento()
    {
        Assert.Equal([1.97, 1, 1.97, 1, 1, 1], new ShapeDashPattern { Dots1 = 2, Dots1Length = 1.97, Dots2 = 1, Dots2Length = 0, Distance = 1 }.InLineWidths(20));
        Assert.Equal([2, 1], new ShapeDashPattern { Dots1Length = 40, Distance = 20, Relative = false }.InLineWidths(20));
    }

    [Fact]
    public void Lo_sfondo_traslucido_e_l_immagine_affiancata_o_non_ripetuta_attraversano_i_formati()
    {
        var deck = Deck(tiled: false);
        deck.Slides[0].Background = 0x80336699u;
        var second = _slides.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        second.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "tessere", Data = _png, MediaType = "image/png" };
        second.BackgroundLayout = new BackgroundImageLayout(BackgroundImageMode.Tile, Twip.FromCentimeters(4), Twip.FromCentimeters(2));
        deck.AddExisting(second);
        var third = _slides.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        third.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "unica", Data = _png, MediaType = "image/png" };
        third.BackgroundLayout = new BackgroundImageLayout(BackgroundImageMode.Natural, Twip.Zero, Twip.Zero);
        deck.AddExisting(third);

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, odp).Succeeded);
        odp.Position = 0;
        var fromOdp = new OdpReader(_themes, _masters).Read(odp).Value!;
        Assert.Equal(0x80u, fromOdp.Slides[0].Background!.Value >> 24);
        Assert.Equal(deck.Slides[1].BackgroundLayout, fromOdp.Slides[1].BackgroundLayout);
        Assert.Equal(BackgroundImageMode.Natural, fromOdp.Slides[2].BackgroundLayout.Mode);

        var pptx = new PptxWriter();
        Assert.Contains(pptx.CheckFidelity(deck).Losses, loss => loss.Code == "PPTX036");
        using var stream = new MemoryStream();
        Assert.True(pptx.Write(deck, stream).Succeeded);
        stream.Position = 0;
        var fromPptx = new PptxReader(_themes, _masters).Read(stream).Value!;
        Assert.Equal(0x80u, fromPptx.Slides[0].Background!.Value >> 24);
        Assert.Equal(BackgroundImageMode.Tile, fromPptx.Slides[1].BackgroundLayout.Mode);
    }
}
