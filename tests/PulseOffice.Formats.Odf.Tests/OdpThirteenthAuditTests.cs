using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpThirteenthAuditTests
{
    private readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private readonly XNamespace _svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private readonly XNamespace _xlink = "http://www.w3.org/1999/xlink";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void La_quota_si_legge_come_quota_senza_dichiarazioni()
    {
        var read = Read(page => page.Add(new XElement(_draw + "measure",
            new XAttribute(_svg + "x1", "1cm"), new XAttribute(_svg + "y1", "2cm"), new XAttribute(_svg + "x2", "6cm"), new XAttribute(_svg + "y2", "2cm"))));

        var element = Assert.Single(read.Value!.Slides[0].Elements.OfType<ObjectElement>(), candidate => candidate.Content is ShapeObject { Dimension: not null });
        var dimension = ((ShapeObject)element.Content).Dimension!;
        Assert.Equal(5, element.Bounds.Width.ToCentimeters(), 2);
        Assert.Equal(0, dimension.Start.X, 3);
        Assert.Equal(1, dimension.End.X, 3);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF190");
    }

    [Fact]
    public void La_scena_3d_si_legge_e_le_animazioni_restano()
    {
        XNamespace dr3d = "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0";
        var read = Read(page => page.Add(new XElement(dr3d + "scene", new XAttribute(_draw + "id", "id9"),
            new XAttribute(_svg + "x", "8cm"), new XAttribute(_svg + "y", "2.5cm"), new XAttribute(_svg + "width", "3.5cm"), new XAttribute(_svg + "height", "3.5cm"),
            new XElement(dr3d + "light", new XAttribute(dr3d + "enabled", "true")), new XElement(dr3d + "sphere"))));

        var scene = Assert.IsType<PulseOffice.Drawing.Scenes.Scene3DObject>(Assert.Single(read.Value!.Slides[0].Elements.OfType<ObjectElement>()).Content);
        Assert.Equal("sphere", System.Xml.Linq.XElement.Parse(scene.Markup).Elements().Last().Name.LocalName);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODP102");
    }

    [Fact]
    public void Un_oggetto_senza_dati_ne_immagine_si_dichiara()
    {
        var read = Read(page => page.Add(new XElement(_draw + "frame",
            new XAttribute(_svg + "x", "1cm"), new XAttribute(_svg + "y", "1cm"), new XAttribute(_svg + "width", "4cm"), new XAttribute(_svg + "height", "2cm"),
            new XElement(_draw + "object", new XAttribute(_xlink + "href", "./Object 9")),
            new XElement(_draw + "image", new XAttribute(_xlink + "href", "./ObjectReplacements/Object 9")))));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODP180");
        Assert.DoesNotContain(Read(_ => { }).Diagnostics, diagnostic => diagnostic.Code == "ODP180");
    }

    [Fact]
    public void Il_collegamento_dentro_uno_span_si_legge()
    {
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        var read = Read(page => page.Add(new XElement(_draw + "frame",
            new XAttribute(_svg + "x", "1cm"), new XAttribute(_svg + "y", "1cm"), new XAttribute(_svg + "width", "10cm"), new XAttribute(_svg + "height", "2cm"),
            new XElement(_draw + "text-box", new XElement(text + "p",
                new XElement(text + "span", "Vedi ", new XElement(text + "a", new XAttribute(_xlink + "href", "https://example.org/"), "sito"), " ora"))))));

        var runs = read.Value!.Slides[0].Elements.OfType<TextElement>().SelectMany(element => element.Text.Paragraphs).SelectMany(paragraph => paragraph.Runs).ToList();
        Assert.Equal("https://example.org/", runs.Single(run => run.Text == "sito").Hyperlink);
        Assert.Equal("Vedi sito ora", string.Concat(runs.Select(run => run.Text)));
    }

    [Fact]
    public void Il_collegamento_nel_testo_di_una_forma_si_legge()
    {
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        var read = Read(page => page.Add(new XElement(_draw + "ellipse",
            new XAttribute(_svg + "x", "2cm"), new XAttribute(_svg + "y", "2cm"), new XAttribute(_svg + "width", "6cm"), new XAttribute(_svg + "height", "3cm"),
            new XElement(text + "p", new XElement(text + "a", new XAttribute(_xlink + "href", "https://example.org/"), "sito")))));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODP181");
        var shape = (PulseOffice.Drawing.Shapes.ShapeObject)read.Value!.Slides[0].Elements.OfType<ObjectElement>().Single().Content;
        Assert.Equal("https://example.org/", shape.Runs!.Single(run => run.Text == "sito").Link);
    }

    [Fact]
    public void Una_forma_libera_di_riferimento_resta_forma_libera_e_le_geometrie_a_formule_si_dichiarano()
    {
        var read = Read(page =>
        {
            page.Add(new XElement(_draw + "custom-shape", new XAttribute(_svg + "x", "1cm"), new XAttribute(_svg + "y", "1cm"), new XAttribute(_svg + "width", "4cm"), new XAttribute(_svg + "height", "2cm"),
                new XElement(_draw + "enhanced-geometry", new XAttribute(_svg + "viewBox", "0 0 841 854"), new XAttribute(_draw + "type", "non-primitive"),
                    new XAttribute(_draw + "enhanced-path", "M 517 247 L 517 415 264 415 264 0 0 0 0 680 517 680 Z N"))));
            page.Add(new XElement(_draw + "custom-shape", new XAttribute(_svg + "x", "6cm"), new XAttribute(_svg + "y", "1cm"), new XAttribute(_svg + "width", "4cm"), new XAttribute(_svg + "height", "2cm"),
                new XElement(_draw + "enhanced-geometry", new XAttribute(_svg + "viewBox", "0 0 21600 21600"), new XAttribute(_draw + "type", "mso-spt100"),
                    new XAttribute(_draw + "enhanced-path", "M 0 ?f1 L ?f2 0 Z N"))));
        });

        Assert.Contains(read.Value!.Slides[0].Elements.OfType<ObjectElement>(), element => element.Content is PulseOffice.Drawing.Freeform.FreeformObject);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODP183");
        Assert.DoesNotContain(Read(_ => { }).Diagnostics, diagnostic => diagnostic.Code == "ODP183");
    }

    [Fact]
    public void Le_forme_nascoste_si_leggono_nascoste()
    {
        var read = Read(page => page.Add(new XElement(_draw + "rect", new XAttribute(_draw + "display", "none"),
            new XAttribute(_svg + "x", "2cm"), new XAttribute(_svg + "y", "2cm"), new XAttribute(_svg + "width", "6cm"), new XAttribute(_svg + "height", "3cm"))));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODP182");
        Assert.True(read.Value!.Slides[0].Elements.OfType<ObjectElement>().Single().Hidden);
    }

    [Fact]
    public void Una_cornice_di_testo_senza_classe_resta_una_casella_libera()
    {
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        var read = Read(page => page.Add(new XElement(_draw + "frame", new XAttribute(_draw + "name", "Title"),
            new XAttribute(_svg + "x", "2cm"), new XAttribute(_svg + "y", "2cm"), new XAttribute(_svg + "width", "20cm"), new XAttribute(_svg + "height", "3cm"),
            new XElement(_draw + "text-box", new XElement(text + "p", "Test")))));

        var element = Assert.Single(read.Value!.Slides[0].Elements.OfType<TextElement>(), candidate => candidate.Text.PlainText == "Test");
        Assert.Equal(PlaceholderRole.Free, element.Text.Role);
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
