using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class StyleTests
{
    private readonly Fixture _fixture = new();

    private StyleSheet Sheet => _fixture.Document.Styles;

    [Fact]
    public void IlTitolo2EreditaDaTitoloEDaStandard()
    {
        var resolved = _fixture.Resolver.ResolveParagraphStyle(Sheet, DefaultStyleNames.HeadingLevel(2));

        Assert.Equal("Liberation Sans", resolved.Character.Font);
        Assert.Equal(16, resolved.Character.FontSize);
        Assert.True(resolved.Character.Bold);
        Assert.False(resolved.Character.Italic);
        Assert.Equal(10, resolved.Paragraph.SpaceBefore);
        Assert.True(resolved.Paragraph.KeepWithNext);
        Assert.Equal(2, resolved.Paragraph.Widows);
        Assert.Equal(2, resolved.OutlineLevel);
    }

    [Fact]
    public void UnCollegamentoSenzaStilePrendeLoStileCollegamentoInternetComeWriter()
    {
        var paragraph = new TextParagraph(string.Empty);

        var linked = _fixture.Resolver.EffectiveRun(Sheet, paragraph, new TextRun("Sito") { Hyperlink = "https://esempio.it" });
        var styled = _fixture.Resolver.EffectiveRun(Sheet, paragraph, new TextRun("Sito") { Hyperlink = "https://esempio.it", StyleName = DefaultStyleNames.Emphasis });
        var plain = _fixture.Resolver.EffectiveRun(Sheet, paragraph, new TextRun("Sito"));

        Assert.True(linked.Underline);
        Assert.Equal(0xFF000080u, linked.Color);
        Assert.Null(linked.StyleName);
        Assert.False(styled.Underline);
        Assert.False(plain.Underline);
        Assert.Null(plain.Color);
    }

    [Fact]
    public void UnoStileSconosciutoRicadeSuiPredefiniti()
    {
        var resolved = _fixture.Resolver.ResolveParagraphStyle(Sheet, "Missing");

        Assert.Equal(Sheet.DefaultCharacter, resolved.Character);
        Assert.Equal(Sheet.DefaultParagraph, resolved.Paragraph);
    }

    [Fact]
    public void UnCicloFraGenitoriNonBloccaLaRisoluzione()
    {
        Sheet.Paragraph.Add(new ParagraphStyle("A") { ParentName = "B", Character = new() { Italic = true } });
        Sheet.Paragraph.Add(new ParagraphStyle("B") { ParentName = "A", Character = new() { FontSize = 30 } });

        var resolved = _fixture.Resolver.ResolveParagraphStyle(Sheet, "A");

        Assert.True(resolved.Character.Italic);
        Assert.Equal(30, resolved.Character.FontSize);
    }

    [Fact]
    public void IlCambioDelGenitoreSiPropagaAiFigli()
    {
        Sheet.Paragraph.Find(DefaultStyleNames.Heading)!.Character = new() { Font = "Georgia", FontSize = 14 };

        Assert.Equal("Georgia", _fixture.Resolver.ResolveParagraphStyle(Sheet, DefaultStyleNames.HeadingLevel(5)).Character.Font);
    }

    [Fact]
    public void LaPorzioneSpegneIlGrassettoDelloStile()
    {
        var paragraph = new TextParagraph("Titolo") { StyleName = DefaultStyleNames.HeadingLevel(1) };
        var plain = new TextRun("Tito");
        var off = new DirectFormatting().Set(new TextRun("lo"), new CharacterProperties { Bold = false });

        Assert.True(_fixture.Resolver.EffectiveRun(Sheet, paragraph, plain).Bold);
        Assert.False(_fixture.Resolver.EffectiveRun(Sheet, paragraph, off).Bold);
    }

    [Fact]
    public void LoStileDiCarattereStaFraParagrafoEFormattazioneDiretta()
    {
        var paragraph = new TextParagraph("x") { StyleName = DefaultStyleNames.TextBody };
        var styled = new TextRun("x") { StyleName = DefaultStyleNames.InternetLink };
        var overridden = new DirectFormatting().Set(styled, new CharacterProperties { Color = 0xFFFF0000 });

        var link = _fixture.Resolver.EffectiveRun(Sheet, paragraph, styled);
        var red = _fixture.Resolver.EffectiveRun(Sheet, paragraph, overridden);

        Assert.Equal(0xFF000080u, link.Color);
        Assert.True(link.Underline);
        Assert.Equal("Liberation Serif", link.Font);
        Assert.Equal(0xFFFF0000u, red.Color);
        Assert.True(red.Underline);
    }

    [Fact]
    public void LAllineamentoNonDichiaratoNonCopreQuelloDelloStile()
    {
        var implicitLeft = new TextParagraph("x") { StyleName = DefaultStyleNames.Title };
        var explicitLeft = new DirectFormatting().Set(implicitLeft, new ParagraphProperties { Align = TextAlign.Right });

        Assert.Equal(TextAlign.Center, _fixture.Resolver.EffectiveParagraph(Sheet, implicitLeft).Align);
        Assert.Equal(TextAlign.Right, _fixture.Resolver.EffectiveParagraph(Sheet, explicitLeft).Align);
        Assert.Equal(28, _fixture.Resolver.EffectiveParagraph(Sheet, implicitLeft).FontSize);
    }

    [Fact]
    public void GliStiliPredefinitiSonoQuelliDiWriter()
    {
        string[] paragraph = [DefaultStyleNames.Standard, DefaultStyleNames.TextBody, DefaultStyleNames.List, DefaultStyleNames.Caption,
            DefaultStyleNames.Index, DefaultStyleNames.Title, DefaultStyleNames.Footnote, DefaultStyleNames.Header, "Heading 1", "Heading 10"];
        Assert.All(paragraph, name => Assert.True(Sheet.Paragraph.Contains(name), name));
        Assert.Equal(DefaultStyleNames.TextBody, Sheet.Paragraph.Find("Heading 1")!.NextStyleName);
        Assert.True(Sheet.Character.Contains(DefaultStyleNames.Emphasis));
        Assert.Equal(ListStyle.LevelCount, Sheet.List.Find(DefaultStyleNames.Numbering123)!.Levels.Count);
        Assert.Equal(Twip.FromMillimeters(210), Sheet.Page.Find(DefaultStyleNames.Standard)!.Width);
        Assert.Equal(Twip.FromMillimeters(297), Sheet.Page.Find(DefaultStyleNames.Landscape)!.Width);
        Assert.Equal(Objects.WrapMode.None, _fixture.Resolver.ResolveFrameStyle(Sheet, DefaultStyleNames.Ole).Wrap);
        Assert.Equal(0.5, _fixture.Resolver.ResolveTableStyle(Sheet, DefaultStyleNames.DefaultTableStyle).BorderWidth);
    }

    [Fact]
    public void DueStiliConLoStessoNomeSonoRifiutati()
    {
        Assert.Throws<InvalidOperationException>(() => Sheet.Paragraph.Add(new ParagraphStyle(DefaultStyleNames.Standard)));
    }

    [Fact]
    public void IlCloneDelFoglioDiStiliEIndipendente()
    {
        var copy = Sheet.Clone();
        copy.Paragraph.Find(DefaultStyleNames.Heading)!.Character = new() { Font = "Other" };

        Assert.Equal("Liberation Sans", Sheet.Paragraph.Find(DefaultStyleNames.Heading)!.Character.Font);
    }
}
