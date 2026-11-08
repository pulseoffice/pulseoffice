using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxTextFrameTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private static readonly TextFrame Custom = new()
    {
        Anchor = TextAnchor.Bottom,
        Insets = new TextInsets(10, 5, 12, 6),
        Autofit = TextAutofit.ShrinkText,
        FontScale = 0.85,
    };

    private Presentation Read(Stream stream)
    {
        var result = new PptxReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Value!;
    }

    private Presentation Fixture(params string[] path)
    {
        using var stream = File.OpenRead(Path.Combine([AppContext.BaseDirectory, "Fixtures", .. path]));
        return Read(stream);
    }

    private MemoryStream Write(Presentation deck)
    {
        var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        return buffer;
    }

    private Presentation Deck(Theme theme)
    {
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        return deck;
    }

    [Fact]
    public void AdattaLaFormaSenzaACapoERientroDellaPrimaRigaAttraversanoIlPptx()
    {
        var deck = Deck(_themes.Default);
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
        Assert.False(back.Text.Paragraphs[0].IsListItem);
    }

    [Fact]
    public void IlTitoloDiBoldonseEreditaAncoraggioERiduzioneDaLayoutEMaster()
    {
        var title = Assert.Single(Fixture("boldonse-mtx.pptx").Slides[0].Elements.OfType<TextElement>());

        Assert.Equal(TextAnchor.Bottom, title.Text.Frame.Anchor);
        Assert.Equal(TextAutofit.ShrinkText, title.Text.Frame.Autofit);
        Assert.Equal(TextInsets.Standard, title.Text.Frame.Insets);
    }

    [Fact]
    public void IRientriDeiParagrafiSiLeggonoDaPptxEDalMaster()
    {
        var body = Fixture("PowerPoint", "NumberedList-12ab-ab-34.pptx").Slides[0].Elements.OfType<TextElement>()
            .Single(element => element.Text.Role == PlaceholderRole.Body);

        Assert.Equal(40.5, body.Text.Paragraphs[0].MarginLeft!.Value, 1);
        Assert.Equal(-40.5, body.Text.Paragraphs[0].Indent!.Value, 1);
    }

    [Fact]
    public void LaCorniceEIRientriAttraversanoIlPptx()
    {
        var deck = Deck(_themes.Default);
        var slide = deck.Slides[0];
        var body = slide.Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);
        slide.Replace(body with
        {
            Text = new RichText(PlaceholderRole.Body,
                [new TextParagraph("Uno") { Bullet = true, MarginLeft = 27, Indent = -27 }]) { Frame = Custom },
        });
        deck.Master = deck.Master with
        {
            Layouts = [.. deck.Master.Layouts.Select(layout => layout.Kind != SlideLayout.TitleSlide ? layout : layout with
            {
                Placeholders = [.. layout.Placeholders.Select(p => p.Role == PlaceholderRole.Subtitle ? p with { Frame = Custom } : p)],
            })],
        };

        using var stream = Write(deck);
        var back = Read(stream);
        var text = back.Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body);

        Assert.Equal(Custom, text.Text.Frame);
        Assert.Equal(27, text.Text.Paragraphs[0].MarginLeft!.Value, 1);
        Assert.Equal(-27, text.Text.Paragraphs[0].Indent!.Value, 1);
        Assert.Equal(Custom, back.Master.Template(SlideLayout.TitleSlide).Placeholders.Single(p => p.Role == PlaceholderRole.Subtitle).Frame);
        Assert.Equal(TextAnchor.Middle, back.Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Title).Text.Frame.Anchor);
    }

    [Fact]
    public void UnTemaScuroSiScriveConLaMappaDeiColoriInvertitaESiRilegge()
    {
        var dark = _themes.Find("Slate")!;
        using var stream = Write(Deck(dark));

        using (var document = PresentationDocument.Open(stream, false))
        {
            var master = document.PresentationPart!.SlideMasterParts.First();
            Assert.Equal(D.ColorSchemeIndexValues.Dark1, master.SlideMaster!.ColorMap!.Background1!.Value);
            Assert.Equal(D.ColorSchemeIndexValues.Light1, master.SlideMaster.ColorMap.Text1!.Value);
            var scheme = master.ThemePart!.Theme!.ThemeElements!.ColorScheme!;
            Assert.Equal((dark.Colors.Background & 0xFFFFFF).ToString("X6"), scheme.Dark1Color!.RgbColorModelHex!.Val!.Value);
        }

        stream.Position = 0;
        var back = Read(stream);
        Assert.Equal(dark.Colors.Background, back.Theme.Colors.Background);
        Assert.Equal(dark.Colors.Text, back.Theme.Colors.Text);
    }

    [Fact]
    public void LaMappaDelMasterELaSostituzioneDellaDiapositivaSiApplicanoAiColori()
    {
        var source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PowerPoint", "NumberedList-12ab-ab-34.pptx"));
        var standard = Fixture("PowerPoint", "NumberedList-12ab-ab-34.pptx");

        var inverted = Edit(source, document =>
        {
            var map = document.PresentationPart!.SlideMasterParts.First().SlideMaster!.ColorMap!;
            (map.Background1, map.Text1) = (D.ColorSchemeIndexValues.Dark1, D.ColorSchemeIndexValues.Light1);
        });
        Assert.Equal(standard.Theme.Colors.Text, inverted.Theme.Colors.Background);
        Assert.Equal(standard.Theme.Colors.Background, inverted.Theme.Colors.Text);
        Assert.Equal(ColorRole.Text, inverted.Slides[0].Elements.OfType<TextElement>().First(e => e.Text.Role == PlaceholderRole.Title).Text.Paragraphs[0].ColorRole);

        var overridden = Edit(source, document =>
        {
            var slide = document.PresentationPart!.SlideParts.First().Slide!;
            slide.ColorMapOverride = new P.ColorMapOverride(new D.OverrideColorMapping
            {
                Background1 = D.ColorSchemeIndexValues.Dark1, Text1 = D.ColorSchemeIndexValues.Light1,
                Background2 = D.ColorSchemeIndexValues.Dark2, Text2 = D.ColorSchemeIndexValues.Light2,
                Accent1 = D.ColorSchemeIndexValues.Accent1, Accent2 = D.ColorSchemeIndexValues.Accent2,
                Accent3 = D.ColorSchemeIndexValues.Accent3, Accent4 = D.ColorSchemeIndexValues.Accent4,
                Accent5 = D.ColorSchemeIndexValues.Accent5, Accent6 = D.ColorSchemeIndexValues.Accent6,
                Hyperlink = D.ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
            });
        });
        Assert.Equal(standard.Theme.Colors.Background, overridden.Theme.Colors.Background);
        Assert.Equal(ColorRole.Background, overridden.Slides[0].Elements.OfType<TextElement>().First(e => e.Text.Role == PlaceholderRole.Title).Text.Paragraphs[0].ColorRole);
    }

    private Presentation Edit(byte[] source, Action<PresentationDocument> change)
    {
        using var buffer = new MemoryStream();
        buffer.Write(source);
        buffer.Position = 0;
        using (var document = PresentationDocument.Open(buffer, true)) change(document);
        buffer.Position = 0;
        return Read(buffer);
    }

    [Fact]
    public void LaRiduzioneInPercentualeDellOoxmlStrictSiLeggeSenzaRifiutareIlFile()
    {
        var deck = Deck(_themes.Default);
        var slide = deck.Slides[0];
        var title = slide.Elements.OfType<TextElement>().First();
        slide.Replace(title with { Text = title.Text with { Frame = Custom } });
        var written = Write(deck).ToArray();

        using var patched = new MemoryStream();
        patched.Write(written);
        using (var package = PresentationDocument.Open(patched, true))
            foreach (var part in package.PresentationPart!.SlideParts.ToList())
                foreach (var autofit in part.Slide!.Descendants<D.NormalAutoFit>().ToList())
                {
                    autofit.SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute("fontScale", string.Empty, "62.5%"));
                    autofit.SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute("lnSpcReduction", string.Empty, "x"));
                }

        patched.Position = 0;
        var back = Read(patched).Slides[0].Elements.OfType<TextElement>().First();
        Assert.Equal(0.625, back.Text.Frame.FontScale, 3);
    }

    [Theory]
    [InlineData("baseline", "abc")]
    [InlineData("u", "ondulato")]
    [InlineData("strike", "x")]
    [InlineData("cap", "x")]
    [InlineData("sz", "grande")]
    [InlineData("b", "forse")]
    public void UnAttributoRovinatoDiUnaPorzioneNonFaRifiutareIlFile(string attribute, string value)
    {
        var deck = Deck(_themes.Default);
        var title = deck.Slides[0].Elements.OfType<TextElement>().First();
        deck.Slides[0].Replace(title with { Text = new RichText(title.Text.Role, [new TextParagraph("Ciao").WithRuns([new TextRun("Ciao") { Highlight = 0xFFFFFF00 }])]) });
        var written = Write(deck).ToArray();

        using var patched = new MemoryStream();
        patched.Write(written);
        using (var package = PresentationDocument.Open(patched, true))
            foreach (var part in package.PresentationPart!.SlideParts.ToList())
                foreach (var run in part.Slide!.Descendants<D.RunProperties>().ToList())
                    run.SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute(attribute, string.Empty, value));

        patched.Position = 0;
        var back = Read(patched).Slides[0].Elements.OfType<TextElement>().First();
        Assert.Equal("Ciao", back.Text.PlainText);
    }

    [Theory]
    [InlineData("scheme")]
    [InlineData("preset")]
    public void LEvidenziazioneSiLeggeAncheDaTemaEDaColorePredefinito(string kind)
    {
        var deck = Deck(_themes.Default);
        var title = deck.Slides[0].Elements.OfType<TextElement>().First();
        deck.Slides[0].Replace(title with { Text = new RichText(title.Text.Role, [new TextParagraph("Ciao").WithRuns([new TextRun("Ciao") { Highlight = 0xFFFFFF00 }])]) });
        var written = Write(deck).ToArray();

        using var patched = new MemoryStream();
        patched.Write(written);
        using (var package = PresentationDocument.Open(patched, true))
            foreach (var part in package.PresentationPart!.SlideParts.ToList())
                foreach (var highlight in part.Slide!.Descendants<D.Highlight>().ToList())
                {
                    highlight.RemoveAllChildren();
                    highlight.Append(kind == "scheme"
                        ? new D.SchemeColor { Val = D.SchemeColorValues.Accent2 }
                        : new D.PresetColor { Val = D.PresetColorValues.DarkBlue });
                }

        patched.Position = 0;
        var back = Read(patched);
        var run = back.Slides[0].Elements.OfType<TextElement>().First().Text.Paragraphs[0].EffectiveRuns().First();
        Assert.Equal(kind == "scheme" ? back.Theme.Colors.Accent2 : 0xFF00008Bu, run.Highlight);
    }

    [Fact]
    public void RientroDestroETabulazioniAttraversanoIlFormato()
    {
        var deck = Deck(_themes.Default);
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
