using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckCoreRound15Tests
{
    private readonly ISlideFactory _factory = new SlideFactory();

    [Fact]
    public void Cambiare_il_layout_conserva_il_diagramma_nativo()
    {
        var source = WithDiagram();
        var master = new SlideMaster { Id = "m", Name = "m", Theme = new Theme { Name = "t" } };

        var relaid = _factory.Relayout(source, master, SlideLayout.TitleOnly);

        Assert.Single(relaid.IntactDiagrams());
    }

    [Fact]
    public void Duplicare_una_diapositiva_conserva_il_diagramma_nativo()
    {
        var copy = _factory.Clone(WithDiagram(), "slide2");

        var diagram = Assert.Single(copy.IntactDiagrams());
        Assert.All(diagram.Members, member => Assert.StartsWith("slide2.", member.Id, StringComparison.Ordinal));
        Assert.Equal(["slide2.a"], diagram.PointsByMember.Keys);
    }

    [Fact]
    public void Duplicare_una_diapositiva_con_identificativi_ripetuti_dal_file_non_fallisce()
    {
        var source = new Slide("slide1", SlideLayout.Blank);
        source.Add(Shape("clip"));
        source.Add(Shape("clip"));

        var copy = _factory.Clone(source, "slide2");

        Assert.Equal(2, copy.Elements.Count);
    }

    private static Slide WithDiagram()
    {
        var source = new Slide("slide1", SlideLayout.Blank);
        var a = Shape("slide1.a");
        var b = Shape("slide1.b");
        source.Add(a);
        source.Add(b);
        source.AddDiagram(new NativeDiagram([a, b], "<frame/>", [])
        {
            PointsByMember = new Dictionary<string, string> { ["slide1.a"] = "p1" },
        });
        return source;
    }

    private static ObjectElement Shape(string id) => new()
    {
        Id = id,
        Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
    };
}
