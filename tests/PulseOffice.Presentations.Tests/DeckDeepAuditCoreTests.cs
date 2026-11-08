using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckDeepAuditCoreTests
{
    private readonly IAnimationScheduler _scheduler = new AnimationScheduler();

    private static AnimationEffect Effect(string id, AnimationTrigger trigger, int duration, int delay, int repeat = 1) =>
        new() { Id = id, TargetId = id, Trigger = trigger, DurationMs = duration, DelayMs = delay, Repeat = repeat };

    [Fact]
    public void Ritardi_e_durate_enormi_dal_file_non_fanno_traboccare_la_scaletta()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, int.MaxValue, int.MaxValue, 100),
            Effect("b", AnimationTrigger.AfterPrevious, int.MaxValue, int.MaxValue),
            Effect("c", AnimationTrigger.OnClick, 500, 0),
        ]);

        Assert.All(schedule, entry =>
        {
            Assert.True(entry.StartMs >= 0);
            Assert.True(entry.EndMs > entry.StartMs || entry.EndMs == int.MaxValue);
        });
        Assert.True(schedule[1].StartMs >= schedule[0].StartMs);
        Assert.True(schedule[2].StartMs >= schedule[1].StartMs);
        Assert.True(_scheduler.Duration(schedule) >= 0);
    }

    [Fact]
    public void Duplicare_una_diapositiva_conserva_i_suoi_gruppi()
    {
        var source = new Slide("slide1", SlideLayout.Blank);
        source.Add(Shape("slide1.a"));
        source.Add(Shape("slide1.b"));
        source.Add(Shape("slide1.c"));
        source.AddGroup(new SlideGroup("slide1.g1", ["slide1.a", "slide1.b"]) { Name = "Inner", RotationDegrees = 15 });
        source.AddGroup(new SlideGroup("slide1.g2", ["slide1.g1", "slide1.c"]));

        var copy = new SlideFactory().Clone(source, "slide2");

        Assert.Equal(2, copy.Groups.Count);
        var inner = Assert.Single(copy.Groups, group => group.Name == "Inner");
        Assert.Equal(15, inner.RotationDegrees);
        Assert.All(inner.Members, member => Assert.NotNull(copy.Find(member)));
        var outer = Assert.Single(copy.Groups, group => group.Name is null);
        Assert.Contains(inner.Id, outer.Members);
        Assert.DoesNotContain(source.Groups, group => group.Id == inner.Id || group.Id == outer.Id);
    }

    [Fact]
    public void Cambiare_il_layout_conserva_i_gruppi()
    {
        var source = new Slide("slide1", SlideLayout.Blank);
        source.Add(Shape("slide1.a"));
        source.Add(Shape("slide1.b"));
        source.AddGroup(new SlideGroup("slide1.g1", ["slide1.a", "slide1.b"]));
        var master = new SlideMaster { Id = "m", Name = "m", Theme = new Theme { Name = "t" } };

        var relaid = new SlideFactory().Relayout(source, master, SlideLayout.TitleOnly);

        var group = Assert.Single(relaid.Groups);
        Assert.Equal(["slide1.a", "slide1.b"], group.Members);
    }

    private static ObjectElement Shape(string id) => new()
    {
        Id = id,
        Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = id, Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
    };
}
