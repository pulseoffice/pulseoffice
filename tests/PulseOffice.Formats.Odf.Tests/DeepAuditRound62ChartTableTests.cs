using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class DeepAuditRound62ChartTableTests
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    private static ChartDefinition Chart() => new()
    {
        Id = "ch1", Type = ChartType.Column, SourceSheet = "", SourceRange = "",
        InlineData = new ChartData(["A", "B"], [new ChartSeries("S", [1.5, 1234.25])]),
    };

    private static void WithSystem(string system, Action body)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static IReadOnlyList<(string Value, string Shown)> LocalTable(byte[] package)
    {
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var entry = Assert.Single(zip.Entries, e => e.FullName.StartsWith("Object", StringComparison.Ordinal) && e.FullName.EndsWith("/content.xml", StringComparison.Ordinal));
        using var stream = entry.Open();
        var content = XDocument.Load(stream);
        return [.. content.Descendants(Table + "table-cell")
            .Where(cell => (string?)cell.Attribute(Office + "value-type") == "float")
            .Select(cell => ((string)cell.Attribute(Office + "value")!, cell.Element(Text + "p")!.Value))];
    }

    private static byte[] Odt(string language)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Language = language;
        document.Body.Replace([new ParagraphBlock("p1", "Testo")]);
        document.Objects.Add(new AnchoredObject("o1", new ParagraphAnchor("p1"), new EmbeddedContent(Chart()),
            new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4))));
        using var stream = new MemoryStream();
        Assert.True(new Odf.Odt.OdtWriter().Write(document, stream).Succeeded);
        return stream.ToArray();
    }

    private static byte[] Odp(string language)
    {
        var theme = new ThemeCatalog().Find("Slate")!;
        var master = new MasterFactory().CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.Properties.Language = language;
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = "ch1",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(14), Twip.FromCentimeters(9)),
            Content = Chart(),
        });
        deck.AddExisting(slide);
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        return stream.ToArray();
    }

    [Theory]
    [InlineData("it-IT", "en-US", "1.5", "1234.25")]
    [InlineData("en-US", "it-IT", "1,5", "1234,25")]
    public void OdtLocalTableShowsTheDocumentLanguage(string system, string language, string first, string second)
    {
        IReadOnlyList<(string Value, string Shown)> cells = [];
        WithSystem(system, () => cells = LocalTable(Odt(language)));
        Assert.Equal([("1.5", first), ("1234.25", second)], cells);
    }

    [Theory]
    [InlineData("it-IT", "en-US", "1.5", "1234.25")]
    [InlineData("en-US", "it-IT", "1,5", "1234,25")]
    public void OdpLocalTableShowsTheDeckLanguage(string system, string language, string first, string second)
    {
        IReadOnlyList<(string Value, string Shown)> cells = [];
        WithSystem(system, () => cells = LocalTable(Odp(language)));
        Assert.Equal([("1.5", first), ("1234.25", second)], cells);
    }
}
