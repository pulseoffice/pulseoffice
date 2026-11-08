using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfTextCodecsAuditTests
{
    private readonly XNamespace _draw = OdfPackage.Namespaces.Draw;
    private readonly XNamespace _svg = OdfPackage.Namespaces.Svg;
    private readonly XNamespace _text = OdfPackage.Namespaces.Text;
    private readonly XNamespace _style = OdfPackage.Namespaces.Style;
    private readonly XNamespace _fo = OdfPackage.Namespaces.Fo;
    private readonly XNamespace _office = OdfPackage.Namespaces.Office;
    private readonly XNamespace _number = OdfPackage.Namespaces.Number;
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e300pt")]
    [InlineData("-5pt")]
    [InlineData("0pt")]
    public void La_dimensione_del_paragrafo_e_dello_span_resta_nei_limiti(string size)
    {
        var paragraph = ReadParagraph(
            [Style("P9", "paragraph", new XElement(_style + "text-properties", new XAttribute(_fo + "font-size", size))),
             Style("T9", "text", new XElement(_style + "text-properties", new XAttribute(_fo + "font-size", size)))],
            new XElement(_text + "p", new XAttribute(_text + "style-name", "P9"),
                new XElement(_text + "span", new XAttribute(_text + "style-name", "T9"), "testo")));

        Assert.InRange(paragraph.FontSize, 1, 4000);
        Assert.InRange(paragraph.Runs.Single().FontSize!.Value, 1, 4000);
    }

    [Theory]
    [InlineData("Infinity%")]
    [InlineData("1e300%")]
    public void L_interlinea_resta_nei_limiti(string height)
    {
        var paragraph = ReadParagraph(
            [Style("P9", "paragraph", new XElement(_style + "paragraph-properties", new XAttribute(_fo + "line-height", height)))],
            new XElement(_text + "p", new XAttribute(_text + "style-name", "P9"), "testo"));

        Assert.InRange(paragraph.LineSpacing, 0.01, 10);
    }

    [Theory]
    [InlineData("NaNcm")]
    [InlineData("Infinitycm")]
    [InlineData("1e300cm")]
    public void Spazi_rientri_e_tabulazioni_restano_finiti(string length)
    {
        var paragraph = ReadParagraph(
            [Style("P9", "paragraph", new XElement(_style + "paragraph-properties",
                new XAttribute(_fo + "margin-top", length), new XAttribute(_fo + "margin-bottom", length),
                new XAttribute(_fo + "margin-left", length), new XAttribute(_fo + "margin-right", length),
                new XAttribute(_fo + "text-indent", length),
                new XElement(_style + "tab-stops", new XElement(_style + "tab-stop", new XAttribute(_style + "position", length)))))],
            new XElement(_text + "p", new XAttribute(_text + "style-name", "P9"), "testo"));

        Assert.InRange(paragraph.SpaceBefore, -10_000, 10_000);
        Assert.InRange(paragraph.SpaceAfter, -10_000, 10_000);
        Assert.InRange(paragraph.MarginLeft ?? 0, -10_000, 10_000);
        Assert.InRange(paragraph.MarginRight ?? 0, -10_000, 10_000);
        Assert.InRange(paragraph.Indent ?? 0, -10_000, 10_000);
        Assert.All(paragraph.TabStops ?? [], stop => Assert.InRange(stop.Position, 0, 10_000));
    }

    [Fact]
    public void I_decimali_dei_secondi_di_un_campo_ora_hanno_un_tetto()
    {
        var paragraph = ReadParagraph(
            [new XElement(_number + "time-style", new XAttribute(_style + "name", "N9"),
                new XElement(_number + "hours"), new XElement(_number + "text", ":"),
                new XElement(_number + "seconds", new XAttribute(_number + "decimal-places", "2000000000")))],
            new XElement(_text + "p", new XElement(_text + "span",
                new XElement(_text + "time", new XAttribute(_style + "data-style-name", "N9"), "10:00"))));

        var format = paragraph.Runs.Single().FieldDetail!.Format!;
        Assert.True(format.Length < 32, format.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Un_mese_con_cinque_lettere_resta_un_mese()
    {
        var deck = NewDeck();
        deck.Slides[0].Add(new TextElement
        {
            Id = "data",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(
                PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero,
                PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(1)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("x") { Runs = [new TextRun("x") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Format = "MMMMM yyyy" } }] }]),
        });
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(stream).Value!;

        var format = back.Slides[0].Elements.OfType<TextElement>().Single().Text.Paragraphs[0].Runs[0].FieldDetail!.Format!;
        Assert.StartsWith("MMMM", format, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("span")]
    [InlineData("meta")]
    public void Elementi_annidati_senza_fine_non_fanno_traboccare_la_pila(string name)
    {
        var inner = new XElement(_text + name, "fondo");
        for (var depth = 0; depth < 50_000; depth++) inner = new XElement(_text + name, inner);

        var paragraph = ReadParagraph([], new XElement(_text + "p", "inizio ", inner));

        Assert.StartsWith("inizio", paragraph.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Infinity%")]
    [InlineData("1e300%")]
    public void La_dimensione_relativa_del_punto_elenco_ha_un_tetto(string size)
    {
        var list = new XElement(_text + "list-style", new XAttribute(_style + "name", "L9"),
            new XElement(_text + "list-level-style-bullet", new XAttribute(_text + "level", "1"), new XAttribute(_text + "bullet-char", "•"),
                new XElement(_style + "text-properties", new XAttribute(_fo + "font-size", size))));
        var paragraph = ReadParagraph([list],
            new XElement(_text + "list", new XAttribute(_text + "style-name", "L9"),
                new XElement(_text + "list-item", new XElement(_text + "p", "voce"))));

        Assert.InRange(paragraph.BulletSymbol!.RelativeSize ?? 1, 0.01, 4);
    }

    [Fact]
    public void Un_punto_elenco_immagine_incorporato_si_legge_con_la_sua_misura()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAAEUlEQVR4nGP4z8DwHwyBNAMAP+IF+0xnmpAAAAAASUVORK5CYII=");
        var list = new XElement(_text + "list-style", new XAttribute(_style + "name", "L9"),
            new XElement(_text + "list-level-style-image", new XAttribute(_text + "level", "1"),
                new XElement(_style + "list-level-properties", new XAttribute(_fo + "width", "0.5cm"), new XAttribute(_fo + "height", "0.25cm")),
                new XElement(_office + "binary-data", Convert.ToBase64String(png))));
        var paragraph = ReadParagraph([list],
            new XElement(_text + "list", new XAttribute(_text + "style-name", "L9"),
                new XElement(_text + "list-item", new XElement(_text + "p", "voce"))));

        var picture = paragraph.BulletSymbol!.Picture!;
        Assert.True(paragraph.Bullet);
        Assert.Equal(png, picture.Data.ToArray());
        Assert.Equal(0.5 * 72 / 2.54, picture.Width, 2);
        Assert.Equal(0.25 * 72 / 2.54, picture.Height, 2);
    }

    private XElement Style(string name, string family, params object[] content) =>
        new(_style + "style", new XAttribute(_style + "name", name), new XAttribute(_style + "family", family), content);

    private Presentation NewDeck()
    {
        var deck = new Presentation { Theme = _themes.Default, Master = _masters.CreateStandard(_themes.Default) };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        return deck;
    }

    private TextParagraph ReadParagraph(IEnumerable<XElement> styles, XElement body)
    {
        using var written = new MemoryStream();
        Assert.True(new OdpWriter().Write(NewDeck(), written).Succeeded);
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
                var automatic = content.Root!.Element(_office + "automatic-styles");
                if (automatic is null)
                {
                    automatic = new XElement(_office + "automatic-styles");
                    content.Root.AddFirst(automatic);
                }
                automatic.Add(styles);
                content.Descendants(_draw + "page").First().Add(new XElement(_draw + "frame",
                    new XAttribute(_svg + "x", "1cm"), new XAttribute(_svg + "y", "1cm"), new XAttribute(_svg + "width", "10cm"), new XAttribute(_svg + "height", "2cm"),
                    new XElement(_draw + "text-box", body)));
                content.Save(to, SaveOptions.DisableFormatting);
            }

        output.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(output);
        Assert.True(read.Succeeded);
        return read.Value!.Slides[0].Elements.OfType<TextElement>().Single().Text.Paragraphs[0];
    }
}
