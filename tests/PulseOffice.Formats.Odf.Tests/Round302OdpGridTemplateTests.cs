using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round302OdpGridTemplateTests
{
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void A_grid_table_names_a_table_template_written_in_the_styles()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["x"], ["y"]] },
            },
        });

        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        using var zip = new ZipArchive(new MemoryStream(buffer.ToArray()), ZipArchiveMode.Read);
        XDocument Part(string name)
        {
            using var stream = zip.GetEntry(name)!.Open();
            return XDocument.Load(stream);
        }

        var name = (string?)Part("content.xml").Descendants(Table + "table").Single().Attribute(Table + "template-name");
        Assert.NotNull(name);
        var template = Part("styles.xml").Descendants(Table + "table-template").Single(t => (string?)t.Attribute(Table + "name") == name);
        Assert.NotNull(template.Element(Table + "first-row"));
        Assert.NotNull(template.Element(Table + "even-rows"));

        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer).Value!.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<DataTableObject>().Single();
        Assert.True(back.Style.Grid);
    }
}
