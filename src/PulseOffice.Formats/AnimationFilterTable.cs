using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed record SmilFilter(string Type, string Subtype, bool Forward);

public interface IAnimationFilterTable
{
    string ToOoxml(FilterBehaviour filter);

    SmilFilter FromOoxml(string? filter);
}

public sealed class AnimationFilterTable : IAnimationFilterTable
{
    private sealed record Row(string Ooxml, string Type, string Subtype, bool Forward);

    private readonly IReadOnlyList<Row> _rows =
    [
        new("wipe(up)", "barWipe", "topToBottom", true),
        new("wipe(right)", "barWipe", "leftToRight", false),
        new("wipe(left)", "barWipe", "leftToRight", true),
        new("wipe(down)", "barWipe", "topToBottom", false),
        new("wheel(1)", "pinWheelWipe", "oneBlade", true),
        new("wheel(2)", "pinWheelWipe", "twoBladeVertical", true),
        new("wheel(3)", "pinWheelWipe", "threeBlade", true),
        new("wheel(4)", "pinWheelWipe", "fourBlade", true),
        new("wheel(8)", "pinWheelWipe", "eightBlade", true),
        new("strips(downLeft)", "waterfallWipe", "horizontalRight", true),
        new("strips(upLeft)", "waterfallWipe", "horizontalLeft", false),
        new("strips(downRight)", "waterfallWipe", "horizontalLeft", true),
        new("strips(upRight)", "waterfallWipe", "horizontalRight", false),
        new("barn(inVertical)", "barnDoorWipe", "vertical", false),
        new("barn(outVertical)", "barnDoorWipe", "vertical", true),
        new("barn(inHorizontal)", "barnDoorWipe", "horizontal", false),
        new("barn(outHorizontal)", "barnDoorWipe", "horizontal", true),
        new("randombar(vertical)", "randomBarWipe", "vertical", true),
        new("randombar(horizontal)", "randomBarWipe", "horizontal", true),
        new("checkerboard(down)", "checkerBoardWipe", "down", true),
        new("checkerboard(across)", "checkerBoardWipe", "across", true),
        new("plus(out)", "fourBoxWipe", "cornersIn", false),
        new("plus(in)", "fourBoxWipe", "cornersIn", true),
        new("diamond(out)", "irisWipe", "diamond", true),
        new("diamond(in)", "irisWipe", "diamond", false),
        new("circle(out)", "ellipseWipe", "horizontal", true),
        new("circle(in)", "ellipseWipe", "horizontal", false),
        new("box(out)", "irisWipe", "rectangle", true),
        new("box(in)", "irisWipe", "rectangle", false),
        new("wedge", "fanWipe", "centerTop", true),
        new("blinds(vertical)", "blindsWipe", "vertical", true),
        new("blinds(horizontal)", "blindsWipe", "horizontal", true),
        new("fade", "fade", "crossfade", true),
        new("slide(fromTop)", "slideWipe", "fromTop", true),
        new("slide(fromRight)", "slideWipe", "fromRight", true),
        new("slide(fromLeft)", "slideWipe", "fromLeft", true),
        new("slide(fromBottom)", "slideWipe", "fromBottom", true),
        new("dissolve", "dissolve", "default", true),
        new("image", "dissolve", "default", true),
    ];

    public string ToOoxml(FilterBehaviour filter) =>
        _rows.MaxBy(row => (row.Type == filter.Type ? 4 : 0) + (row.Subtype == filter.Subtype ? 2 : 0) + (row.Forward == filter.Forward ? 1 : 0))!.Ooxml;

    public SmilFilter FromOoxml(string? filter)
    {
        var row = _rows.FirstOrDefault(candidate => candidate.Ooxml == filter) ?? _rows.First(candidate => candidate.Ooxml == "fade");
        return new SmilFilter(row.Type, row.Subtype, row.Forward);
    }
}
