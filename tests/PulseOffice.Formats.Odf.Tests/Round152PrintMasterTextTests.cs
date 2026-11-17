using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round152PrintMasterTextTests
{
    private const string Header = "Course  A\tB";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace PresentationNs = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void Notes_and_handout_headers_keep_tabs_and_repeated_spaces_as_odf_markup()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));
        deck.NotesMaster = new NotesMaster { Header = Header };
        deck.Handout = new HandoutMaster { Header = Header };

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);

        buffer.Position = 0;
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var styles = archive.GetEntry("styles.xml")!.Open();
            var headers = XDocument.Load(styles).Descendants()
                .Where(element => (string?)element.Attribute(PresentationNs + "class") == "header" && element.Descendants(Text + "p").Any(paragraph => paragraph.Value.Contains("Course", StringComparison.Ordinal))).ToList();
            Assert.NotEmpty(headers);
            foreach (var header in headers)
            {
                Assert.Single(header.Descendants(Text + "tab"));
                Assert.Single(header.Descendants(Text + "s"));
            }
        }

        buffer.Position = 0;
        var result = new OdpReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        Assert.Equal(Header, result.Value!.NotesMaster.Header);
        Assert.Equal(Header, result.Value!.Handout.Header);
    }
}
