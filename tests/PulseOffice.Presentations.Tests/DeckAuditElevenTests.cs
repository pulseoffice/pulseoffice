using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckAuditElevenTests
{
    private readonly ISlideFactory _factory = new SlideFactory();
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);

    [Fact]
    public void LIntervalloScorreQuandoSiEliminaUnaDiapositivaPrima()
    {
        var setup = new ShowSetup { FromSlide = 3, ToSlide = 5 };
        Assert.Equal((2, 4), (setup.WithoutSlide(1, 5).FromSlide, setup.WithoutSlide(1, 5).ToSlide));
        Assert.Equal((3, 4), (setup.WithoutSlide(4, 5).FromSlide, setup.WithoutSlide(4, 5).ToSlide));
        Assert.Equal((3, 5), (setup.WithoutSlide(6, 5).FromSlide, setup.WithoutSlide(6, 5).ToSlide));
    }

    [Fact]
    public void UnIntervalloDiUnaSolaDiapositivaEliminataTornaTutte()
    {
        var setup = new ShowSetup { FromSlide = 2, ToSlide = 2 };
        Assert.False(setup.WithoutSlide(2, 3).HasRange);
    }

    [Fact]
    public void AnimazioniEInterazioniSeguonoLoggettoTolto()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.Blank);
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "x" });
        slide.AddAnimation(new AnimationEffect { Id = "a2", TargetId = "y" });
        slide.AddInteraction(new InteractiveAction { TargetId = "x", Kind = InteractionKind.NextSlide });

        var behaviours = new ElementBehaviours(slide, ["x"]);
        behaviours.Detach();
        Assert.Equal("a2", Assert.Single(slide.Animations).Id);
        Assert.Empty(slide.Interactions);

        behaviours.Attach();
        Assert.Equal(["a1", "a2"], slide.Animations.Select(a => a.Id));
        Assert.Single(slide.Interactions);
    }

    [Fact]
    public void CambiareLayoutConservaLeCaselleDiTestoLibere()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TitleAndContent);
        var box = new TextElement
        {
            Id = "s1.box",
            Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(1000), new Twip(500)),
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph("Nota")]),
        };
        slide.Add(box);
        var body = slide.Placeholder(PlaceholderRole.Body)!;
        slide.Replace(body with { Text = body.Text.WithPlainText("Corpo") });

        var relaid = _factory.Relayout(slide, _master, SlideLayout.TitleOnly);

        Assert.NotNull(relaid.Find("s1.box"));
        Assert.Contains(relaid.Elements.OfType<TextElement>(), t => t.Text.PlainText == "Corpo");
    }
}
