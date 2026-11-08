using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpVersionsRoundTripTests
{
    private static Presentation Deck()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));
        return deck;
    }

    [Fact]
    public void LeVersioniSopravvivonoEOgniVoceEDichiarataNelManifest()
    {
        var deck = Deck();
        deck.Versions.Add([1, 2, 3, 4], "Alice", "Prima bozza");
        deck.Versions.Add([5, 6, 7], "Alice", "Seconda bozza");

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);

        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            var manifest = XDocument.Load(zip.GetEntry("META-INF/manifest.xml")!.Open());
            XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
            var declared = manifest.Descendants(ns + "file-entry")
                .Select(entry => (string)entry.Attribute(ns + "full-path")!)
                .ToHashSet(StringComparer.Ordinal);

            var undeclared = zip.Entries
                .Select(entry => entry.FullName)
                .Where(name => name != "mimetype" && !name.StartsWith("META-INF/", StringComparison.Ordinal))
                .Where(name => !declared.Contains(name))
                .ToList();

            Assert.Empty(undeclared);
        }

        stream.Position = 0;
        var back = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(stream).Value!;

        Assert.Equal(2, back.Versions.Count);
        Assert.Equal("Prima bozza", back.Versions.All[0].Comment);
        Assert.Equal("Alice", back.Versions.All[0].Author);
        Assert.Equal(new byte[] { 5, 6, 7 }, back.Versions.All[1].Content);
    }
}
