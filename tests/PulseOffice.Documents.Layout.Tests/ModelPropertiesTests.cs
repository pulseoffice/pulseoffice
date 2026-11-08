using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Documents.Layout.Measurement.OpenType;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ModelPropertiesTests
{
    private const string ReferenceFonts = "/Applications/LibreOffice.app/Contents/Resources/fonts/truetype";
    private const string SystemFonts = "/System/Library/Fonts";

    [Fact]
    public void LInterlineaEsattaDelModelloFissaLAltezzaDellaRiga()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.Add("riga", p => p with { LineHeight = ParagraphLineHeight.Exact(30) });

        var line = Assert.Single(f.Lines(f.Layout()), l => l.ParagraphId == block.Id);

        Assert.Equal(30, line.Line.Height, 3);
    }

    [Fact]
    public void LInterlineaAlmenoEDiSpaziaturaArrivanoDalloStile()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        f.Document.Styles.Paragraph.Add(new ParagraphStyle("Alta") { Paragraph = new ParagraphProperties { LineHeight = ParagraphLineHeight.AtLeast(40) } });
        f.Document.Styles.Paragraph.Add(new ParagraphStyle("Spaziata") { Paragraph = new ParagraphProperties { LineHeight = ParagraphLineHeight.Leading(5) } });
        var tall = f.Add("riga", p => p with { StyleName = "Alta" });
        var spaced = f.Add("riga", p => p with { StyleName = "Spaziata" });

        var lines = f.Lines(f.Layout());

        Assert.Equal(40, lines.Single(l => l.ParagraphId == tall.Id).Line.Height, 3);
        Assert.Equal(17, lines.Single(l => l.ParagraphId == spaced.Id).Line.Height, 3);
    }

    [Fact]
    public void LaSillabazioneDelModelloRispettaIMinimi()
    {
        var f = new LayoutFixture(width: 30, height: 400);
        f.Hyphenation.Words["abcdefgh"] = [1, 4];
        var block = f.Add("abcdefgh", p => p with { Hyphenation = ParagraphHyphenation.On with { CharactersBefore = 2, CharactersAfter = 2 } });

        var texts = f.LineTexts(f.Layout(), block.Id);

        Assert.Equal(["abcd-", "efgh"], texts);
    }

    [Fact]
    public void SenzaSillabazioneNelModelloLaParolaNonSiSpezza()
    {
        var f = new LayoutFixture(width: 30, height: 400);
        f.Hyphenation.Words["abcdefgh"] = [4];
        var block = f.Add("abcdefgh", p => p with { Hyphenation = ParagraphHyphenation.Off });

        Assert.DoesNotContain(f.LineTexts(f.Layout(), block.Id), text => text.EndsWith('-'));
    }

    [Fact]
    public void LaSpaziaturaContestualeArrivaDalloStile()
    {
        var f = new LayoutFixture();
        f.Document.Styles.Paragraph.Add(new ParagraphStyle("Elenco") { Paragraph = new ParagraphProperties { ContextualSpacing = true } });
        var block = f.Add("x", p => p with { StyleName = "Elenco" });
        var effective = new StyleResolver().EffectiveParagraph(f.Document.Styles, block.Content);

        Assert.True(new Flow.ModelFlowSettings().ContextualSpacing(block, effective));
        Assert.True(effective.ContextualSpacing);
    }

    [Fact]
    public void RelayoutVedeIlCambioDiUnoStileSenzaInvalidate()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        f.Document.Styles.Paragraph.Add(new ParagraphStyle("Corpo") { Paragraph = new ParagraphProperties { SpaceBefore = 0 } });
        var block = f.Add("riga", p => p with { StyleName = "Corpo" });
        var before = f.Layout();

        f.Document.Styles.Paragraph.Find("Corpo")!.Paragraph = new ParagraphProperties { SpaceBefore = 50 };
        var after = f.Engine.Relayout(f.Document, before);

        Assert.NotSame(before, after);
        Assert.Equal(50, f.Lines(after).Single(l => l.ParagraphId == block.Id).Top, 3);
    }

    [Fact]
    public void LaVersioneDegliStiliCresceAOgniModifica()
    {
        var sheet = new StyleSheet();
        var start = sheet.Version;
        var style = new ParagraphStyle("A");
        sheet.Paragraph.Add(style);
        var added = sheet.Version;
        style.Paragraph = new ParagraphProperties { SpaceAfter = 3 };
        var edited = sheet.Version;
        sheet.Page.Add(new PageStyle("P"));
        sheet.Page.Find("P")!.Header.Enabled = true;

        Assert.True(added > start);
        Assert.True(edited > added);
        Assert.True(sheet.Version > edited + 1);
    }

    [Fact]
    public void LaCrenaturaAccorciaLeCoppie()
    {
        Assert.SkipUnless(Directory.Exists(ReferenceFonts), "Reference fonts are not installed.");
        var locator = new PulseOffice.Typography.OpenType.Metrics.DirectoryFontLocator([ReferenceFonts]);
        var kerned = new OpenTypeMeasurer(locator, new HorizontalHeaderMetrics(), new ExactAdvances(), new FixedPitchMeasurer());
        var plain = new OpenTypeMeasurer(locator, new HorizontalHeaderMetrics(), new ExactAdvances(), new NoKerningApplier(), new FixedPitchMeasurer());
        var font = new FontKey("Liberation Serif", 12, false, false);

        Assert.True(kerned.Advances(font, "AV").Sum() < plain.Advances(font, "AV").Sum());
        Assert.Equal(plain.Advances(font, "A").Sum(), kerned.Advances(font, "A").Sum(), 6);
    }

    [Fact]
    public void LeCollezioniTtcSiLeggono()
    {
        var file = Path.Combine(SystemFonts, "Helvetica.ttc");
        Assert.SkipUnless(File.Exists(file), "Helvetica.ttc is not available.");
        var locator = new PulseOffice.Typography.OpenType.Metrics.DirectoryFontLocator([SystemFonts]);

        var regular = locator.Find("Helvetica", false, false);
        var bold = locator.Find("Helvetica", true, false);

        Assert.NotNull(regular);
        Assert.NotNull(bold);
        Assert.True(bold!.Bold);
        Assert.True(regular!.AdvanceOf('M') > 0);
    }
}
