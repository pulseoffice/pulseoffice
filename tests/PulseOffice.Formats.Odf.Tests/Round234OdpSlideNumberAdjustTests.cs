using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round234OdpSlideNumberAdjustTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void Numbering_from_zero_writes_page_adjust_on_slide_number_fields_and_reads_it_back_alone()
    {
        using var stream = Written(0);

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var styles = Xml(archive, "styles.xml");
            var fields = styles.Descendants(Text + "page-number").ToList();
            Assert.NotEmpty(fields);
            Assert.All(fields, field => Assert.Equal("-1", (string?)field.Attribute(Text + "page-adjust")));
            foreach (var attribute in styles.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "first-slide-number").ToList())
                attribute.Remove();
            Replace(archive, "styles.xml", styles);
        }

        stream.Position = 0;
        Assert.Equal(0, new OdpReader(_themes, _masters).Read(stream).Value!.FirstSlideNumber);
    }

    [Fact]
    public void Numbering_from_one_writes_no_page_adjust()
    {
        using var stream = Written(1);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        Assert.DoesNotContain(Xml(archive, "styles.xml").Descendants(Text + "page-number"), field => field.Attribute(Text + "page-adjust") is not null);
    }

    private MemoryStream Written(int first)
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default, FirstSlideNumber = first };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));
        var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private static XDocument Xml(ZipArchive archive, string name)
    {
        using var entry = archive.GetEntry(name)!.Open();
        return XDocument.Load(entry);
    }

    private static void Replace(ZipArchive archive, string name, XDocument document)
    {
        archive.GetEntry(name)!.Delete();
        using var entry = archive.CreateEntry(name).Open();
        document.Save(entry);
    }
}
