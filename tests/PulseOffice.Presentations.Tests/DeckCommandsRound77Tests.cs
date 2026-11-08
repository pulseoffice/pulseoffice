using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckCommandsRound77Tests
{
    private readonly ISlideFactory _factory = new SlideFactory();

    [Fact]
    public void Duplicating_keeps_element_ids_unique_when_a_bare_id_meets_a_prefixed_one()
    {
        var source = new Slide("s1", SlideLayout.Blank);
        source.Add(Shape("x"));
        source.Add(Shape("s1.x"));
        source.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "x" });
        source.AddAnimation(new AnimationEffect { Id = "a2", TargetId = "s1.x" });

        var copy = _factory.Clone(source, "s9");

        var ids = copy.Elements.Select(element => element.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, copy.Animations.Select(animation => animation.TargetId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(copy.Animations, animation => Assert.Contains(animation.TargetId, ids));
    }

    private static ObjectElement Shape(string id) => new()
    {
        Id = id,
        Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
    };
}
