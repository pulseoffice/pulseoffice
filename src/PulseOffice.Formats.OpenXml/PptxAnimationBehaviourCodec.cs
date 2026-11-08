using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.OpenXml;

public sealed class TimeNodeIdSequence(int first)
{
    private int _next = first;

    public int Next() => _next++;

    public int Peek => _next;
}

public sealed class PptxAnimationBehaviourCodec(IAnimationAttributeTable attributeTable, IAnimationFilterTable filterTable)
{
    private readonly IAnimationAttributeTable _attributes = attributeTable;
    private readonly IAnimationFilterTable _filters = filterTable;

    public PptxAnimationBehaviourCodec() : this(new AnimationAttributeTable(), new AnimationFilterTable())
    {
    }

    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private readonly IReadOnlyList<(string Odf, string Ooxml)> _iterations =
        [("by-letter", "lt"), ("by-word", "wd"), ("by-paragraph", "el")];

    private readonly IReadOnlyList<(string Odf, string Ooxml)> _calcModes =
        [("discrete", "discrete"), ("linear", "lin"), ("paced", "lin"), ("spline", "lin")];

    private readonly IReadOnlyList<(string Odf, string Ooxml)> _commands =
        [("play", "playFrom(0.0)"), ("stop", "stop"), ("toggle-pause", "togglePause")];

    private readonly IReadOnlyList<(string Odf, string Ooxml)> _additives =
        [("replace", "repl"), ("sum", "sum"), ("multiply", "mult"), ("base", "base"), ("none", "none")];

    private readonly IReadOnlySet<string> _numericAttributes =
        new HashSet<string>(StringComparer.Ordinal) { "x", "y", "width", "height", "rotate", "opacity", "skewX", "font-size" };

    private readonly IReadOnlySet<string> _colorAttributes =
        new HashSet<string>(StringComparer.Ordinal) { "color", "fill-color", "stroke-color", "dim" };

    public XNamespace PresentationNamespace => P;

    public IEnumerable<XAttribute> RootAttributes(AnimationBehaviours behaviours)
    {
        if (behaviours.RootTiming.Accelerate > 0) yield return new XAttribute("accel", Percent(behaviours.RootTiming.Accelerate));
        if (behaviours.RootTiming.Decelerate > 0) yield return new XAttribute("decel", Percent(behaviours.RootTiming.Decelerate));
        if (behaviours.RootTiming.AutoReverse) yield return new XAttribute("autoRev", "1");
    }

    public XElement? Iterate(AnimationBehaviours behaviours) =>
        behaviours.IterateType is not { } type
            ? null
            : new XElement(P + "iterate",
                new XAttribute("type", _iterations.FirstOrDefault(pair => pair.Odf == type).Ooxml ?? "lt"),
                new XElement(P + "tmAbs", new XAttribute("val", Milliseconds(behaviours.IterateInterval ?? 0))));

    public IEnumerable<XElement> Children(AnimationBehaviours behaviours, uint shapeId, TimeNodeIdSequence ids)
    {
        var visitor = new ElementVisitor(this, shapeId, ids);
        return [.. behaviours.Nodes.Select(node => node.Accept(visitor)).OfType<XElement>()];
    }

    public AnimationBehaviours Read(XElement effectCommonTimeNode)
    {
        var iterate = effectCommonTimeNode.Element(P + "iterate");
        return new AnimationBehaviours
        {
            Nodes = [.. Nodes(effectCommonTimeNode)],
            IterateType = iterate is null
                ? null
                : _iterations.FirstOrDefault(pair => pair.Ooxml == ((string?)iterate.Attribute("type") ?? "el")).Odf ?? "by-paragraph",
            IterateInterval = iterate is null
                ? null
                : Number((string?)iterate.Element(P + "tmAbs")?.Attribute("val")) / 1000.0 ?? 0,
            TextOnly = false,
            RootTiming = new BehaviourTiming
            {
                Accelerate = Number((string?)effectCommonTimeNode.Attribute("accel")) / 100000.0 ?? 0,
                Decelerate = Number((string?)effectCommonTimeNode.Attribute("decel")) / 100000.0 ?? 0,
                AutoReverse = (string?)effectCommonTimeNode.Attribute("autoRev") is "1" or "true",
            },
        };
    }

    private IEnumerable<AnimationBehaviour> Nodes(XElement commonTimeNode) =>
        commonTimeNode.Element(P + "childTnLst")?.Elements().Select(Node).OfType<AnimationBehaviour>() ?? [];

    private AnimationBehaviour? Node(XElement element)
    {
        var behaviourNode = element.Element(P + "cBhvr");
        var timeNode = element.Name.LocalName is "par" or "seq" ? element.Element(P + "cTn") : behaviourNode?.Element(P + "cTn");
        if (timeNode is null) return null;
        var attributeName = (string?)behaviourNode?.Element(P + "attrNameLst")?.Element(P + "attrName");
        var attribute = attributeName is null ? null : _attributes.FromOoxml(attributeName.Trim());
        AnimationBehaviour? behaviour = element.Name.LocalName switch
        {
            "par" or "seq" => new GroupBehaviour { Sequential = element.Name.LocalName == "seq", Children = [.. Nodes(timeNode)] },
            "set" when attribute is not null => new SetBehaviour { Attribute = attribute, To = Value(attribute, element.Element(P + "to")) },
            "anim" when attribute is not null => Animate(element, attribute, behaviourNode!),
            "animClr" when attribute is not null => new ColorBehaviour
            {
                Attribute = attribute,
                From = Color(element.Element(P + "from")),
                To = Color(element.Element(P + "to")),
                By = RgbBy(element.Element(P + "by")),
                ColorSpace = (string?)element.Attribute("clrSpc") ?? "rgb",
                Clockwise = (string?)element.Attribute("dir") != "ccw",
            },
            "animRot" => new TransformBehaviour
            {
                TransformType = "rotate",
                From = Degrees((string?)element.Attribute("from")),
                To = Degrees((string?)element.Attribute("to")),
                By = Degrees((string?)element.Attribute("by")),
            },
            "animScale" => new TransformBehaviour
            {
                TransformType = "scale",
                From = Scale(element.Element(P + "from")),
                To = Scale(element.Element(P + "to")),
                By = Scale(element.Element(P + "by")),
            },
            "animMotion" when (string?)element.Attribute("path") is { } path => new MotionBehaviour { Path = path },
            "animEffect" => Filter(element),
            "cmd" when (string?)element.Attribute("cmd") is { } command => new CommandBehaviour
            {
                Command = _commands.FirstOrDefault(pair => command.StartsWith(pair.Ooxml.Split('(')[0], StringComparison.Ordinal)).Odf ?? command,
            },
            _ => null,
        };
        return behaviour is null ? null : behaviour with { Timing = Timing(timeNode) };
    }

    private AnimationBehaviour Filter(XElement element)
    {
        var filter = _filters.FromOoxml((string?)element.Attribute("filter"));
        return new FilterBehaviour
        {
            Type = filter.Type,
            Subtype = filter.Subtype,
            Forward = filter.Forward,
            Entering = (string?)element.Attribute("transition") != "out",
        };
    }

    private AnimateBehaviour Animate(XElement element, string attribute, XElement behaviourNode)
    {
        var points = element.Element(P + "tavLst")?.Elements(P + "tav").ToList() ?? [];
        string? Converted(string? value) => value is null ? null : _attributes.ValueFromOoxml(attribute, value);
        var calcMode = (string?)element.Attribute("calcmode");
        var additive = (string?)behaviourNode.Attribute("additive");
        return new AnimateBehaviour
        {
            Attribute = attribute,
            Values = points.Count == 0 ? null : [.. points.Select(point => Value(attribute, point.Element(P + "val")) ?? string.Empty)],
            KeyTimes = points.Count == 0 ? null : [.. points.Select(point => (Number((string?)point.Attribute("tm")) ?? 0) / 100000.0)],
            Formula = points.Select(point => (string?)point.Attribute("fmla")).FirstOrDefault(formula => !string.IsNullOrEmpty(formula)) is { } formula
                ? _attributes.ValueFromOoxml(attribute, formula)
                : null,
            From = Converted((string?)element.Attribute("from")),
            To = Converted((string?)element.Attribute("to")),
            By = Converted((string?)element.Attribute("by")),
            CalcMode = calcMode is null or "fmla" ? null : _calcModes.FirstOrDefault(pair => pair.Ooxml == calcMode).Odf,
            Additive = additive is null ? null : _additives.FirstOrDefault(pair => pair.Ooxml == additive).Odf,
        };
    }

    private string? Value(string attribute, XElement? holder)
    {
        var value = holder?.Elements().FirstOrDefault();
        return value?.Name.LocalName switch
        {
            "strVal" => _attributes.ValueFromOoxml(attribute, (string?)value.Attribute("val") ?? string.Empty),
            "fltVal" or "intVal" => (string?)value.Attribute("val"),
            "boolVal" => (string?)value.Attribute("val") is "1" or "true" ? "true" : "false",
            "clrVal" => Color(value),
            _ => null,
        };
    }

    private string? Color(XElement? holder) =>
        (string?)holder?.Descendants(A + "srgbClr").FirstOrDefault()?.Attribute("val") is { Length: 6 } hex ? "#" + hex.ToLowerInvariant() : null;

    private string? RgbBy(XElement? holder)
    {
        if (holder?.Element(P + "rgb") is not { } rgb) return null;
        var channels = new[] { "r", "g", "b" }
            .Select(name => (int)Math.Round((Number((string?)rgb.Attribute(name)) ?? 0) * 255 / 100000.0))
            .Select(channel => Math.Clamp(channel, 0, 255).ToString("x2", CultureInfo.InvariantCulture));
        return "#" + string.Concat(channels);
    }

    private string? Degrees(string? sixtieths) =>
        Number(sixtieths) is { } value ? Format(value / 60000.0) : null;

    private string? Scale(XElement? point) =>
        point is null ? null : Format((Number((string?)point.Attribute("x")) ?? 100000) / 100000.0) + "," + Format((Number((string?)point.Attribute("y")) ?? 100000) / 100000.0);

    private BehaviourTiming Timing(XElement timeNode)
    {
        var duration = (string?)timeNode.Attribute("dur");
        var delay = (string?)timeNode.Element(P + "stCondLst")?.Element(P + "cond")?.Attribute("delay");
        var repeat = (string?)timeNode.Attribute("repeatCount");
        return new BehaviourTiming
        {
            Begin = Number(delay) / 1000.0,
            Duration = Number(duration) / 1000.0,
            Fill = (string?)timeNode.Attribute("fill"),
            Accelerate = Number((string?)timeNode.Attribute("accel")) / 100000.0 ?? 0,
            Decelerate = Number((string?)timeNode.Attribute("decel")) / 100000.0 ?? 0,
            AutoReverse = (string?)timeNode.Attribute("autoRev") is "1" or "true",
            RepeatCount = Number(repeat) / 1000.0,
        };
    }

    private double? Number(string? value) =>
        value is not null && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? number
            : null;

    private string Format(double value) => Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);

    private int Percent(double fraction) => (int)Math.Round(fraction * 100000);

    private int Milliseconds(double seconds) => (int)Math.Round(seconds * 1000);

    private sealed class ElementVisitor(PptxAnimationBehaviourCodec codec, uint shapeId, TimeNodeIdSequence ids) : IAnimationBehaviourVisitor<XElement?>
    {
        public XElement? Visit(GroupBehaviour behaviour)
        {
            var timeNode = TimeNode(behaviour.Timing);
            timeNode.Add(new XElement(P + "childTnLst", behaviour.Children.Select(child => child.Accept(this)).OfType<XElement>()));
            return new XElement(P + (behaviour.Sequential ? "seq" : "par"), timeNode);
        }

        public XElement? Visit(SetBehaviour behaviour) =>
            new(P + "set", Behaviour(behaviour, behaviour.Attribute),
                new XElement(P + "to", Value(behaviour.Attribute, behaviour.To ?? string.Empty)));

        public XElement? Visit(AnimateBehaviour behaviour)
        {
            var attribute = behaviour.Attribute;
            var element = new XElement(P + "anim");
            if (behaviour.By is { } by) element.Add(new XAttribute("by", codec._attributes.ValueToOoxml(attribute, by)));
            if (behaviour.From is { } from) element.Add(new XAttribute("from", codec._attributes.ValueToOoxml(attribute, from)));
            if (behaviour.To is { } to) element.Add(new XAttribute("to", codec._attributes.ValueToOoxml(attribute, to)));
            element.Add(new XAttribute("calcmode", behaviour.CalcMode is { } mode
                ? codec._calcModes.FirstOrDefault(pair => pair.Odf == mode).Ooxml ?? "lin"
                : "lin"));
            element.Add(new XAttribute("valueType", codec._colorAttributes.Contains(attribute) ? "clr" : codec._numericAttributes.Contains(attribute) ? "num" : "str"));
            var behaviourElement = Behaviour(behaviour, attribute);
            if (behaviour.Additive is { } additive)
                behaviourElement.Add(new XAttribute("additive", codec._additives.FirstOrDefault(pair => pair.Odf == additive).Ooxml ?? "base"));
            element.Add(behaviourElement);
            if (behaviour.Values is { Count: > 0 } values)
            {
                var times = behaviour.KeyTimes is { } keyTimes && keyTimes.Count == values.Count
                    ? keyTimes
                    : [.. values.Select((_, index) => values.Count == 1 ? 0 : index / (double)(values.Count - 1))];
                element.Add(new XElement(P + "tavLst", values.Select((value, index) =>
                {
                    var point = new XElement(P + "tav", new XAttribute("tm", codec.Percent(times[index])));
                    if (behaviour.Formula is { } formula) point.Add(new XAttribute("fmla", codec._attributes.ValueToOoxml(attribute, formula)));
                    point.Add(new XElement(P + "val", Value(attribute, value)));
                    return point;
                })));
            }

            return element;
        }

        public XElement? Visit(ColorBehaviour behaviour)
        {
            var element = new XElement(P + "animClr",
                new XAttribute("clrSpc", behaviour.ColorSpace == "hsl" ? "hsl" : "rgb"),
                new XAttribute("dir", behaviour.Clockwise ? "cw" : "ccw"),
                Behaviour(behaviour, behaviour.Attribute));
            if (Rgb(behaviour.By) is { } by)
                element.Add(new XElement(P + "by", new XElement(P + "rgb",
                    new XAttribute("r", by.R * 100000 / 255), new XAttribute("g", by.G * 100000 / 255), new XAttribute("b", by.B * 100000 / 255))));
            if (Color(behaviour.From) is { } from) element.Add(new XElement(P + "from", from));
            if (Color(behaviour.To) is { } to) element.Add(new XElement(P + "to", to));
            return element;
        }

        public XElement? Visit(TransformBehaviour behaviour)
        {
            switch (behaviour.TransformType)
            {
                case "rotate":
                    var rotation = new XElement(P + "animRot");
                    foreach (var (name, value) in new[] { ("by", behaviour.By), ("from", behaviour.From), ("to", behaviour.To) })
                        if (codec.Number(value) is { } degrees) rotation.Add(new XAttribute(name, (long)Math.Round(degrees * 60000)));
                    rotation.Add(Behaviour(behaviour, "rotate"));
                    return rotation;
                case "scale":
                    var scale = new XElement(P + "animScale", Behaviour(behaviour));
                    foreach (var (name, value) in new[] { ("by", behaviour.By), ("from", behaviour.From), ("to", behaviour.To) })
                        if (Point(value) is { } point) scale.Add(new XElement(P + name, new XAttribute("x", point.X), new XAttribute("y", point.Y)));
                    return scale;
                default:
                    return null;
            }
        }

        public XElement? Visit(MotionBehaviour behaviour) =>
            new(P + "animMotion",
                new XAttribute("origin", "layout"),
                new XAttribute("path", behaviour.Path),
                new XAttribute("pathEditMode", "relative"),
                Behaviour(behaviour, "x", "y"));

        public XElement? Visit(FilterBehaviour behaviour) =>
            new(P + "animEffect",
                new XAttribute("transition", behaviour.Entering ? "in" : "out"),
                new XAttribute("filter", codec._filters.ToOoxml(behaviour)),
                Behaviour(behaviour));

        public XElement? Visit(CommandBehaviour behaviour) =>
            new(P + "cmd",
                new XAttribute("type", "call"),
                new XAttribute("cmd", codec._commands.FirstOrDefault(pair => pair.Odf == behaviour.Command).Ooxml ?? behaviour.Command),
                Behaviour(behaviour));

        public XElement? Visit(PhysicsBehaviour behaviour) => null;

        private XElement Behaviour(AnimationBehaviour behaviour, params string?[] names)
        {
            var element = new XElement(P + "cBhvr", TimeNode(behaviour.Timing),
                new XElement(P + "tgtEl", new XElement(P + "spTgt", new XAttribute("spid", shapeId))));
            var listed = names.OfType<string>().ToList();
            if (listed.Count > 0)
                element.Add(new XElement(P + "attrNameLst", listed.Select(name => new XElement(P + "attrName", codec._attributes.ToOoxml(name)))));
            return element;
        }

        private XElement TimeNode(BehaviourTiming timing)
        {
            var node = new XElement(P + "cTn", new XAttribute("id", ids.Next()));
            if (timing.Duration is { } duration) node.Add(new XAttribute("dur", Math.Max(1, codec.Milliseconds(duration))));
            if (timing.Accelerate > 0) node.Add(new XAttribute("accel", codec.Percent(timing.Accelerate)));
            if (timing.Decelerate > 0) node.Add(new XAttribute("decel", codec.Percent(timing.Decelerate)));
            if (timing.AutoReverse) node.Add(new XAttribute("autoRev", "1"));
            if (timing.RepeatCount is { } repeat) node.Add(new XAttribute("repeatCount", (int)Math.Round(repeat * 1000)));
            if (timing.Fill is "hold" or "remove" or "freeze" or "transition") node.Add(new XAttribute("fill", timing.Fill));
            if (timing.Begin is { } begin)
                node.Add(new XElement(P + "stCondLst", new XElement(P + "cond", new XAttribute("delay", codec.Milliseconds(begin)))));
            return node;
        }

        private XElement Value(string attribute, string value)
        {
            if (Color(value) is { } color) return new XElement(P + "clrVal", color);
            return codec._attributes.IsNumber(value)
                ? new XElement(P + "fltVal", new XAttribute("val", value))
                : new XElement(P + "strVal", new XAttribute("val", codec._attributes.ValueToOoxml(attribute, value)));
        }

        private XElement? Color(string? value) =>
            Rgb(value) is { } rgb ? new XElement(A + "srgbClr", new XAttribute("val", $"{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}")) : null;

        private (int R, int G, int B)? Rgb(string? value) =>
            value is { Length: 7 } && value[0] == '#'
            && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed)
                ? ((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF)
                : null;

        private (int X, int Y)? Point(string? value)
        {
            if (value is null) return null;
            var parts = value.Split(',');
            if (codec.Number(parts[0]) is not { } x) return null;
            var y = parts.Length > 1 ? codec.Number(parts[1]) ?? x : x;
            return ((int)Math.Round(x * 100000), (int)Math.Round(y * 100000));
        }
    }
}
