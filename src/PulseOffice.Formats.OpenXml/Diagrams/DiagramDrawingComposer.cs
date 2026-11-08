using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml.Diagrams;

internal sealed class DiagramStyleSheet(XDocument? colors, XDocument? style)
{
    private static readonly XNamespace Diagram = "http://schemas.openxmlformats.org/drawingml/2006/diagram";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public XElement? Fill(DiagramBox box) => Pick(box, "fillClrLst") ?? (FillReference(box) == 0 ? null : Scheme("accent1"));

    public XElement? Line(DiagramBox box) => Pick(box, "linClrLst") ?? Scheme("lt1");

    public XElement Text(DiagramBox box)
    {
        if (Pick(box, "txFillClrLst") is { } listed) return listed;
        var font = StyleLabel(style, box)?.Element(Diagram + "style")?.Element(Drawing + "fontRef")?.Elements().FirstOrDefault();
        if (font is not null && font.Name.LocalName != "scrgbClr") return new XElement(font);
        return Scheme(box.HasGeometry && FillReference(box) != 0 ? "lt1" : "tx1");
    }

    public int FillReference(DiagramBox box) => Index(StyleLabel(style, box)?.Element(Diagram + "style")?.Element(Drawing + "fillRef"), 1);

    public int LineReference(DiagramBox box) => Index(StyleLabel(style, box)?.Element(Diagram + "style")?.Element(Drawing + "lnRef"), 2);

    private int Index(XElement? reference, int fallback) =>
        int.TryParse((string?)reference?.Attribute("idx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private XElement? Pick(DiagramBox box, string list)
    {
        var entries = StyleLabel(colors, box)?.Element(Diagram + list);
        var choices = entries?.Elements().ToList() ?? [];
        if (choices.Count == 0) return null;
        var method = (string?)entries!.Attribute("meth") ?? "repeat";
        var index = method == "span" && box.StyleCount > 1
            ? (int)Math.Round(box.StyleIndex * (choices.Count - 1) / (double)(box.StyleCount - 1))
            : box.StyleIndex % choices.Count;
        return new XElement(choices[Math.Clamp(index, 0, choices.Count - 1)]);
    }

    private XElement? StyleLabel(XDocument? document, DiagramBox box)
    {
        var labels = document?.Root?.Elements(Diagram + "styleLbl").ToList();
        if (labels is null || labels.Count == 0) return null;
        var name = box.StyleLabel ?? (box.IsConnector ? "sibTrans2D1" : "node1");
        return labels.FirstOrDefault(label => (string?)label.Attribute("name") == name)
            ?? labels.FirstOrDefault(label => (string?)label.Attribute("name") == (box.IsConnector ? "sibTrans2D1" : "node1"));
    }

    private XElement Scheme(string value) => new(Drawing + "schemeClr", new XAttribute("val", value));
}

internal sealed class DiagramDrawingComposer
{
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Shapes = "http://schemas.microsoft.com/office/drawing/2008/diagram";
    private readonly IReadOnlyDictionary<int, int> _lineWidths = new Dictionary<int, int> { [1] = 6350, [2] = 12700, [3] = 19050 };

    public List<XElement> Compose(DiagramBox root, DiagramStyleSheet styles, XElement? background = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(styles);
        var shapes = new List<XElement>();
        if (Fill(background) is { } backdrop && root.Bounds.Width > 0)
            shapes.Add(new XElement(Shapes + "sp", new XAttribute("modelId", "background"),
                new XElement(Shapes + "nvSpPr", new XElement(Shapes + "cNvPr", new XAttribute("id", "0"), new XAttribute("name", string.Empty)), new XElement(Shapes + "cNvSpPr")),
                new XElement(Shapes + "spPr",
                    new XElement(Drawing + "xfrm", new XElement(Drawing + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                        new XElement(Drawing + "ext", new XAttribute("cx", Emu(root.Bounds.Width)), new XAttribute("cy", Emu(root.Bounds.Height)))),
                    new XElement(Drawing + "prstGeom", new XAttribute("prst", "rect"), new XElement(Drawing + "avLst")),
                    new XElement(backdrop), new XElement(Drawing + "ln", new XElement(Drawing + "noFill")))));
        foreach (var box in root.SelfAndDescendants())
        {
            var geometry = Geometry(box);
            if (box.Bounds.Width <= 0 || box.Bounds.Height <= 0) continue;
            if (geometry is null && !box.HasText) continue;
            shapes.Add(Shape(box, geometry, styles));
        }
        return shapes;
    }

    private XElement? Fill(XElement? properties) =>
        properties?.Elements().FirstOrDefault(element => element.Name.LocalName is "solidFill" or "gradFill" or "noFill" or "pattFill" or "blipFill");

    private string? Geometry(DiagramBox box)
    {
        if (box.Shape is not { } shape || shape.HideGeometry) return null;
        var preset = box.PresetOverride ?? (shape.Type == "conn" ? "rightArrow" : shape.Type);
        return preset is { Length: > 0 } and not "none" ? preset : null;
    }

    private XElement Shape(DiagramBox box, string? geometry, DiagramStyleSheet styles)
    {
        var bounds = box.Bounds;
        var transform = new XElement(Drawing + "xfrm",
            new XElement(Drawing + "off", new XAttribute("x", Emu(bounds.X)), new XAttribute("y", Emu(bounds.Y))),
            new XElement(Drawing + "ext", new XAttribute("cx", Emu(bounds.Width)), new XAttribute("cy", Emu(bounds.Height))));
        if (Math.Abs(box.Rotation) > 0.01) transform.SetAttributeValue("rot", ((long)Math.Round(Normalised(box.Rotation) * 60000)).ToString(CultureInfo.InvariantCulture));

        var properties = new XElement(Shapes + "spPr", transform,
            new XElement(Drawing + "prstGeom", new XAttribute("prst", geometry ?? "rect"), Adjustments(box, geometry)));
        if (geometry is not null && Fill(box.CustomProperties) is { } custom)
            properties.Add(new XElement(custom));
        else if (geometry is not null && styles.FillReference(box) != 0 && styles.Fill(box) is { } fill)
            properties.Add(new XElement(Drawing + "solidFill", fill));
        else
            properties.Add(new XElement(Drawing + "noFill"));
        properties.Add(geometry is not null && box.CustomProperties?.Element(Drawing + "ln") is { } customLine ? new XElement(customLine)
            : geometry is not null && styles.LineReference(box) != 0 && styles.Line(box) is { } line
            ? new XElement(Drawing + "ln", new XAttribute("w", _lineWidths.GetValueOrDefault(styles.LineReference(box), 12700)), new XElement(Drawing + "solidFill", line))
            : new XElement(Drawing + "ln", new XElement(Drawing + "noFill")));

        var shape = new XElement(Shapes + "sp", new XAttribute("modelId", box.Point.Id),
            new XElement(Shapes + "nvSpPr", new XElement(Shapes + "cNvPr", new XAttribute("id", "0"), new XAttribute("name", string.Empty)), new XElement(Shapes + "cNvSpPr")),
            properties);
        if (box.HasText) shape.Add(Body(box, styles));
        return shape;
    }

    private XElement Adjustments(DiagramBox box, string? geometry)
    {
        var list = new XElement(Drawing + "avLst");
        if (geometry == "trapezoid" && box.Bounds.Width > 0 && box.Bounds.Height > 0)
        {
            var inset = Math.Min(box.Bounds.Width / 2, box.Bounds.Height * 0.5);
            list.Add(Guide("adj", inset / Math.Min(box.Bounds.Width, box.Bounds.Height) * 100000));
            return list;
        }
        foreach (var (name, value) in box.Shape?.Adjustments ?? [])
            list.Add(Guide(name == "1" ? "adj" : "adj" + name, value * 100000));
        return list;
    }

    private XElement Guide(string name, double value) =>
        new(Drawing + "gd", new XAttribute("name", name), new XAttribute("fmla", "val " + ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)));

    private XElement Body(DiagramBox box, DiagramStyleSheet styles)
    {
        var inset = Emu(box.FontSize * 0.1 * ((double)PulseOffice.Foundation.Units.Emu.PerPoint)).ToString(CultureInfo.InvariantCulture);
        var anchor = box.Algorithm.Parameter("txAnchorVert", "mid") switch { "t" => "t", "b" => "b", _ => "ctr" };
        var body = new XElement(Shapes + "txBody",
            new XElement(Drawing + "bodyPr", new XAttribute("spcFirstLastPara", "0"), new XAttribute("vert", "horz"), new XAttribute("wrap", "square"),
                new XAttribute("lIns", inset), new XAttribute("tIns", inset), new XAttribute("rIns", inset), new XAttribute("bIns", inset),
                new XAttribute("numCol", "1"), new XAttribute("anchor", anchor), new XAttribute("anchorCtr", "0"), new XElement(Drawing + "noAutofit")),
            new XElement(Drawing + "lstStyle"));
        var bullets = box.Paragraphs.Any(paragraph => paragraph.Level > 0);
        var size = ((int)Math.Round(box.FontSize * 100)).ToString(CultureInfo.InvariantCulture);
        foreach (var paragraph in box.Paragraphs)
        {
            var copy = new XElement(Drawing + "p");
            var properties = new XElement(Drawing + "pPr", new XAttribute("lvl", Math.Clamp(paragraph.Level, 0, 8)),
                new XAttribute("algn", bullets && paragraph.Level > 0 ? "l" : box.Algorithm.Parameter("parTxLTRAlign", bullets ? "l" : "ctr") switch { "l" => "l", "r" => "r", _ => "ctr" }));
            if (paragraph.Level > 0)
            {
                properties.SetAttributeValue("marL", (171450 * paragraph.Level).ToString(CultureInfo.InvariantCulture));
                properties.SetAttributeValue("indent", "-171450");
                properties.Add(new XElement(Drawing + "buChar", new XAttribute("char", "•")));
            }
            else properties.Add(new XElement(Drawing + "buNone"));
            copy.Add(properties);
            foreach (var run in paragraph.Source.Elements(Drawing + "r"))
            {
                var runCopy = new XElement(run);
                var runProperties = runCopy.Element(Drawing + "rPr");
                if (runProperties is null) runCopy.AddFirst(runProperties = new XElement(Drawing + "rPr"));
                runProperties.SetAttributeValue("sz", (string?)runProperties.Attribute("sz") ?? size);
                if (!runProperties.Elements().Any(element => element.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal)))
                    runProperties.AddFirst(new XElement(Drawing + "solidFill", styles.Text(box)));
                copy.Add(runCopy);
            }
            body.Add(copy);
        }
        return body;
    }

    private double Normalised(double degrees) => (degrees % 360 + 360) % 360;

    private long Emu(double value) => (long)Math.Round(value);
}
