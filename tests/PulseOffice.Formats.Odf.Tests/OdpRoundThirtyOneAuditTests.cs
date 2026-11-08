using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpRoundThirtyOneAuditTests
{
    private readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private readonly XNamespace _svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private readonly XNamespace _presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";
    private readonly XNamespace _script = "urn:oasis:names:tc:opendocument:xmlns:script:1.0";
    private readonly XNamespace _xlink = "http://www.w3.org/1999/xlink";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Theory]
    [InlineData("execute", true)]
    [InlineData("verb", true)]
    [InlineData("next-page", false)]
    public void Run_program_and_object_verb_actions_are_declared_when_read(string action, bool declared)
    {
        var read = Read(page => page.Add(new XElement(_draw + "custom-shape",
            new XAttribute(_svg + "x", "1cm"), new XAttribute(_svg + "y", "1cm"), new XAttribute(_svg + "width", "4cm"), new XAttribute(_svg + "height", "2cm"),
            new XElement(_office + "event-listeners", new XElement(_presentation + "event-listener",
                new XAttribute(_script + "event-name", "dom:click"), new XAttribute(_presentation + "action", action),
                new XAttribute(_xlink + "href", "/Applications/Calculator.app"))),
            new XElement(_draw + "enhanced-geometry", new XAttribute(_draw + "type", "rectangle")))));

        Assert.True(read.Succeeded);
        Assert.Equal(declared, read.Diagnostics.Any(diagnostic => diagnostic.Code == "ODP216"));
    }

    private PulseOffice.Foundation.Diagnostics.OperationResult<Presentation> Read(Action<XElement> change)
    {
        var deck = new Presentation { Theme = _themes.Default, Master = _masters.CreateStandard(_themes.Default) };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        using var written = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, written).Succeeded);
        written.Position = 0;
        using var output = new MemoryStream();
        using (var source = new ZipArchive(written, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var from = entry.Open();
                using var to = copy.Open();
                if (entry.FullName != "content.xml")
                {
                    from.CopyTo(to);
                    continue;
                }

                var content = XDocument.Load(from);
                change(content.Descendants(_draw + "page").First());
                content.Save(to, SaveOptions.DisableFormatting);
            }

        output.Position = 0;
        return new OdpReader(_themes, _masters).Read(output);
    }
}
