using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Fonts;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Typography.Tests;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class EmbeddedFontRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private DocumentFonts Fonts(bool embedSystem = false) => new(embedSystem,
    [
        new EmbeddedFontFile("Prova Sans", 400, false, "a.ttf", new SyntheticFont().Build()),
        new EmbeddedFontFile("Prova Sans", 700, true, "b.ttf", new SyntheticFont { Weight = 700, Italic = true }.Build()),
    ]);

    [Fact]
    public void UnFoglioPortaICaratteriIncorporatiNelPacchetto()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        workbook.Fonts = Fonts();

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            var manifest = XDocument.Load(archive.GetEntry("META-INF/manifest.xml")!.Open()).ToString();
            Assert.Contains("Fonts/ProvaSans-Regular.ttf", manifest, StringComparison.Ordinal);
            Assert.Contains("application/x-font-ttf", manifest, StringComparison.Ordinal);
            var content = XDocument.Load(archive.GetEntry("content.xml")!.Open()).ToString();
            Assert.Contains("font-face-uri", content, StringComparison.Ordinal);
            Assert.NotNull(archive.GetEntry("settings.xml"));
        }

        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;
        Assert.Equal(["Prova Sans"], read.Fonts.Families);
        Assert.Equal(2, read.Fonts.Files.Count);
        var bold = read.Fonts.Files.Single(file => file.IsBold);
        Assert.True(bold.Italic);
        Assert.Equal(Fonts().Files[1].Data, bold.Data);
        Assert.False(read.Fonts.EmbedSystemFonts);
    }

    [Fact]
    public void LaPreferenzaDiIncorporareISistemaSopravvive()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        workbook.Fonts = DocumentFonts.Empty with { EmbedSystemFonts = true };

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        Assert.True(new OdsReader().Read(stream).Value!.Fonts.EmbedSystemFonts);
    }

    [Fact]
    public void UnaPresentazioneDichiaraICaratteriEPortaIFile()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default, Fonts = Fonts(embedSystem: true) };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide);
        deck.AddExisting(slide);

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            var content = XDocument.Load(archive.GetEntry("content.xml")!.Open());
            var decls = content.Root!.Element(OdfPackage.Namespaces.Office + "font-face-decls");
            Assert.NotNull(decls);
            Assert.Contains(decls!.Elements(), face => (string?)face.Attribute(OdfPackage.Namespaces.Style + "name") == _themes.Default.Fonts.Major);
            Assert.Equal("automatic-styles", ((XElement)decls.NextNode!).Name.LocalName);
        }

        stream.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(stream).Value!;
        Assert.Equal(2, read.Fonts.Files.Count);
        Assert.True(read.Fonts.EmbedSystemFonts);
    }

    [Fact]
    public void IlCaratterePropriaDiUnaPorzioneNonSiPerde()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide);
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(title with { Text = new RichText(PlaceholderRole.Title, [new TextParagraph("Titolo") { Runs = [new TextRun("Titolo") { Font = "Prova Sans" }] }]) });
        deck.AddExisting(slide);

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(stream).Value!;

        var run = read.Slides[0].Elements.OfType<TextElement>().SelectMany(element => element.Text.Paragraphs).SelectMany(paragraph => paragraph.EffectiveRuns())
            .First(candidate => candidate.Text == "Titolo");
        Assert.Equal("Prova Sans", run.Font);
    }
}
