using System.IO.Compression;
using System.Text;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckEighthAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(params TextParagraph[] paragraphs)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        if (paragraphs.Length > 0)
            deck.Slides[0].Add(new TextElement
            {
                Id = "box",
                Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(20), Twip.FromCentimeters(8)),
                Text = new RichText(PlaceholderRole.Free, paragraphs),
            });
        return deck;
    }

    private byte[] Odp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private byte[] Pptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private Presentation ReadPptx(byte[] bytes)
    {
        var result = new PptxReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation ReadOdp(byte[] bytes)
    {
        var result = new OdpReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private string Entry(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private IReadOnlyList<TextParagraph> Paragraphs(Presentation deck) =>
        deck.Slides[0].Elements.OfType<TextElement>().Single().Text.Paragraphs;

    [Fact]
    public void La_lingua_predefinita_fa_il_giro_in_ODP_e_PPTX()
    {
        var deck = Deck();
        deck.DefaultLanguages = new TextLanguages("it-IT", "zh-CN", "hi-IN");
        deck.DefaultScriptFonts = new ScriptFonts("Noto Sans CJK SC", "Noto Sans Devanagari");

        var odp = Odp(deck);
        var styles = Entry(odp, "styles.xml");
        Assert.Contains("fo:language=\"it\" fo:country=\"IT\"", styles);
        Assert.Contains("style:language-asian=\"zh\" style:country-asian=\"CN\"", styles);
        Assert.Contains("style:language-complex=\"hi\" style:country-complex=\"IN\"", styles);
        var fromOdp = ReadOdp(odp);
        Assert.Equal(deck.DefaultLanguages, fromOdp.DefaultLanguages);
        Assert.Equal(deck.DefaultScriptFonts, fromOdp.DefaultScriptFonts);

        var pptx = Pptx(deck);
        var presentation = Entry(pptx, "ppt/presentation.xml");
        Assert.Contains("lang=\"it-IT\"", presentation);
        Assert.Contains("altLang=\"zh-CN\"", presentation);
        Assert.Contains("typeface=\"Noto Sans CJK SC\"", presentation);
        var fromPptx = ReadPptx(pptx);
        Assert.Equal(new TextLanguages("it-IT", "zh-CN"), fromPptx.DefaultLanguages);
        Assert.Equal(deck.DefaultScriptFonts, fromPptx.DefaultScriptFonts);
        Assert.Contains(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code == "PPTX038");
        Assert.DoesNotContain(new PptxWriter().CheckFidelity(Deck()).Losses, loss => loss.Code == "PPTX038");
    }

    [Fact]
    public void Il_testo_CJK_conserva_font_e_lingua_asiatici_nei_due_formati()
    {
        var run = new TextRun("你好世界") { Font = "Arial", AsianFont = "SimSun", Language = "en-US", AsianLanguage = "zh-CN" };
        var deck = Deck(new TextParagraph(run.Text).WithRuns([run, new TextRun(" ok") { Font = "Arial" }]));

        var odp = Odp(deck);
        var content = Entry(odp, "content.xml");
        Assert.Contains("style:font-name-asian=\"SimSun\"", content);
        Assert.Contains("style:language-asian=\"zh\"", content);
        Assert.Contains("style:name=\"SimSun\"", content);
        var odpRun = Paragraphs(ReadOdp(odp))[0].Runs[0];
        Assert.Equal(("SimSun", "en-US", "zh-CN"), (odpRun.AsianFont, odpRun.Language, odpRun.AsianLanguage));

        var pptx = Pptx(deck);
        var slide = Entry(pptx, "ppt/slides/slide1.xml");
        Assert.Contains("<a:ea typeface=\"SimSun\" />", slide.Replace("\"/>", "\" />"));
        Assert.Contains("lang=\"zh-CN\" altLang=\"en-US\"", slide);
        var pptxRun = Paragraphs(ReadPptx(pptx))[0].Runs[0];
        Assert.Equal(("SimSun", "en-US", "zh-CN"), (pptxRun.AsianFont, pptxRun.Language, pptxRun.AsianLanguage));
    }

    [Fact]
    public void Il_testo_CJK_senza_font_asiatico_eredita_quello_predefinito_del_file_ODP()
    {
        var deck = ReadOdp(Odp(Deck()));
        Assert.True(deck.DefaultScriptFonts.IsEmpty);

        var source = Deck(new TextParagraph("日本語").WithRuns([new TextRun("日本語") { Font = "Arial" }, new TextRun("abc")]));
        source.DefaultScriptFonts = new ScriptFonts("MS Gothic");
        source.DefaultLanguages = new TextLanguages("en-US", "ja-JP");
        var pptx = ReadPptx(Pptx(ReadOdp(Odp(source))));
        Assert.Equal("MS Gothic", pptx.DefaultScriptFonts.Asian);
        Assert.Equal("ja-JP", pptx.DefaultLanguages.Asian);
        Assert.Null(Paragraphs(pptx)[0].Runs[0].AsianFont);
    }

    [Fact]
    public void I_punti_elenco_simbolici_conservano_font_e_charset()
    {
        var symbol = new BulletSymbol("") { Font = "Wingdings", SymbolCharset = true };
        var deck = Deck(new TextParagraph("uno") { Bullet = true, BulletSymbol = symbol });

        var odp = Odp(deck);
        Assert.Contains("style:font-charset=\"x-symbol\"", Entry(odp, "content.xml"));
        var fromOdp = Paragraphs(ReadOdp(odp))[0].BulletSymbol!;
        Assert.Equal(("\uF0D8", "Wingdings", true), (fromOdp.Character, fromOdp.Font, fromOdp.SymbolCharset));

        var pptx = Pptx(deck);
        Assert.Contains("charset=\"2\"", Entry(pptx, "ppt/slides/slide1.xml"));
        var read = Paragraphs(ReadPptx(pptx))[0].BulletSymbol!;
        Assert.Equal(("", "Wingdings", true), (read.Character, read.Font, read.SymbolCharset));
    }

    [Fact]
    public void Un_font_simbolico_senza_charset_si_riconosce_dal_nome()
    {
        var deck = Deck(new TextParagraph("uno") { Bullet = true, BulletSymbol = new BulletSymbol("") { Font = "Symbol" } });
        var read = Paragraphs(ReadOdp(Odp(deck)))[0].BulletSymbol!;
        Assert.True(read.SymbolCharset);
    }
}
