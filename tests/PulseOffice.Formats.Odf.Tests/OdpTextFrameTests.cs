using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpTextFrameTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private static readonly TextFrame Custom = new()
    {
        Anchor = TextAnchor.Bottom,
        Insets = new TextInsets(10, 5, 12, 6),
        Autofit = TextAutofit.ResizeShape,
    };

    private byte[] Write(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private Presentation Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new OdpReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        return deck;
    }

    [Fact]
    public void AdattaLaFormaSenzaACapoERientroDellaPrimaRigaAttraversanoLOdp()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var body = slide.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);
        var bounds = body.Bounds with { Height = new PulseOffice.Foundation.Units.Twip(1234) };
        slide.Replace(body with
        {
            Bounds = bounds,
            Text = new RichText(PlaceholderRole.Body, [new TextParagraph("Testo libero") { MarginLeft = 0, Indent = 24 }])
            {
                Frame = TextFrame.Default with { Autofit = TextAutofit.ResizeShape, WordWrap = false },
            },
        });

        var back = Read(Write(deck)).Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);

        Assert.Equal(TextAutofit.ResizeShape, back.Text.Frame.Autofit);
        Assert.False(back.Text.Frame.WordWrap);
        Assert.InRange(back.Bounds.Height.Value, 1230, 1238);
        Assert.Equal(24, back.Text.Paragraphs[0].Indent!.Value, 1);
        Assert.Equal(0, back.Text.Paragraphs[0].MarginLeft!.Value);
        Assert.False(back.Text.Paragraphs[0].IsListItem);
    }

    [Fact]
    public void IlRientroDellaPrimaRigaSenzaMargineScriveUnMargineMinimoCheUnAltraSuiteEsporta()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var body = slide.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);
        slide.Replace(body with { Text = new RichText(PlaceholderRole.Body, [new TextParagraph("Testo") { MarginLeft = 0, Indent = 48 }]) });

        using var archive = new ZipArchive(new MemoryStream(Write(deck)));
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        var content = XDocument.Parse(reader.ReadToEnd());
        XNamespace fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
        var properties = content.Descendants().Single(e => e.Attribute(fo + "text-indent") is not null);

        Assert.NotEqual("0cm", (string?)properties.Attribute(fo + "margin-left"));
        Assert.StartsWith("0.00", (string?)properties.Attribute(fo + "margin-left"));
    }

    [Fact]
    public void LaCorniceDelTestoEIRientriAttraversanoLOdp()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var body = slide.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);
        slide.Replace(body with
        {
            Text = new RichText(PlaceholderRole.Body,
                [new TextParagraph("Uno") { Bullet = true, MarginLeft = 27, Indent = -27 }]) { Frame = Custom },
        });

        var back = Read(Write(deck)).Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);

        Assert.Equal(TextAnchor.Bottom, back.Text.Frame.Anchor);
        Assert.Equal(TextAutofit.ResizeShape, back.Text.Frame.Autofit);
        Assert.Equal(10, back.Text.Frame.Insets.Left, 1);
        Assert.Equal(6, back.Text.Frame.Insets.Bottom, 1);
        Assert.Equal(27, back.Text.Paragraphs[0].MarginLeft!.Value, 1);
        Assert.Equal(-27, back.Text.Paragraphs[0].Indent!.Value, 1);
    }

    [Fact]
    public void IlRiduciTestoSiScriveComeShrinkToFit()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var title = slide.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Title);
        slide.Replace(title with { Text = title.Text with { Frame = TextFrame.Default with { Autofit = TextAutofit.ShrinkText, FontScale = 0.8 } } });

        var back = Read(Write(deck)).Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Title);
        Assert.Equal(TextAutofit.ShrinkText, back.Text.Frame.Autofit);
        Assert.Contains(new OdpWriter().CheckFidelity(deck).Losses, d => d.Code == "ODP019");
    }

    [Fact]
    public void ILayoutDelMasterConservanoLaCornice()
    {
        var deck = Deck();
        var master = deck.Master;
        deck.Master = master with
        {
            Layouts = [.. master.Layouts.Select(layout => layout.Kind != SlideLayout.TitleAndContent ? layout : layout with
            {
                Placeholders = [.. layout.Placeholders.Select(p => p.Role == PlaceholderRole.Body ? p with { Frame = Custom } : p)],
            })],
        };

        var back = Read(Write(deck)).Master.Template(SlideLayout.TitleAndContent).Placeholders.Single(p => p.Role == PlaceholderRole.Body);
        Assert.Equal(TextAnchor.Bottom, back.Frame.Anchor);
        Assert.Equal(12, back.Frame.Insets.Right, 1);
    }

    [Fact]
    public void IlTitoloDelNostroMasterEAncoratoComeInPowerPoint()
    {
        var back = Read(Write(Deck()));
        Assert.Equal(TextAnchor.Middle, back.Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Title).Text.Frame.Anchor);
        Assert.Equal(TextAnchor.Bottom, back.Master.Template(SlideLayout.TitleSlide).Placeholders.Single(p => p.Role == PlaceholderRole.Title).Frame.Anchor);
    }

    [Fact]
    public void LoStileDiPresentazioneDellaCorniceDecideLAncoraggio()
    {
        var bytes = Write(Deck());
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
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
                XNamespace draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
                XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
                XNamespace presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";
                XNamespace office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
                var frame = content.Descendants(draw + "frame").First(f => (string?)f.Attribute(presentation + "class") == "title");
                var own = (string)frame.Attribute(draw + "style-name")!;
                content.Descendants(style + "style").First(s => (string?)s.Attribute(style + "name") == own)
                    .Element(style + "graphic-properties")!.Attribute(draw + "textarea-vertical-align")!.Remove();
                content.Root!.Element(office + "automatic-styles")!.Add(new XElement(style + "style",
                    new XAttribute(style + "name", "prX"), new XAttribute(style + "family", "presentation"),
                    new XElement(style + "graphic-properties", new XAttribute(draw + "textarea-vertical-align", "bottom"))));
                frame.SetAttributeValue(presentation + "style-name", "prX");
                content.Save(to);
            }

        var title = Read(output.ToArray()).Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Title);
        Assert.Equal(TextAnchor.Bottom, title.Text.Frame.Anchor);
    }

    [Fact]
    public void SenzaACapoConAdattaLaFormaSiScriveComeLaLeggonoLeAltreSuite()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var body = slide.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);
        slide.Replace(body with { Text = body.Text.WithPlainText("Una riga") with { Frame = TextFrame.Default with { Autofit = TextAutofit.ResizeShape, WordWrap = false } } });
        var bytes = Write(deck);

        using (var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes)))
        using (var reader = new StreamReader(archive.GetEntry("content.xml")!.Open()))
            Assert.Contains("draw:auto-grow-width=\"true\"", reader.ReadToEnd());

        using var stripped = new MemoryStream();
        using (var source = new System.IO.Compression.ZipArchive(new MemoryStream(bytes)))
        using (var target = new System.IO.Compression.ZipArchive(stripped, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                using var input = new StreamReader(entry.Open());
                var text = input.ReadToEnd();
                if (entry.FullName == "content.xml") text = text.Replace("fo:wrap-option=\"no-wrap\"", string.Empty);
                using var output = new StreamWriter(target.CreateEntry(entry.FullName).Open());
                output.Write(text);
            }

        var back = Read(stripped.ToArray()).Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);
        Assert.False(back.Text.Frame.WordWrap);
    }
    [Theory]
    [InlineData(TextAutofit.ShrinkText)]
    [InlineData(TextAutofit.None)]
    public void UnaCasellaLiberaSenzaACapoSiScriveComeFormaCheUnAltraSuiteEsportaSenzaACapo(TextAutofit autofit)
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var frame = TextFrame.Default with { Autofit = autofit, WordWrap = false, Anchor = TextAnchor.Middle, Insets = new TextInsets(10, 5, 12, 6) };
        slide.Add(new TextElement
        {
            Id = "libera",
            Bounds = slide.Elements[0].Bounds,
            ZOrder = 7,
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("Uno due") { Runs = [new TextRun("Uno ") { Bold = true }, new TextRun("due")] }]) { Frame = frame },
        });
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "libera" });

        var bytes = Write(deck);
        using (var archive = new ZipArchive(new MemoryStream(bytes)))
        using (var reader = new StreamReader(archive.GetEntry("content.xml")!.Open()))
        {
            var content = reader.ReadToEnd();
            Assert.Contains("draw:type=\"pulseoffice-text-box\"", content);
        }

        var back = Read(bytes).Slides[0];
        var free = back.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Free);
        Assert.Equal("Uno due", free.Text.Paragraphs[0].Text);
        Assert.True(free.Text.Paragraphs[0].Runs[0].Bold);
        Assert.False(free.Text.Frame.WordWrap);
        Assert.Equal(autofit, free.Text.Frame.Autofit);
        Assert.Equal(TextAnchor.Middle, free.Text.Frame.Anchor);
        Assert.Equal(10, free.Text.Frame.Insets.Left, 1);
        Assert.Equal(back.Elements.Max(e => e.ZOrder), free.ZOrder);
        Assert.Empty(back.Elements.OfType<ObjectElement>());
        Assert.Equal(free.Id, back.Animations.Single().TargetId);
        Assert.Contains(back.Elements.OfType<TextElement>(), e => e.Text.Role == PlaceholderRole.Title);
    }
    [Fact]
    public void RientroDestroETabulazioniAttraversanoIlFormato()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var body = slide.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);
        slide.Replace(body with
        {
            Text = new RichText(PlaceholderRole.Body, [new TextParagraph("Nome\tCifra")
            {
                MarginLeft = 0, Indent = 0, MarginRight = 36,
                TabStops = [new TabStop(72), new TabStop(144) { Alignment = TabAlignment.Right }],
            }]),
        });

        var back = Read(Write(deck)).Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body).Text.Paragraphs[0];

        Assert.Equal(36, back.MarginRight!.Value, 1);
        Assert.Equal(2, back.TabStops!.Count);
        Assert.Equal(72, back.TabStops[0].Position, 1);
        Assert.Equal(TabAlignment.Right, back.TabStops[1].Alignment);
        Assert.Equal(144, back.TabStops[1].Position, 1);
    }
}

public sealed class OdpTextFrameStyleScopeTests
{
    private const string Ns = """xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:presentation="urn:oasis:names:tc:opendocument:xmlns:presentation:1.0" xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" """;

    [Fact]
    public void IlGenitoreDiUnoStileSiCercaFraGliStiliComuniNonFraGliAutomatici()
    {
        var content = $"""
            <office:document-content {Ns} office:version="1.3"><office:automatic-styles>
            <style:style style:name="gr1" style:family="graphic" style:parent-style-name="Base"><style:graphic-properties draw:textarea-vertical-align="bottom"/></style:style>
            <style:style style:name="Base" style:family="graphic"><style:graphic-properties fo:padding-left="5cm"/></style:style>
            </office:automatic-styles><office:body><office:presentation>
            <draw:page draw:name="page1" draw:master-page-name="M">
            <draw:frame draw:style-name="gr1" svg:x="1cm" svg:y="1cm" svg:width="10cm" svg:height="3cm"><draw:text-box><text:p>Uno</text:p></draw:text-box></draw:frame>
            </draw:page></office:presentation></office:body></office:document-content>
            """;
        var styles = $"""
            <office:document-styles {Ns} office:version="1.3"><office:styles>
            <style:style style:name="Base" style:family="graphic"><style:graphic-properties fo:padding-left="1cm"/></style:style>
            </office:styles><office:automatic-styles>
            <style:style style:name="gr1" style:family="graphic"><style:graphic-properties draw:textarea-vertical-align="top" fo:padding-left="3cm"/></style:style>
            <style:page-layout style:name="PM1"><style:page-layout-properties fo:page-width="28cm" fo:page-height="15.75cm"/></style:page-layout>
            </office:automatic-styles><office:master-styles><style:master-page style:name="M" style:page-layout-name="PM1"/></office:master-styles></office:document-styles>
            """;

        var bytes = Package(content, styles);
        using var stream = new MemoryStream(bytes);
        var result = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(stream);
        Assert.True(result.Succeeded);

        var element = result.Value!.Slides[0].Elements.OfType<TextElement>().Single();
        Assert.Equal(TextAnchor.Bottom, element.Text.Frame.Anchor);
        Assert.Equal(28.35, element.Text.Frame.Insets.Left, 0);
    }

    private byte[] Package(string content, string styles)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            var mime = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var w = new StreamWriter(mime.Open())) w.Write("application/vnd.oasis.opendocument.presentation");
            foreach (var (name, body) in new[]
            {
                ("content.xml", content),
                ("styles.xml", styles),
                ("META-INF/manifest.xml", $"""<manifest:manifest {Ns} manifest:version="1.3"><manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.presentation"/><manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/><manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml"/></manifest:manifest>"""),
            })
            {
                using var w = new StreamWriter(archive.CreateEntry(name).Open());
                w.Write(body);
            }
        }
        return buffer.ToArray();
    }
}
