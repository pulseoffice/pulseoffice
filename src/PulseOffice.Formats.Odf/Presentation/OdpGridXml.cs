using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpGridXml
{
    private readonly XNamespace _pulse = "urn:pulseoffice:presentation-grid";

    private readonly (string Name, Func<GridSettings, bool> Get, Action<GridSettings, bool> Set)[] _flags =
    [
        ("show-grid", grid => grid.ShowGrid, (grid, value) => grid.ShowGrid = value),
        ("snap-to-grid", grid => grid.SnapToGrid, (grid, value) => grid.SnapToGrid = value),
        ("show-rulers", grid => grid.ShowRulers, (grid, value) => grid.ShowRulers = value),
        ("show-guides", grid => grid.ShowGuides, (grid, value) => grid.ShowGuides = value),
        ("snap-to-guides", grid => grid.SnapToGuides, (grid, value) => grid.SnapToGuides = value),
        ("grid-in-front", grid => grid.GridInFront, (grid, value) => grid.GridInFront = value),
        ("guides-in-front", grid => grid.GuidesInFront, (grid, value) => grid.GuidesInFront = value),
        ("helplines-while-moving", grid => grid.HelplinesWhileMoving, (grid, value) => grid.HelplinesWhileMoving = value),
        ("snap-to-object-frames", grid => grid.SnapToObjectFrames, (grid, value) => grid.SnapToObjectFrames = value),
        ("snap-to-object-points", grid => grid.SnapToObjectPoints, (grid, value) => grid.SnapToObjectPoints = value),
        ("snap-to-page-margins", grid => grid.SnapToPageMargins, (grid, value) => grid.SnapToPageMargins = value),
    ];

    public XElement? Element(GridSettings grid)
    {
        var standard = new GridSettings();
        var element = new XElement(_pulse + "grid", new XAttribute(XNamespace.Xmlns + "pulsegrid", _pulse.NamespaceName));
        foreach (var flag in _flags.Where(flag => flag.Get(grid) != flag.Get(standard)))
            element.Add(new XAttribute(flag.Name, flag.Get(grid) ? "true" : "false"));
        if (grid.Spacing != standard.Spacing)
            element.Add(new XAttribute("spacing", grid.Spacing.Value.ToString(CultureInfo.InvariantCulture)));
        foreach (var guide in grid.Guides)
            element.Add(new XElement(_pulse + "guide",
                new XAttribute("orientation", guide.Horizontal ? "horizontal" : "vertical"),
                new XAttribute("position", guide.Position.ToString(CultureInfo.InvariantCulture))));
        return element.Attributes().Count() > 1 || element.HasElements ? element : null;
    }

    public void Read(XElement? container, GridSettings grid)
    {
        if (container?.Element(_pulse + "grid") is not { } element) return;
        foreach (var flag in _flags)
            if ((string?)element.Attribute(flag.Name) is { } value)
                flag.Set(grid, value == "true");
        if (int.TryParse((string?)element.Attribute("spacing"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var spacing) && spacing > 0)
            grid.Spacing = new Twip(spacing);
        foreach (var guide in element.Elements(_pulse + "guide"))
            if (int.TryParse((string?)guide.Attribute("position"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
                grid.AddGuide(new SlideGuide((string?)guide.Attribute("orientation") == "horizontal", position));
    }
}
