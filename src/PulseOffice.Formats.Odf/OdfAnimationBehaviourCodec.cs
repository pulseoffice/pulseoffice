using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfAnimationBehaviourCodec
{
    private const int DeepestNesting = 64;

    private static readonly XNamespace Anim = OdfPackage.Namespaces.Anim;
    private static readonly XNamespace Smil = OdfPackage.Namespaces.Smil;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;

    public XElement Write(AnimationBehaviours behaviours, string targetId)
    {
        var iterating = behaviours.IterateType is not null;
        var root = iterating
            ? new XElement(Anim + "iterate",
                new XAttribute(Smil + "targetElement", targetId),
                new XAttribute(Anim + "iterate-type", behaviours.IterateType!),
                new XAttribute(Anim + "iterate-interval", Seconds(behaviours.IterateInterval ?? 0)))
            : new XElement(Anim + "par");
        if (iterating && behaviours.TextOnly) root.Add(new XAttribute(Anim + "sub-item", "text"));
        root.Add(TimingAttributes(behaviours.RootTiming));
        var visitor = new ElementVisitor(this, iterating ? null : targetId);
        foreach (var node in behaviours.Nodes) root.Add(node.Accept(visitor));
        return root;
    }

    public AnimationBehaviours Read(XElement effect)
    {
        var iterating = effect.Name == Anim + "iterate";
        return new AnimationBehaviours
        {
            Nodes = [.. effect.Elements().Select(element => Node(element, 1)).OfType<AnimationBehaviour>()],
            IterateType = iterating ? (string?)effect.Attribute(Anim + "iterate-type") ?? "by-letter" : null,
            IterateInterval = iterating ? Number((string?)effect.Attribute(Anim + "iterate-interval")) ?? 0 : null,
            TextOnly = iterating && (string?)effect.Attribute(Anim + "sub-item") == "text",
            RootTiming = ReadTiming(effect) with { Begin = null, Duration = null, Fill = null, RepeatCount = null },
        };
    }

    private AnimationBehaviour? Node(XElement element, int depth)
    {
        if (depth > DeepestNesting) return null;
        var timing = ReadTiming(element);
        var subItem = (string?)element.Attribute(Anim + "sub-item");
        string? Smil(string name) => (string?)element.Attribute(OdfAnimationBehaviourCodec.Smil + name);
        AnimationBehaviour? behaviour = element.Name.LocalName switch
        {
            "par" or "seq" => new GroupBehaviour
            {
                Sequential = element.Name.LocalName == "seq",
                Children = [.. element.Elements().Select(child => Node(child, depth + 1)).OfType<AnimationBehaviour>()],
            },
            "set" when Smil("attributeName") is { } attribute => new SetBehaviour { Attribute = attribute, To = Smil("to") },
            "animate" when Smil("attributeName") is { } attribute => new AnimateBehaviour
            {
                Attribute = attribute,
                Values = Smil("values")?.Split(';'),
                KeyTimes = Smil("keyTimes") is { } times ? [.. times.Split(';').Select(time => Number(time) ?? 0)] : null,
                From = Smil("from"),
                To = Smil("to"),
                By = Smil("by"),
                Formula = (string?)element.Attribute(Anim + "formula"),
                CalcMode = Smil("calcMode"),
                Additive = Smil("additive"),
                KeySplines = Smil("keySplines"),
            },
            "animateColor" when Smil("attributeName") is { } attribute => new ColorBehaviour
            {
                Attribute = attribute,
                From = Smil("from"),
                To = Smil("to"),
                By = Smil("by"),
                ColorSpace = (string?)element.Attribute(Anim + "color-interpolation") ?? "rgb",
                Clockwise = (string?)element.Attribute(Anim + "color-interpolation-direction") != "counter-clockwise",
            },
            "animateTransform" when (string?)element.Attribute(Svg + "type") is { } type => new TransformBehaviour
            {
                TransformType = type, From = Smil("from"), To = Smil("to"), By = Smil("by"),
            },
            "animateMotion" when (string?)element.Attribute(Svg + "path") is { } path => new MotionBehaviour { Path = path },
            "transitionFilter" when Smil("type") is { } type => new FilterBehaviour
            {
                Type = type,
                Subtype = Smil("subtype"),
                Forward = Smil("direction") != "reverse",
                Entering = Smil("mode") != "out",
            },
            "command" when (string?)element.Attribute(Anim + "command") is { } command => new CommandBehaviour { Command = command },
            "animatePhysics" => new PhysicsBehaviour
            {
                Bounciness = Number((string?)element.Attribute(Anim + "bounciness")),
                VelocityX = Number((string?)element.Attribute(Anim + "velocity-x")),
                VelocityY = Number((string?)element.Attribute(Anim + "velocity-y")),
            },
            _ => null,
        };
        return behaviour is null ? null : behaviour with { Timing = timing, SubItem = subItem };
    }

    private BehaviourTiming ReadTiming(XElement element)
    {
        string? Smil(string name) => (string?)element.Attribute(OdfAnimationBehaviourCodec.Smil + name);
        return new BehaviourTiming
        {
            Begin = Number(Smil("begin")),
            Duration = Number(Smil("dur")),
            Fill = Smil("fill"),
            Accelerate = Number(Smil("accelerate")) ?? 0,
            Decelerate = Number(Smil("decelerate")) ?? 0,
            AutoReverse = Smil("autoReverse") == "true",
            RepeatCount = Number(Smil("repeatCount")),
        };
    }

    private IEnumerable<XAttribute> TimingAttributes(BehaviourTiming timing)
    {
        if (timing.Begin is { } begin) yield return new XAttribute(Smil + "begin", Seconds(begin));
        if (timing.Duration is { } duration) yield return new XAttribute(Smil + "dur", Seconds(duration));
        if (timing.Fill is { } fill) yield return new XAttribute(Smil + "fill", fill);
        if (timing.Accelerate > 0) yield return new XAttribute(Smil + "accelerate", Format(timing.Accelerate));
        if (timing.Decelerate > 0) yield return new XAttribute(Smil + "decelerate", Format(timing.Decelerate));
        if (timing.AutoReverse) yield return new XAttribute(Smil + "autoReverse", "true");
        if (timing.RepeatCount is { } repeat) yield return new XAttribute(Smil + "repeatCount", Format(repeat));
    }

    private string Seconds(double seconds) => Format(seconds) + "s";

    private string Format(double value) => Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);

    private double? Number(string? value) =>
        value is not null && double.TryParse(value.TrimEnd('s', 'S'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
        && double.IsFinite(number) ? number : null;

    private sealed class ElementVisitor(OdfAnimationBehaviourCodec codec, string? target) : IAnimationBehaviourVisitor<XElement>
    {
        public XElement Visit(GroupBehaviour behaviour) =>
            Element(behaviour.Sequential ? "seq" : "par", behaviour, false, behaviour.Children.Select(child => child.Accept(this)));

        public XElement Visit(SetBehaviour behaviour) =>
            Element("set", behaviour, true, Attr(Smil + "attributeName", behaviour.Attribute), Attr(Smil + "to", behaviour.To));

        public XElement Visit(AnimateBehaviour behaviour) =>
            Element("animate", behaviour, true,
                Attr(Smil + "attributeName", behaviour.Attribute),
                Attr(Smil + "values", behaviour.Values is { } values ? string.Join(';', values) : null),
                Attr(Smil + "keyTimes", behaviour.KeyTimes is { } times ? string.Join(';', times.Select(codec.Format)) : null),
                Attr(Smil + "from", behaviour.From),
                Attr(Smil + "to", behaviour.To),
                Attr(Smil + "by", behaviour.By),
                Attr(Anim + "formula", behaviour.Formula),
                Attr(Smil + "calcMode", behaviour.CalcMode),
                Attr(Smil + "additive", behaviour.Additive),
                Attr(Smil + "keySplines", behaviour.KeySplines));

        public XElement Visit(ColorBehaviour behaviour) =>
            Element("animateColor", behaviour, true,
                Attr(Smil + "attributeName", behaviour.Attribute),
                Attr(Smil + "from", behaviour.From),
                Attr(Smil + "to", behaviour.To),
                Attr(Smil + "by", behaviour.By),
                Attr(Anim + "color-interpolation", behaviour.ColorSpace),
                Attr(Anim + "color-interpolation-direction", behaviour.Clockwise ? "clockwise" : "counter-clockwise"));

        public XElement Visit(TransformBehaviour behaviour) =>
            Element("animateTransform", behaviour, true,
                Attr(Smil + "attributeName", "transform"),
                Attr(Smil + "from", behaviour.From),
                Attr(Smil + "to", behaviour.To),
                Attr(Smil + "by", behaviour.By),
                Attr(Svg + "type", behaviour.TransformType));

        public XElement Visit(MotionBehaviour behaviour) =>
            Element("animateMotion", behaviour, true, Attr(Svg + "path", behaviour.Path));

        public XElement Visit(FilterBehaviour behaviour) =>
            Element("transitionFilter", behaviour, true,
                Attr(Smil + "type", behaviour.Type),
                Attr(Smil + "subtype", behaviour.Subtype),
                Attr(Smil + "direction", behaviour.Forward ? null : "reverse"),
                Attr(Smil + "mode", behaviour.Entering ? null : "out"));

        public XElement Visit(CommandBehaviour behaviour) =>
            Element("command", behaviour, true, Attr(Anim + "command", behaviour.Command));

        public XElement Visit(PhysicsBehaviour behaviour) =>
            Element("animatePhysics", behaviour, true,
                Attr(Anim + "bounciness", behaviour.Bounciness is { } bounciness ? codec.Format(bounciness) : null),
                Attr(Anim + "velocity-x", behaviour.VelocityX is { } x ? codec.Format(x) : null),
                Attr(Anim + "velocity-y", behaviour.VelocityY is { } y ? codec.Format(y) : null));

        private XAttribute? Attr(XName name, string? value) => value is null ? null : new XAttribute(name, value);

        private XElement Element(string name, AnimationBehaviour behaviour, bool targeted, params IEnumerable<object?> content)
        {
            var element = new XElement(Anim + name, codec.TimingAttributes(behaviour.Timing));
            if (targeted && target is not null) element.Add(new XAttribute(Smil + "targetElement", target));
            if (behaviour.SubItem is { } subItem) element.Add(new XAttribute(Anim + "sub-item", subItem));
            element.Add(content);
            return element;
        }
    }
}
