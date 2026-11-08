using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideGroupEditingTests
{
    private readonly ISlideGroupEditing _editing = new SlideGroupEditing();

    private static ObjectElement Box(string id, int x, int y, int w, int h) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(x), new Twip(y), new Twip(w), new Twip(h)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };

    private Slide Slide(params SlideElement[] elements)
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        foreach (var element in elements) slide.Add(element);
        return slide;
    }

    [Fact]
    public void Il_riquadro_del_gruppo_e_l_unione_dei_membri()
    {
        var slide = Slide(Box("a", 0, 0, 1000, 1000), Box("b", 3000, 2000, 1000, 1000));
        slide.ReplaceGroups(_editing.Group(slide, [.. slide.Elements], null, "g")!);

        var frame = _editing.Frame(slide, slide.Groups[0])!;

        Assert.Equal(new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(3000)), frame.Bounds);
        Assert.Equal(0, frame.RotationDegrees);
    }

    [Fact]
    public void Ridimensionare_il_gruppo_scala_posizioni_e_misure_dei_membri()
    {
        var slide = Slide(Box("a", 0, 0, 1000, 1000), Box("b", 3000, 2000, 1000, 1000));
        slide.ReplaceGroups(_editing.Group(slide, [.. slide.Elements], null, "g")!);

        var result = _editing.Transform(slide, slide.Groups[0], new DocumentRect(new Twip(1000), new Twip(0), new Twip(8000), new Twip(3000)), 0)!;

        var b = result.Elements.Single(element => element.Id == "b");
        Assert.Equal(new DocumentRect(new Twip(7000), new Twip(2000), new Twip(2000), new Twip(1000)), b.Bounds);
        var a = result.Elements.Single(element => element.Id == "a");
        Assert.Equal(new DocumentRect(new Twip(1000), new Twip(0), new Twip(2000), new Twip(1000)), a.Bounds);
    }

    [Fact]
    public void Ruotare_il_gruppo_gira_i_membri_attorno_al_centro_e_conserva_il_riquadro()
    {
        var slide = Slide(Box("a", 0, 0, 1000, 1000), Box("b", 3000, 0, 1000, 1000));
        slide.ReplaceGroups(_editing.Group(slide, [.. slide.Elements], null, "g")!);
        var before = _editing.Frame(slide, slide.Groups[0])!;

        var result = _editing.Transform(slide, slide.Groups[0], before.Bounds, 90)!;
        foreach (var element in result.Elements) slide.Replace(element);
        slide.ReplaceGroups(result.Groups);

        Assert.Equal(90, slide.Groups[0].RotationDegrees);
        Assert.All(slide.Elements, element => Assert.Equal(90, element.RotationDegrees));
        var a = slide.Find("a")!;
        Assert.Equal(1500, a.Bounds.Left.Value);
        Assert.Equal(-1500, a.Bounds.Top.Value);
        var after = _editing.Frame(slide, slide.Groups[0])!;
        Assert.Equal(before.Bounds, after.Bounds);
        Assert.Equal(90, after.RotationDegrees);
    }

    [Fact]
    public void Raggruppare_dentro_un_gruppo_aperto_sostituisce_i_membri_nel_genitore()
    {
        var slide = Slide(Box("a", 0, 0, 100, 100), Box("b", 200, 0, 100, 100), Box("c", 400, 0, 100, 100));
        slide.AddGroup(new SlideGroup("outer", ["a", "b", "c"]));

        var groups = _editing.Group(slide, [slide.Find("a")!, slide.Find("b")!], "outer", "inner")!;

        Assert.Equal(["inner", "c"], groups.Single(group => group.Id == "outer").Members);
        Assert.Equal(["a", "b"], groups.Single(group => group.Id == "inner").Members);
        slide.ReplaceGroups(groups);
        var dissolved = _editing.Dissolve(slide, slide.Groups.Single(group => group.Id == "inner"));
        Assert.Equal(["a", "b", "c"], Assert.Single(dissolved).Members);
    }

    [Fact]
    public void Un_solo_gruppo_non_si_raggruppa_con_se_stesso()
    {
        var slide = Slide(Box("a", 0, 0, 100, 100), Box("b", 200, 0, 100, 100));
        slide.AddGroup(new SlideGroup("g", ["a", "b"]));

        Assert.Null(_editing.Group(slide, [.. slide.Elements], null, "h"));
    }

    [Fact]
    public void I_gruppi_contenuti_si_rinominano_con_i_membri_clonati()
    {
        var groups = new[] { new SlideGroup("inner", ["a", "b"]), new SlideGroup("outer", ["inner", "c"]), new SlideGroup("other", ["a", "z"]) };

        var within = _editing.Within(groups, ["a", "b", "c"]);
        var counter = 0;
        var renamed = _editing.Renamed(within, new Dictionary<string, string> { ["a"] = "a2", ["b"] = "b2", ["c"] = "c2" }, () => $"n{counter++}");

        Assert.Equal(2, renamed.Count);
        Assert.Equal(["a2", "b2"], renamed[0].Members);
        Assert.Equal([renamed[0].Id, "c2"], renamed[1].Members);
        Assert.Equal("outer", Assert.Single(_editing.Outermost(within)).Id);
    }
}
