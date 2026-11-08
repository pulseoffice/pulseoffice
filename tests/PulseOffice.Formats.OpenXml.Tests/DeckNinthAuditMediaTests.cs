using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckNinthAuditMediaTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(params MediaElement[] media)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        foreach (var element in media) slide.Add(element);
        deck.AddExisting(slide);
        return deck;
    }

    private MediaElement Video(string id) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
        Kind = MediaKind.Video,
        Data = [0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32],
        MimeType = "video/mp4",
    };

    private Presentation ThroughPptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new PptxReader(_themes, _masters).Read(buffer).Value!;
    }

    private Presentation ThroughOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return new OdpReader(_themes, _masters).Read(buffer).Value!;
    }

    private MediaElement Only(Presentation deck) => Assert.Single(deck.Slides[0].Elements.OfType<MediaElement>());

    [Fact]
    public void Ripetizione_volume_e_muto_fanno_il_giro_in_PPTX_e_ODP()
    {
        var deck = Deck(Video("v1") with { Loop = true, Volume = 0.5, Muted = true }, Video("v2") with { Volume = 0.25 });

        foreach (var again in new[] { ThroughPptx(deck), ThroughOdp(deck), ThroughPptx(ThroughOdp(deck)), ThroughOdp(ThroughPptx(deck)) })
        {
            var media = again.Slides[0].Elements.OfType<MediaElement>().OrderBy(element => element.Bounds.Left.Value).ThenBy(element => element.Volume).ToList();
            Assert.Equal(2, media.Count);
            var looped = media.Single(element => element.Loop);
            Assert.True(looped.Muted);
            Assert.InRange(looped.Volume, 0.49, 0.51);
            var quiet = media.Single(element => !element.Loop);
            Assert.False(quiet.Muted);
            Assert.InRange(quiet.Volume, 0.24, 0.26);
        }
    }

    [Fact]
    public void Un_media_senza_impostazioni_resta_a_volume_pieno_e_non_scrive_nodi_di_tempo()
    {
        var deck = Deck(Video("v1"));
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        using (var document = PresentationDocument.Open(buffer, false))
            Assert.Null(document.PresentationPart!.SlideParts.First().Slide!.GetFirstChild<P.Timing>());

        var again = Only(ThroughPptx(deck));
        Assert.False(again.Loop);
        Assert.Equal(1, again.Volume);
        Assert.Equal(1, Only(ThroughOdp(deck)).Volume);
    }

    [Theory]
    [InlineData("filmati/clip.mp4")]
    [InlineData("https://example.org/clip.mp4")]
    public void I_media_collegati_restano_collegati_in_PPTX_e_ODP(string link)
    {
        var deck = Deck(Video("v1") with { Data = [], LinkedPath = link, Loop = true });

        using (var buffer = new MemoryStream())
        {
            Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
            buffer.Position = 0;
            using var document = PresentationDocument.Open(buffer, false);
            var slide = document.PresentationPart!.SlideParts.First();
            Assert.Empty(slide.DataPartReferenceRelationships);
            Assert.Equal(2, slide.ExternalRelationships.Count());
        }

        using (var buffer = new MemoryStream())
        {
            Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
            buffer.Position = 0;
            using var archive = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("Media/", StringComparison.Ordinal));
        }

        foreach (var again in new[] { ThroughPptx(deck), ThroughOdp(deck), ThroughOdp(ThroughPptx(deck)), ThroughPptx(ThroughOdp(deck)) })
        {
            var media = Only(again);
            Assert.Equal(link, media.LinkedPath);
            Assert.Empty(media.Data);
            Assert.Equal(MediaKind.Video, media.Kind);
            Assert.True(media.Loop);
        }
    }

    [Fact]
    public void L_anteprima_trasparente_di_riserva_e_un_PNG_valido_e_si_rilegge_come_nessuna_anteprima()
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(Video("v1")), buffer).Succeeded);
        buffer.Position = 0;
        using (var document = PresentationDocument.Open(buffer, false))
        {
            var image = Assert.Single(document.PresentationPart!.SlideParts.First().ImageParts);
            using var stream = image.GetStream();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            var png = copy.ToArray();
            var length = (png[33] << 24) | (png[34] << 16) | (png[35] << 8) | png[36];
            Assert.Equal("IDAT", System.Text.Encoding.ASCII.GetString(png, 37, 4));
            using var inflater = new System.IO.Compression.ZLibStream(new MemoryStream(png, 41, length), System.IO.Compression.CompressionMode.Decompress);
            using var pixels = new MemoryStream();
            inflater.CopyTo(pixels);
            Assert.Equal(5, pixels.Length);
        }
        buffer.Position = 0;
        Assert.Null(Only(new PptxReader(_themes, _masters).Read(buffer).Value!).PosterData);
    }
}
