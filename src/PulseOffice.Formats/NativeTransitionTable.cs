using System.Xml.Linq;

namespace PulseOffice.Formats;

public sealed record NativeTransitionPair(string Ooxml, string Smil);

public interface INativeTransitionTable
{
    string? SmilFor(string ooxml);

    string? OoxmlFor(string smil);

    string Key(string ooxml);

    bool Knows(string ooxml);
}

public sealed class NativeTransitionTable : INativeTransitionTable
{
    private readonly IReadOnlyList<NativeTransitionPair> _pairs =
    [
        new("<p:blinds xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"horz\"/>", "blindsWipe horizontal"),
        new("<p:blinds xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"vert\"/>", "blindsWipe vertical"),
        new("<p:checker xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"horz\"/>", "checkerBoardWipe across"),
        new("<p:checker xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"vert\"/>", "checkerBoardWipe down"),
        new("<p:comb xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"horz\"/>", "pushWipe combHorizontal"),
        new("<p:comb xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"vert\"/>", "pushWipe combVertical"),
        new("<p:wheel xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" spokes=\"4\"/>", "pinWheelWipe fourBlade"),
        new("<p:wheel xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" spokes=\"1\"/>", "pinWheelWipe oneBlade"),
        new("<p:wheel xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" spokes=\"2\"/>", "pinWheelWipe twoBladeVertical"),
        new("<p:wheel xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" spokes=\"3\"/>", "pinWheelWipe threeBlade"),
        new("<p:wheel xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" spokes=\"8\"/>", "pinWheelWipe eightBlade"),
        new("<p:randomBar xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"horz\"/>", "randomBarWipe horizontal"),
        new("<p:randomBar xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"vert\"/>", "randomBarWipe vertical"),
        new("<p:circle xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "ellipseWipe circle"),
        new("<p:circle xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "ellipseWipe horizontal"),
        new("<p:circle xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "ellipseWipe vertical"),
        new("<p:diamond xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "irisWipe diamond"),
        new("<p:newsflash xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "zoom rotateIn"),
        new("<p:plus xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "fourBoxWipe cornersOut"),
        new("<p:random xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "random default"),
        new("<p:wedge xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "fanWipe centerTop"),
        new("<p:cut xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" thruBlk=\"1\"/>", "barWipe fadeOverColor"),
        new("<p:split xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" orient=\"horz\" dir=\"out\"/>", "barnDoorWipe horizontal"),
        new("<p:split xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" orient=\"horz\" dir=\"in\"/>", "barnDoorWipe horizontal reverse"),
        new("<p:split xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" orient=\"vert\" dir=\"out\"/>", "barnDoorWipe vertical"),
        new("<p:split xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" orient=\"vert\" dir=\"in\"/>", "barnDoorWipe vertical reverse"),
        new("<p:cover xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"lu\"/>", "slideWipe fromBottomRight"),
        new("<p:cover xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"ru\"/>", "slideWipe fromBottomLeft"),
        new("<p:cover xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"ld\"/>", "slideWipe fromTopRight"),
        new("<p:cover xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"rd\"/>", "slideWipe fromTopLeft"),
        new("<p:pull xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"lu\"/>", "slideWipe fromBottomRight reverse"),
        new("<p:pull xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"ru\"/>", "slideWipe fromBottomLeft reverse"),
        new("<p:pull xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"ld\"/>", "slideWipe fromTopRight reverse"),
        new("<p:pull xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" dir=\"rd\"/>", "slideWipe fromTopLeft reverse"),
        new("<p:split xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>", "barnDoorWipe horizontal"),
        new("<p14:vortex xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\"/>", "miscShapeWipe vertical"),
        new("<p14:ripple xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\"/>", "miscShapeWipe horizontal"),
        new("<p14:glitter xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\"/>", "miscShapeWipe diamond"),
        new("<p14:honeycomb xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\"/>", "miscShapeWipe heart"),
        new("<p14:prism xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\"/>", "miscShapeWipe cornersOut"),
        new("<p14:prism xmlns:p14=\"http://schemas.microsoft.com/office/powerpoint/2010/main\" isInverted=\"1\"/>", "miscShapeWipe cornersIn"),
    ];

    public string? SmilFor(string ooxml)
    {
        var key = Key(ooxml);
        var name = XElement.Parse(ooxml).Name;
        return (_pairs.FirstOrDefault(pair => Key(pair.Ooxml) == key)
            ?? _pairs.FirstOrDefault(pair => XElement.Parse(pair.Ooxml).Name == name))?.Smil;
    }

    public bool Knows(string ooxml)
    {
        var key = Key(ooxml);
        return _pairs.Any(pair => Key(pair.Ooxml) == key);
    }

    public string? OoxmlFor(string smil) =>
        _pairs.FirstOrDefault(pair => pair.Smil == smil)?.Ooxml;

    public string Key(string ooxml)
    {
        var element = XElement.Parse(ooxml);
        return string.Join(' ', new[] { element.Name.ToString() }.Concat(element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration)
            .Select(attribute => $"{attribute.Name}={attribute.Value}")
            .Order(StringComparer.Ordinal)));
    }
}
