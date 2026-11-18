using System.IO.Compression;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round229PptxHeaderFooterTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void A_slide_number_the_master_hides_is_not_written_on_the_slides()
    {
        var master = _masters.CreateStandard(_themes.Default) with { Elements = new MasterElements(SlideNumber: false) };
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        deck.HeaderFooter = new HeaderFooterSettings { ShowSlideNumber = true, ShowFooter = true, FooterText = "Foot", ShowOnTitleSlide = true };

        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("ppt/slides/slide1.xml")!.Open());
        var xml = reader.ReadToEnd();

        Assert.Contains("type=\"ftr\"", xml);
        Assert.DoesNotContain("type=\"sldNum\"", xml);
    }

    [Fact]
    public void A_foreign_pptx_without_chrome_on_its_title_slide_reads_as_hidden_on_the_title_slide()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        deck.HeaderFooter = new HeaderFooterSettings { ShowFooter = true, FooterText = "Foot", ShowOnTitleSlide = false };

        using var written = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, written).Succeeded);
        using var foreign = new MemoryStream();
        foreign.Write(written.ToArray());
        using (var zip = new ZipArchive(foreign, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("ppt/presentation.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            xml = System.Text.RegularExpressions.Regex.Replace(xml, "<p:ext [^>]*uri=\"\\{QO-HEADERFOOTER\\}\">.*?</p:ext>", string.Empty);
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("ppt/presentation.xml").Open());
            writer.Write(xml);
        }
        foreign.Position = 0;

        var read = new PptxReader(_themes, _masters).Read(foreign).Value!;

        Assert.True(read.HeaderFooter.ShowFooter);
        Assert.False(read.HeaderFooter.ShowOnTitleSlide);
    }
}
