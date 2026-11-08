using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpPageAuditTests
{
    private readonly XNamespace _pulse = "urn:pulseoffice:presentation-master";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck()
    {
        var deck = new Presentation { Theme = _themes.Default, Master = _masters.CreateStandard(_themes.Default) };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        return deck;
    }

    private MemoryStream Rewritten(Presentation deck, string part, Action<XDocument> change)
    {
        var written = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, written).Succeeded);

        var result = new MemoryStream();
        written.Position = 0;
        using (var source = new ZipArchive(written, ZipArchiveMode.Read))
        using (var target = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var input = entry.Open();
                using var output = copy.Open();
                if (entry.FullName != part)
                {
                    input.CopyTo(output);
                    continue;
                }
                var document = XDocument.Load(input);
                change(document);
                document.Save(output);
            }
        result.Position = 0;
        return result;
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e300")]
    [InlineData("-5")]
    [InlineData("0")]
    public void La_dimensione_del_testo_delle_note_letta_resta_un_corpo_valido(string fontSize)
    {
        using var stream = Rewritten(Deck(), "styles.xml", styles =>
        {
            foreach (var attribute in styles.Descendants().Attributes(_pulse + "font-size")) attribute.Value = fontSize;
        });

        var deck = new OdpReader(_themes, _masters).Read(stream).Value!;

        Assert.True(double.IsFinite(deck.NotesMaster.FontSize));
        Assert.InRange(deck.NotesMaster.FontSize, 1, 4000);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("1e300pt")]
    [InlineData("-5pt")]
    [InlineData("0pt")]
    public void La_dimensione_del_testo_dei_segnaposto_letta_resta_un_corpo_valido(string fontSize)
    {
        using var stream = Rewritten(Deck(), "styles.xml", styles =>
        {
            foreach (var attribute in styles.Descendants().Attributes(OdfPackage.Namespaces.Fo + "font-size")) attribute.Value = fontSize;
        });

        var deck = new OdpReader(_themes, _masters).Read(stream).Value!;

        Assert.All(deck.Master.Layouts.SelectMany(layout => layout.Placeholders), placeholder =>
            Assert.InRange(placeholder.Seed.FontSize, 1, 4000));
    }

    [Theory]
    [InlineData("-5s")]
    [InlineData("Infinitys")]
    [InlineData("1e300s")]
    public void La_durata_della_transizione_letta_resta_valida(string duration)
    {
        var source = Deck();
        source.Slides[0].Transition = source.Slides[0].Transition with { Kind = TransitionKind.Fade, DurationMs = 700 };
        using var stream = Rewritten(source, "content.xml", content =>
        {
            foreach (var attribute in content.Descendants().Attributes(OdfPackage.Namespaces.Smil + "dur")) attribute.Value = duration;
        });

        var slide = new OdpReader(_themes, _masters).Read(stream).Value!.Slides[0];

        Assert.InRange(slide.Transition.DurationMs, 0, 3_600_000);
    }

    [Fact]
    public void Un_opacita_non_numerica_dell_immagine_di_sfondo_resta_piena()
    {
        var source = Deck();
        source.Slides[0].BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "bg", Data = new byte[] { 10, 20, 30, 40 }, MediaType = "image/png" };
        source.Slides[0].BackgroundLayout = BackgroundImageLayout.Stretched with { Opacity = 0.5 };
        using var stream = Rewritten(source, "content.xml", content =>
        {
            foreach (var attribute in content.Descendants().Attributes(OdfPackage.Namespaces.Draw + "opacity")) attribute.Value = "NaN%";
        });

        var slide = new OdpReader(_themes, _masters).Read(stream).Value!.Slides[0];

        Assert.Equal(1, slide.BackgroundLayout.Opacity);
    }
}
