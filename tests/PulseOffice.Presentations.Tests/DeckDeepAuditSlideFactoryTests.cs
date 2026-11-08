using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckDeepAuditSlideFactoryTests
{
    private readonly ISlideFactory _factory = new SlideFactory();
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);

    [Fact]
    public void CambiareLayoutNonDuplicaIlPrimoCorpoInTutteLeCaselle()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.TwoContent);
        var bodies = slide.Elements.OfType<TextElement>().Where(t => t.Text.Role == PlaceholderRole.Body).ToList();
        slide.Replace(bodies[0] with { Text = bodies[0].Text.WithPlainText("Sinistra") });
        slide.Replace(bodies[1] with { Text = bodies[1].Text.WithPlainText("Destra") });

        var relaid = _factory.Relayout(slide, _master, SlideLayout.FourContent);

        Assert.Equal(relaid.Elements.Count, relaid.Elements.Select(e => e.Id).Distinct().Count());
        var texts = relaid.Elements.OfType<TextElement>().Where(t => t.Text.Role == PlaceholderRole.Body).Select(t => t.Text.PlainText).ToList();
        Assert.Equal(1, texts.Count(t => t == "Sinistra"));
        Assert.Equal(1, texts.Count(t => t == "Destra"));
        Assert.Equal(4, texts.Count);
    }

    [Fact]
    public void DuplicareUnaDiapositivaConservaIGruppi()
    {
        var slide = SlideWithGroup();

        var copy = _factory.Clone(slide, "s2");

        var group = Assert.Single(copy.Groups);
        Assert.Equal(["s2.a", "s2.b"], group.Members);
        Assert.All(group.Members, member => Assert.NotNull(copy.Find(member)));
        Assert.Equal("Gruppo", group.Name);
    }

    [Fact]
    public void CambiareLayoutConservaIGruppi()
    {
        var slide = SlideWithGroup();

        var relaid = _factory.Relayout(slide, _master, SlideLayout.TitleOnly);

        var group = Assert.Single(relaid.Groups);
        Assert.Equal(["s1.a", "s1.b"], group.Members);
    }

    private Slide SlideWithGroup()
    {
        var slide = _factory.Create("s1", _master, SlideLayout.Blank);
        slide.Add(Shape("s1.a", 0));
        slide.Add(Shape("s1.b", 2000));
        slide.AddGroup(new SlideGroup("s1.g", ["s1.a", "s1.b"]) { Name = "Gruppo" });
        return slide;
    }

    private ObjectElement Shape(string id, int left) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(left), new Twip(0), new Twip(1000), new Twip(1000)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };
}
