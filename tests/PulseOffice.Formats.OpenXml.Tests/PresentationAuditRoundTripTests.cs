using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PresentationAuditRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private (Presentation Deck, Slide Slide) Sample()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        deck.AddExisting(slide);
        return (deck, slide);
    }

    private Presentation Through(string format, Presentation deck)
    {
        using var buffer = new MemoryStream();
        if (format == "odp") new OdpWriter().Write(deck, buffer);
        else new PptxWriter().Write(deck, buffer);
        buffer.Position = 0;
        var result = format == "odp" ? new OdpReader(_themes, _masters).Read(buffer) : new PptxReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Theory]
    [InlineData("odp")]
    [InlineData("pptx")]
    public void UnaFormaSottoIlTitoloRestaSotto(string format)
    {
        var (deck, slide) = Sample();
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(title with { Text = title.Text.WithPlainText("Titolo"), ZOrder = 5 });
        slide.Add(new ObjectElement
        {
            Id = "box", ZOrder = 1,
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(3000), new Twip(2000)),
            Content = new ShapeObject { Id = "box", Shape = ShapeKind.Rectangle },
        });

        var back = Through(format, deck).Slides[0].InZOrder().ToList();

        Assert.True(back.FindIndex(e => e is ObjectElement) < back.FindIndex(e => e is TextElement { Text.Role: PlaceholderRole.Title }));
    }

    [Theory]
    [InlineData("odp")]
    [InlineData("pptx")]
    public void LAnimazioneDiUnaFormaSopravvive(string format)
    {
        var (deck, slide) = Sample();
        slide.Add(new ObjectElement
        {
            Id = "box",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(3000), new Twip(2000)),
            Content = new ShapeObject { Id = "box", Shape = ShapeKind.Ellipse },
        });
        slide.AddAnimation(new AnimationEffect
        {
            Id = "a1", TargetId = "box", Category = AnimationCategory.Entrance,
            Preset = "fade", Trigger = AnimationTrigger.OnClick, DurationMs = 500,
        });

        var back = Through(format, deck).Slides[0];

        var animation = Assert.Single(back.Animations);
        Assert.IsType<ObjectElement>(back.Elements.Single(e => e.Id == animation.TargetId));
    }

    [Theory]
    [InlineData("odp")]
    [InlineData("pptx")]
    public void LoSfondoProprioSopravviveEQuelloDelMasterNonDiventaProprio(string format)
    {
        var (deck, slide) = Sample();
        var plain = _slides.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent);
        deck.AddExisting(plain);
        slide.Background = 0xFFDC2626;

        var back = Through(format, deck);

        Assert.Equal(0xFFDC2626u, back.Slides[0].Background);
        Assert.Null(back.Slides[1].Background);
    }

    [Theory]
    [InlineData("odp")]
    [InlineData("pptx")]
    public void GliSpaziFraLePorzioniSopravvivono(string format)
    {
        var (deck, slide) = Sample();
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        var paragraph = new TextParagraph("Ciao  mondo") { Runs = [new TextRun("Ciao") { Bold = true }, new TextRun(" "), new TextRun(" mondo")] };
        slide.Replace(title with { Text = title.Text with { Paragraphs = [paragraph] } });

        var back = Through(format, deck).Slides[0].Placeholder(PlaceholderRole.Title)!;

        Assert.Equal("Ciao  mondo", back.Text.Paragraphs[0].Text);
    }

    [Fact]
    public void DueDiapositiveConLoStessoNomeRestanoDistinteInOdp()
    {
        var (deck, slide) = Sample();
        var other = _slides.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(other);
        slide.Name = "Uguale";
        other.Name = "Uguale";

        var back = Through("odp", deck);

        Assert.Equal(2, back.Slides.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void ICommentiSuPiuRigheConLaDataSopravvivonoInOdp()
    {
        var (deck, slide) = Sample();
        var when = new DateTimeOffset(2026, 3, 4, 10, 30, 0, TimeSpan.Zero);
        slide.AddComment(new SlideComment { Id = "c1", Author = "Ada", Text = "uno\ndue", CreatedAt = when });

        var comment = Assert.Single(Through("odp", deck).Slides[0].Comments);

        Assert.Equal("uno\ndue", comment.Text);
        Assert.Equal(when, comment.CreatedAt);
    }

    [Fact]
    public void IColoriDeiCollegamentiDelTemaSopravvivonoInPptx()
    {
        var (deck, _) = Sample();
        deck.Theme = deck.Theme with { Colors = deck.Theme.Colors with { Hyperlink = 0xFF123456 } };
        deck.Master = deck.Master with { Theme = deck.Theme };

        Assert.Equal(0xFF123456u, Through("pptx", deck).Theme.Colors.Hyperlink);
    }
}
