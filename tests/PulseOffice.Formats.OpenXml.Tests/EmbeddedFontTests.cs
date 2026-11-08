using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Fonts;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Typography.Tests;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class EmbeddedFontTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private DocumentFonts Fonts() => new(false,
    [
        new EmbeddedFontFile("Prova Sans", 400, false, "a.ttf", new SyntheticFont().Build()),
        new EmbeddedFontFile("Prova Sans", 700, false, "b.ttf", new SyntheticFont { Weight = 700 }.Build()),
    ]);

    private Presentation Deck()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default, Fonts = Fonts() };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));
        return deck;
    }

    [Fact]
    public void PptxScriveLElencoDeiCaratteriConPartiFntdata()
    {
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(), stream).Succeeded);

        stream.Position = 0;
        using (var document = PresentationDocument.Open(stream, false))
        {
            var presentation = document.PresentationPart!.Presentation!;
            Assert.True(presentation.EmbedTrueTypeFonts?.Value);
            var font = Assert.Single(presentation.EmbeddedFontList!.Elements<DocumentFormat.OpenXml.Presentation.EmbeddedFont>());
            Assert.Equal("Prova Sans", font.GetFirstChild<DocumentFormat.OpenXml.Presentation.Font>()!.Typeface!.Value);
            var part = (FontPart)document.PresentationPart.GetPartById(font.GetFirstChild<DocumentFormat.OpenXml.Presentation.RegularFont>()!.Id!.Value!);
            Assert.Equal("application/x-fontdata", part.ContentType);
            using var data = part.GetStream();
            var header = new byte[36];
            data.ReadExactly(header);
            Assert.Equal(0x504C, BitConverter.ToUInt16(header, 34));
            Assert.Null(font.GetFirstChild<DocumentFormat.OpenXml.Presentation.ItalicFont>());
        }

        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream).Value!;
        Assert.Equal(2, read.Fonts.Files.Count);
        Assert.Equal(Fonts().Files[0].Data, read.Fonts.Files.Single(file => !file.IsBold).Data);
    }

    [Fact]
    public void PptxDiPowerPointConCaratteriCompressiMtxSiLegge()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "figtree-mtx.pptx"));
        var result = new PptxReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var read = result.Value!;

        Assert.Equal(4, read.Fonts.Files.Count);
        var reader = new PulseOffice.Typography.OpenType.OpenTypeReader();
        foreach (var file in read.Fonts.Files)
        {
            Assert.Equal("Figtree", file.Family);
            Assert.Equal("Figtree", Assert.Single(reader.Read(file.Data)).Family);
        }

        Assert.Equal([false, true, false, true], read.Fonts.Files.Select(file => file.IsBold));
        Assert.Equal([false, false, true, true], read.Fonts.Files.Select(file => file.Italic));
    }

    [Fact]
    public void PptxConUnaParteMancanteSiApreEBoldonseSiCarica()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "boldonse-mtx.pptx"));
        var result = new PptxReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var file = Assert.Single(result.Value!.Fonts.Files);
        Assert.Equal("Boldonse", file.Family);
        Assert.Equal("Boldonse", Assert.Single(new PulseOffice.Typography.OpenType.OpenTypeReader().Read(file.Data)).Family);
        Assert.Contains(result.Value.Slides.SelectMany(slide => slide.Elements).OfType<TextElement>()
            .SelectMany(element => element.Text.Paragraphs).SelectMany(paragraph => paragraph.EffectiveRuns()),
            run => run.Font == "Boldonse");
    }

    [Fact]
    public void PptxConservaIlCaratterePropriaDiUnaPorzione()
    {
        var deck = Deck();
        var title = deck.Slides[0].Placeholder(PlaceholderRole.Title)!;
        deck.Slides[0].Replace(title with { Text = new RichText(PlaceholderRole.Title, [new TextParagraph("Titolo") { Runs = [new TextRun("Titolo") { Font = "Prova Sans" }] }]) });

        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream).Value!;

        var run = read.Slides[0].Elements.OfType<TextElement>().SelectMany(element => element.Text.Paragraphs)
            .SelectMany(paragraph => paragraph.EffectiveRuns()).First(candidate => candidate.Text == "Titolo");
        Assert.Equal("Prova Sans", run.Font);
    }

    [Fact]
    public void XlsxDichiaraLaPerditaDeiCaratteriIncorporati()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati");
        workbook.Fonts = Fonts();

        var report = new XlsxSpreadsheetWriter().CheckFidelity(workbook);

        Assert.Contains(report.Losses, loss => loss.Code == "XLSX036");
    }
}
