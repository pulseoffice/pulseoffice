using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PlaceholderInheritanceTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Read(params string[] path)
    {
        using var stream = File.OpenRead(Path.Combine([AppContext.BaseDirectory, "Fixtures", .. path]));
        var result = new PptxReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return result.Value!;
    }

    public static TheoryData<string> PowerPointFiles() =>
        [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PowerPoint"), "*.pptx").Select(Path.GetFileName).OfType<string>()];

    [Theory]
    [MemberData(nameof(PowerPointFiles))]
    public void NessunSegnapostoConTestoEsceAMisuraZero(string file)
    {
        var deck = Read("PowerPoint", file);
        foreach (var slide in deck.Slides)
        foreach (var element in slide.Elements.OfType<TextElement>())
        {
            if (element.Text.IsEmpty) continue;
            Assert.True(element.Bounds.Width.Value > 0 && element.Bounds.Height.Value > 0,
                $"{file}/{slide.Id}: «{element.Text.PlainText}» a misura zero");
        }
    }

    [Fact]
    public void IlTitoloSenzaGeometriaEreditaPosizioneEStileDalLayoutEDalMaster()
    {
        var deck = Read("boldonse-mtx.pptx");
        var title = Assert.Single(deck.Slides[0].Elements.OfType<TextElement>());

        Assert.Equal(PlaceholderRole.Title, title.Text.Role);
        Assert.True(title.Bounds.Width.Value > 0 && title.Bounds.Height.Value > 0);
        Assert.True(title.Bounds.Left.Value > 0 && title.Bounds.Top.Value > 0);

        var paragraph = Assert.Single(title.Text.Paragraphs);
        Assert.Equal("Test", paragraph.Text);
        Assert.Equal(60, paragraph.FontSize);
        Assert.Equal(TextAlign.Center, paragraph.Align);
        Assert.Equal(ColorRole.Text, paragraph.ColorRole);
        Assert.False(paragraph.Bullet);
        Assert.Equal("Boldonse", paragraph.EffectiveRuns()[0].Font);
    }

    [Fact]
    public void ILivelliDelCorpoEreditanoIlCorpoDalloStileDelMaster()
    {
        var body = Read("PowerPoint", "NumberedList-12ab-ab-34.pptx").Slides[0].Elements.OfType<TextElement>()
            .Single(element => element.Text.Role == PlaceholderRole.Body);

        Assert.All(body.Text.Paragraphs.Where(p => p.BulletLevel == 0), p => Assert.Equal(28, p.FontSize));
        Assert.All(body.Text.Paragraphs.Where(p => p.BulletLevel == 1), p => Assert.Equal(24, p.FontSize));
        Assert.Contains(body.Text.Paragraphs, p => p.Numbered);
    }

    [Fact]
    public void IlSegnapostoPrendeColoreECorpoDalLayoutAbbinatoPerIndice()
    {
        var texts = Read("PowerPoint", "placeholder-priority.pptx").Slides[0].Elements.OfType<TextElement>()
            .OrderBy(element => element.Bounds.Top.Value).ToList();

        Assert.Equal(0xFFFF6A52u, texts[0].Text.Paragraphs[0].Color);
        Assert.Equal(14, texts[0].Text.Paragraphs[0].FontSize);
        Assert.Equal(0xFF00B050u, texts[1].Text.Paragraphs[0].Color);
        Assert.Equal(24, texts[1].Text.Paragraphs[0].FontSize);
    }

    [Fact]
    public void ISegnapostiDeiLayoutSenzaGeometriaLaPrendonoDalMaster() =>
        Assert.All(Read("boldonse-mtx.pptx").Master.Layouts.SelectMany(layout => layout.Placeholders),
            placeholder => Assert.True(placeholder.Bounds.Width.Value > 0 && placeholder.Bounds.Height.Value > 0));

    [Fact]
    public void IlNomeDelTemaSiLegge()
    {
        var deck = Read("boldonse-mtx.pptx");
        Assert.Equal("Office Theme", deck.Theme.Name);
        Assert.Equal("Office Theme", deck.Master.Theme.Name);
        Assert.All(deck.Masters, master => Assert.False(string.IsNullOrEmpty(master.Theme.Name)));
    }
}
