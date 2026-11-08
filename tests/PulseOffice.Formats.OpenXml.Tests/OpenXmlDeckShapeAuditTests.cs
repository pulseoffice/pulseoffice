using System.IO.Compression;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlDeckShapeAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private (Presentation Deck, Slide Slide) Base()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private static MemoryStream Written(Presentation deck)
    {
        var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return buffer;
    }

    private static void Rewrite(MemoryStream buffer, string part, Func<string, string> change)
    {
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry(part)!;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(part).Open());
            writer.Write(change(xml));
        }

        buffer.Position = 0;
    }

    [Fact]
    public void A_picture_whose_embed_names_a_missing_relationship_does_not_reject_the_presentation()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "img",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Content = new RasterImageObject { Id = "img", Data = new byte[] { 1, 2, 3, 4 }, MediaType = "image/png" },
        });
        using var buffer = Written(deck);
        Rewrite(buffer, "ppt/slides/slide1.xml", xml => System.Text.RegularExpressions.Regex.Replace(xml, "r:embed=\"[^\"]+\"", "r:embed=\"rId999\""));

        var result = new PptxReader(_themes, _masters).Read(buffer);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void Positions_beyond_the_twip_range_are_clamped_instead_of_wrapping_to_negative()
    {
        var (deck, slide) = Base();
        slide.Add(new ObjectElement
        {
            Id = "img",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(4000), new Twip(3000)),
            Content = new RasterImageObject { Id = "img", Data = new byte[] { 1, 2, 3, 4 }, MediaType = "image/png" },
        });
        using var buffer = Written(deck);
        Rewrite(buffer, "ppt/slides/slide1.xml", xml => System.Text.RegularExpressions.Regex.Replace(xml, "<a:off x=\"\\d+\"", "<a:off x=\"8000000000000\""));

        var element = new PptxReader(_themes, _masters).Read(buffer).Value!.Slides[0].Elements.OfType<ObjectElement>().Single();

        Assert.True(element.Bounds.Left.Value > 0);
    }
}
