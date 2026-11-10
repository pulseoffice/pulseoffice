using PulseOffice.Formats;
using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfAnimationMapper
{
    private readonly AnimationClickGroups _groups = new();
    private readonly TokenMap<AnimationTrigger> _triggers = new(AnimationTrigger.OnClick, "on-click",
        (AnimationTrigger.WithPrevious, "with-previous"), (AnimationTrigger.AfterPrevious, "after-previous"));
    private readonly TokenMap<AnimationCategory> _categories = new(AnimationCategory.MotionPath, "motion-path",
        (AnimationCategory.Entrance, "entrance"), (AnimationCategory.Emphasis, "emphasis"), (AnimationCategory.Exit, "exit"), (AnimationCategory.Media, "media-call"));

    private readonly IAnimationPresetRegistry _presets;
    private readonly SmilTransitionMapper _transitions = new();
    private readonly IAnimationBehaviourResolver _behaviours;

    public OdfAnimationMapper(IAnimationBehaviourResolver? behaviours = null, IAnimationPresetRegistry? presets = null)
    {
        _behaviours = behaviours ?? new AnimationBehaviourResolver();
        _presets = presets ?? new AnimationPresetRegistry();
    }

    private readonly OdfAnimationBehaviourCodec _codec = new();

    private readonly OdfAnimationParagraphs _paragraphs = new();

    public void Identify(XElement page, Slide slide) => _paragraphs.Identify(page, slide);

    private static readonly XNamespace Anim = OdfPackage.Namespaces.Anim;
    private static readonly XNamespace Smil = OdfPackage.Namespaces.Smil;
    private static readonly XNamespace Pres = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;

    public XElement? Build(Slide slide, string pageId)
    {
        if (slide.Animations.Count == 0 && slide.Transition.IsNone) return null;

        var root = new XElement(Anim + "par", new XAttribute(Pres + "node-type", "timing-root"));
        if (!slide.Transition.IsNone)
        {
            var smil = _transitions.ToSmil(slide.Transition);
            var filter = new XElement(Anim + "transitionFilter",
                new XAttribute(Smil + "dur", Time(Math.Max(1, slide.Transition.DurationMs))),
                new XAttribute(Smil + "type", smil.Type),
                new XAttribute(Smil + "subtype", smil.Subtype));
            if (smil.Reverse) filter.Add(new XAttribute(Smil + "direction", "reverse"));
            root.Add(new XElement(Anim + "par", new XAttribute(Smil + "begin", pageId + ".begin"), filter));
        }

        if (slide.Animations.Count == 0) return root;

        var triggered = slide.Animations.Where(effect => effect.IsTriggered).ToList();
        var main = slide.Animations.Except(triggered).ToList();
        if (main.Count > 0 || triggered.Count == 0)
            root.Add(Sequence(new XElement(Anim + "seq", new XAttribute(Pres + "node-type", "main-sequence")), main));
        foreach (var sequence in triggered.GroupBy(effect => effect.TriggerShapeId!))
            root.Add(Sequence(new XElement(Anim + "seq",
                new XAttribute(Smil + "begin", sequence.Key + ".click"),
                new XAttribute(Pres + "node-type", "interactive-sequence")), [.. sequence]));
        return root;
    }

    private XElement Sequence(XElement seq, IReadOnlyList<AnimationEffect> animations)
    {
        foreach (var group in _groups.Of(animations))
        {
            var click = new XElement(Anim + "par",
                new XAttribute(Pres + "node-type", "on-click"));

            foreach (var effect in group)
            {
                if (effect.CurrentNative is { Format: NativeAnimation.Odf } native)
                {
                    click.Add(Native(native, effect));
                    continue;
                }

                var effectPar = _codec.Write(_behaviours.Resolve(effect), _paragraphs.Target(effect));
                effectPar.Add(
                    new XAttribute(Smil + "begin", Time(effect.DelayMs)),
                    new XAttribute(Smil + "fill", Fill(effect)),
                    new XAttribute(Pres + "node-type", _triggers.Token(effect.Trigger)),
                    new XAttribute(Pres + "preset-class", _categories.Token(effect.Category)),
                    new XAttribute(Pres + "preset-id", _presets.OdfPresetId(effect.Category, effect.Preset)));

                if (!string.IsNullOrEmpty(effect.SoundPath))
                    effectPar.Add(new XElement(Anim + "audio",
                        new XAttribute(Xlink + "href", effect.SoundPath)));

                if (effect.Repeat > 1)
                    effectPar.Add(new XAttribute(Smil + "repeatCount", effect.Repeat.ToString(CultureInfo.InvariantCulture)));

                if (!string.IsNullOrEmpty(effect.Subtype))
                    effectPar.Add(new XAttribute(Pres + "preset-sub-type", effect.Subtype));

                click.Add(effectPar);
            }

            seq.Add(click);
        }

        return seq;
    }

    private string Fill(AnimationEffect effect) => effect.Rewind ? "remove" : "hold";

    private XElement Native(NativeAnimation native, AnimationEffect effect)
    {
        var par = XElement.Parse(native.Xml);
        par.SetAttributeValue(Smil + "begin", Time(effect.DelayMs));
        par.SetAttributeValue(Smil + "fill", Fill(effect));
        par.SetAttributeValue(Pres + "node-type", _triggers.Token(effect.Trigger));
        par.SetAttributeValue(Pres + "preset-class", _categories.Token(effect.Category));
        par.SetAttributeValue(Pres + "preset-id", _presets.OdfPresetId(effect.Category, effect.Preset));
        par.SetAttributeValue(Pres + "preset-sub-type", effect.Subtype);
        foreach (var target in par.DescendantsAndSelf().Attributes(Smil + "targetElement").ToList())
            target.Value = _paragraphs.Target(effect);
        if (string.IsNullOrEmpty(effect.SoundPath))
            par.Descendants(Anim + "audio").Remove();
        return par;
    }

    public int? TransitionDuration(XElement page)
    {
        var filter = page.Element(Anim + "par")?.Elements(Anim + "par")
            .Where(par => ((string?)par.Attribute(Smil + "begin"))?.EndsWith(".begin", StringComparison.Ordinal) == true)
            .Select(par => par.Element(Anim + "transitionFilter"))
            .OfType<XElement>()
            .FirstOrDefault();
        var duration = Seconds((string?)filter?.Attribute(Smil + "dur"), -1);
        return duration > 0 ? duration : null;
    }

    public IReadOnlyList<AnimationEffect> Read(XElement page, string slideId)
    {
        var root = page.Element(Anim + "par");
        if (root is null) return [];

        var effects = new List<AnimationEffect>();
        var index = 0;

        foreach (var effectPar in root.Descendants()
            .Where(p => (p.Name == Anim + "par" || p.Name == Anim + "iterate") && p.Attribute(Pres + "preset-class") is not null))
        {
            var target = effectPar.DescendantsAndSelf()
                .Select(e => (string?)e.Attribute(Smil + "targetElement"))
                .FirstOrDefault(t => t is not null);
            if (target is null) continue;

            var category = _categories.Value((string?)effectPar.Attribute(Pres + "preset-class"));
            var single = effectPar.DescendantsAndSelf().Attributes(Smil + "targetElement").Select(attribute => attribute.Value).Distinct().Count() == 1;
            var set = effectPar.Element(Anim + "set");
            var timed = effectPar.Descendants().Where(e => e.Name != Anim + "set" && e.Attribute(Smil + "dur") is not null).ToList();

            var behaviours = _codec.Read(effectPar);
            var effect = new AnimationEffect
            {
                Id = $"{slideId}.anim{++index}",
                TargetId = target,
                Category = category,
                Preset = category == AnimationCategory.Media
                    ? (string?)effectPar.Descendants(Anim + "command").FirstOrDefault()?.Attribute(Anim + "command") ?? "play"
                    : _presets.FromOdf(category, (string?)effectPar.Attribute(Pres + "preset-id") ?? string.Empty),
                Trigger = _triggers.Value((string?)effectPar.Attribute(Pres + "node-type")),
                DurationMs = behaviours.NaturalDuration > 0 && double.IsFinite(behaviours.NaturalDuration) ? (int)Math.Min(AnimationEffect.LongestTimingMs, Math.Round(behaviours.NaturalDuration * 1000))
                    : timed.Count > 0
                    ? timed.Max(e => Seconds((string?)e.Attribute(Smil + "dur"), 500))
                    : Seconds((string?)set?.Attribute(Smil + "dur"), 500),
                DelayMs = Seconds((string?)effectPar.Attribute(Smil + "begin") ?? (string?)set?.Attribute(Smil + "begin"), 0),
                MotionPath = (string?)effectPar.Descendants(Anim + "animateMotion").FirstOrDefault()?.Attribute(Svg + "path"),
                SoundPath = (string?)effectPar.Descendants(Anim + "audio").FirstOrDefault()?.Attribute(Xlink + "href"),
                Repeat = RepeatCount((string?)effectPar.Attribute(Smil + "repeatCount")),
                Subtype = (string?)effectPar.Attribute(Pres + "preset-sub-type") is { Length: > 0 } subtype ? subtype : null,
                TriggerShapeId = TriggerShape(effectPar),
                Rewind = (string?)effectPar.Attribute(Smil + "fill") == "remove",
            };
            effects.Add(single
                ? effect with { Native = new NativeAnimation(NativeAnimation.Odf, effectPar.ToString(SaveOptions.DisableFormatting), effect.Category, effect.Preset, effect.Subtype, effect.DurationMs, effect.Repeat, effect.MotionPath, behaviours) }
                : effect);
        }

        return effects;
    }

    private string? TriggerShape(XElement effect) =>
        effect.Ancestors(Anim + "seq").FirstOrDefault(seq => (string?)seq.Attribute(Pres + "node-type") == "interactive-sequence")?
            .Attribute(Smil + "begin")?.Value.Split(';').Select(value => value.Trim())
            .Where(value => value.EndsWith(".click", StringComparison.Ordinal) && value.Length > ".click".Length)
            .Select(value => value[..^".click".Length])
            .FirstOrDefault();

    private string Time(int milliseconds) =>
        (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "s";

    private int RepeatCount(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var count) && count >= 1
            ? (int)Math.Min(100, Math.Round(count))
            : 1;

    private int Seconds(string? value, int fallback) =>
        value is not null && double.TryParse(value.TrimEnd('s', 'S'), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && double.IsFinite(seconds) && seconds >= 0
            ? (int)Math.Min(AnimationEffect.LongestTimingMs, Math.Round(seconds * 1000))
            : fallback;
}

internal sealed class OdfAnimationParagraphs
{
    private static readonly XName XmlId = OdfPackage.Namespaces.Xml + "id";
    private static readonly XName DrawId = OdfPackage.Namespaces.Draw + "id";
    private static readonly XName TextId = OdfPackage.Namespaces.Text + "id";
    private static readonly XName TextP = OdfPackage.Namespaces.Text + "p";
    private static readonly XName TextH = OdfPackage.Namespaces.Text + "h";

    public string Target(AnimationEffect effect) => effect.Paragraph is { } index ? $"{effect.TargetId}.p{index}" : effect.TargetId;

    public void Identify(XElement page, Slide slide)
    {
        foreach (var effect in slide.Animations.Where(effect => effect.Paragraph is not null))
        {
            var frame = page.Descendants().FirstOrDefault(element => (string?)element.Attribute(XmlId) == effect.TargetId || (string?)element.Attribute(DrawId) == effect.TargetId);
            var paragraphs = frame?.Descendants().Where(element => element.Name == TextP || element.Name == TextH).ToList();
            if (paragraphs is null || effect.Paragraph!.Value >= paragraphs.Count) continue;
            var paragraph = paragraphs[effect.Paragraph.Value];
            var id = Target(effect);
            paragraph.SetAttributeValue(XmlId, id);
            paragraph.SetAttributeValue(TextId, id);
        }
    }
}

