using PulseOffice.Formats;
using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxAnimationMapper
{
    private const int MaxTimingMs = AnimationEffect.LongestTimingMs;

    private readonly AnimationClickGroups _groups = new();
    private readonly TokenMap<AnimationCategory> _categories = new(AnimationCategory.MotionPath, "path",
        (AnimationCategory.Entrance, "entr"), (AnimationCategory.Emphasis, "emph"), (AnimationCategory.Exit, "exit"), (AnimationCategory.MotionPath, "path"), (AnimationCategory.Media, "mediacall"));

    private readonly IAnimationPresetRegistry _presets;
    private readonly IAnimationSubtypeTable _subtypes;
    private readonly IAnimationBehaviourResolver _behaviours;

    public PptxAnimationMapper(IAnimationBehaviourResolver? behaviours = null, IAnimationPresetRegistry? presets = null, IAnimationSubtypeTable? subtypes = null)
    {
        _subtypes = subtypes ?? new AnimationSubtypeTable();
        _behaviours = behaviours ?? new AnimationBehaviourResolver();
        _presets = presets ?? new AnimationPresetRegistry();
    }

    private readonly PptxAnimationBehaviourCodec _codec = new();

    private const string PNs = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private const string ANs = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private readonly PptxAnimationParagraphs _paragraphs = new();

    private readonly PptxMediaTiming _media = new();

    private readonly PptxAnimationSound _sound = new();

    public P.Timing? ToTiming(Slide slide, IReadOnlyDictionary<string, uint> spidByElement, Func<string?, PptxSoundReference?>? sounds = null)
    {
        sounds ??= _ => null;
        var media = _media.Nodes(slide, spidByElement, 200000);
        if (slide.Animations.Count == 0)
            return media.Length == 0 ? null : new P.Timing(
                $"<p:timing xmlns:p=\"{PNs}\" xmlns:a=\"{ANs}\"><p:tnLst>" +
                "<p:par><p:cTn id=\"1\" dur=\"indefinite\" restart=\"never\" nodeType=\"tmRoot\"><p:childTnLst>" +
                media + "</p:childTnLst></p:cTn></p:par></p:tnLst></p:timing>");

        var id = 2;
        var triggered = slide.Animations
            .Where(effect => effect.IsTriggered && spidByElement.ContainsKey(effect.TriggerShapeId!))
            .ToList();
        var mainSeq = Groups([.. slide.Animations.Except(triggered)], spidByElement, sounds, ref id, "indefinite");
        var interactive = new StringBuilder();
        foreach (var sequence in triggered.GroupBy(effect => effect.TriggerShapeId!))
        {
            var triggerSpid = spidByElement[sequence.Key];
            var sequenceId = id++;
            var body = Groups([.. sequence], spidByElement, sounds, ref id, "0");
            if (body.Length == 0) continue;
            var condition = string.Create(CultureInfo.InvariantCulture,
                $"<p:cond evt=\"onClick\" delay=\"0\"><p:tgtEl><p:spTgt spid=\"{triggerSpid}\"/></p:tgtEl></p:cond>");
            interactive.Append(CultureInfo.InvariantCulture,
                $"<p:seq concurrent=\"1\" nextAc=\"seek\"><p:cTn id=\"{sequenceId}\" restart=\"whenNotActive\" fill=\"hold\" evtFilter=\"cancelBubble\" nodeType=\"interactiveSeq\">");
            interactive.Append("<p:stCondLst>").Append(condition).Append("</p:stCondLst>");
            interactive.Append("<p:endSync evt=\"end\" delay=\"0\"><p:rtn val=\"all\"/></p:endSync><p:childTnLst>");
            interactive.Append(body);
            interactive.Append("</p:childTnLst></p:cTn><p:nextCondLst>").Append(condition).Append("</p:nextCondLst></p:seq>");
        }

        var main = mainSeq.Length == 0 && interactive.Length > 0 ? string.Empty :
            "<p:seq concurrent=\"1\" nextAc=\"seek\"><p:cTn id=\"100000\" dur=\"indefinite\" nodeType=\"mainSeq\"><p:childTnLst>" +
            mainSeq +
            "</p:childTnLst></p:cTn>" +
            "<p:prevCondLst><p:cond evt=\"onPrev\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:prevCondLst>" +
            "<p:nextCondLst><p:cond evt=\"onNext\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:nextCondLst>" +
            "</p:seq>";
        var xml =
            $"<p:timing xmlns:p=\"{PNs}\" xmlns:a=\"{ANs}\"><p:tnLst>" +
            "<p:par><p:cTn id=\"1\" dur=\"indefinite\" restart=\"never\" nodeType=\"tmRoot\"><p:childTnLst>" +
            main + interactive + media + "</p:childTnLst></p:cTn></p:par></p:tnLst></p:timing>";

        return new P.Timing(xml);
    }

    private StringBuilder Groups(IReadOnlyList<AnimationEffect> animations, IReadOnlyDictionary<string, uint> spidByElement,
        Func<string?, PptxSoundReference?> sounds, ref int id, string firstDelay)
    {
        var sequence = new StringBuilder();
        foreach (var group in _groups.Of(animations))
        {
            var groupStart = id;
            var clickId = id++;
            var withId = id++;
            var effects = new StringBuilder();
            var targeted = new HashSet<(uint, int?, AnimationCategory, string)>();

            foreach (var effect in group)
            {
                if (!spidByElement.TryGetValue(effect.TargetId, out var spid)) continue;
                if (!targeted.Add((spid, effect.Paragraph, effect.Category, effect.Preset))) continue;

                if (effect.CurrentNative is { Format: NativeAnimation.Ooxml } native)
                {
                    effects.Append(Native(native, effect, spid, ref id, sounds(effect.SoundPath)));
                    continue;
                }

                var behaviours = _behaviours.Resolve(effect);
                if (!_codec.Children(behaviours, spid, new TimeNodeIdSequence(0)).Any())
                    behaviours = _behaviours.Fallback(effect);
                var ids = new TimeNodeIdSequence(id);
                System.Xml.Linq.XNamespace p = PNs;
                var head = new System.Xml.Linq.XElement(p + "cTn",
                    new System.Xml.Linq.XAttribute("id", ids.Next()),
                    new System.Xml.Linq.XAttribute("presetID", _presets.OoxmlPresetId(effect.Category, effect.Preset)),
                    new System.Xml.Linq.XAttribute("presetClass", _categories.Token(effect.Category)),
                    new System.Xml.Linq.XAttribute("presetSubtype", Subtype(effect)),
                    effect.Repeat > 1 ? new System.Xml.Linq.XAttribute("repeatCount", effect.Repeat * 1000) : null,
                    _codec.RootAttributes(behaviours),
                    new System.Xml.Linq.XAttribute("fill", Fill(effect)),
                    new System.Xml.Linq.XAttribute("grpId", "0"),
                    new System.Xml.Linq.XAttribute("nodeType", NodeType(effect)),
                    new System.Xml.Linq.XElement(p + "stCondLst", new System.Xml.Linq.XElement(p + "cond", new System.Xml.Linq.XAttribute("delay", effect.DelayMs))),
                    _codec.Iterate(behaviours),
                    new System.Xml.Linq.XElement(p + "childTnLst", _codec.Children(behaviours, spid, ids)));
                if (sounds(effect.SoundPath) is { } sound) head.Add(_sound.Build(sound, ids.Next()));
                var effectPar = new System.Xml.Linq.XElement(p + "par",
                    new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "p", PNs),
                    new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "a", ANs),
                    head);
                _paragraphs.Target(effectPar, effect.Paragraph);
                effects.Append(effectPar.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
                id = ids.Peek;
            }

            if (effects.Length == 0)
            {
                id = groupStart;
                continue;
            }
            var delay = sequence.Length == 0 ? firstDelay : "indefinite";
            sequence.Append(CultureInfo.InvariantCulture,
                $"<p:par><p:cTn id=\"{clickId}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"{delay}\"/></p:stCondLst><p:childTnLst>");
            sequence.Append(CultureInfo.InvariantCulture,
                $"<p:par><p:cTn id=\"{withId}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>");
            sequence.Append(effects);
            sequence.Append("</p:childTnLst></p:cTn></p:par>");
            sequence.Append("</p:childTnLst></p:cTn></p:par>");
        }
        return sequence;
    }

    private string NodeType(AnimationEffect effect) => effect.Trigger switch
    {
        AnimationTrigger.WithPrevious => "withEffect",
        AnimationTrigger.AfterPrevious => "afterEffect",
        _ => "clickEffect",
    };

    private string Fill(AnimationEffect effect) => effect.Rewind ? "remove" : "hold";

    private int Subtype(AnimationEffect effect) =>
        _subtypes.ToOoxml(effect.Category, int.Parse(_presets.OoxmlPresetId(effect.Category, effect.Preset), CultureInfo.InvariantCulture), effect.Subtype);

    private string Native(NativeAnimation native, AnimationEffect effect, uint spid, ref int id, PptxSoundReference? sound)
    {
        System.Xml.Linq.XNamespace p = PNs;
        var par = System.Xml.Linq.XElement.Parse(native.Xml);
        _sound.Strip(par);
        if (sound is not null) par.Element(p + "cTn")?.Add(_sound.Build(sound, 0));
        foreach (var node in par.DescendantsAndSelf(p + "cTn").ToList())
            node.SetAttributeValue("id", id++);
        foreach (var target in par.Descendants(p + "spTgt").ToList())
            target.SetAttributeValue("spid", spid);
        _paragraphs.Target(par, effect.Paragraph);
        if (par.Element(p + "cTn") is { } head)
        {
            head.SetAttributeValue("presetID", _presets.OoxmlPresetId(effect.Category, effect.Preset));
            head.SetAttributeValue("presetClass", _categories.Token(effect.Category));
            head.SetAttributeValue("presetSubtype", Subtype(effect));
            head.SetAttributeValue("nodeType", NodeType(effect));
            head.SetAttributeValue("fill", Fill(effect));
            if (head.Element(p + "stCondLst")?.Element(p + "cond") is { } condition)
                condition.SetAttributeValue("delay", effect.DelayMs);
        }

        return par.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    public IReadOnlyList<AnimationEffect> FromTiming(
        P.Timing? timing, Func<uint, IReadOnlyList<string>> resolve, string slideId, Func<OpenXmlElement, string?>? sounds = null)
    {
        var effects = new List<AnimationEffect>();
        if (timing is null) return effects;

        var index = 0;
        foreach (var cTn in timing.Descendants<P.CommonTimeNode>())
        {
            if (cTn.PresetClass?.Value is not { } presetClass) continue;

            var spTgt = cTn.Descendants<P.SetBehavior>().FirstOrDefault()?
                .Descendants<P.ShapeTarget>().FirstOrDefault()
                ?? cTn.Descendants<P.ShapeTarget>().FirstOrDefault();

            if (spTgt?.ShapeId?.Value is not { } spidStr
                || !uint.TryParse(spidStr, out var spid)
                || resolve(spid) is not [var elementId, ..] targets)
                continue;

            if (_categories.Find(cTn.PresetClass?.InnerText) is not { } category) continue;
            var durations = cTn.ChildTimeNodeList?.Descendants<P.CommonTimeNode>()
                .Select(node => int.TryParse(node.Duration?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0)
                .Where(value => value > 1)
                .ToList() ?? [];

            var presetId = cTn.PresetId?.Value ?? 0;
            var single = cTn.Descendants<P.ShapeTarget>().Select(target => target.ShapeId?.Value).Distinct().Count() == 1;
            var xml = single && cTn.Parent is P.ParallelTimeNode parallel ? Qualified(parallel) : null;
            var behaviours = xml is null ? null : Behaviours(xml);
            var effect = new AnimationEffect
            {
                Id = $"{slideId}.anim{++index}",
                TargetId = elementId,
                Paragraph = targets.Count == 1 ? _paragraphs.Read(spTgt) : null,
                Category = category,
                Preset = _presets.FromOoxml(category, presetId.ToString(CultureInfo.InvariantCulture)),
                Subtype = _subtypes.FromOoxml(category, presetId, cTn.PresetSubtype?.Value ?? 0),
                Trigger = cTn.NodeType?.Value switch
                {
                    var n when n == P.TimeNodeValues.WithEffect => AnimationTrigger.WithPrevious,
                    var n when n == P.TimeNodeValues.AfterEffect => AnimationTrigger.AfterPrevious,
                    _ => AnimationTrigger.OnClick,
                },
                DurationMs = behaviours is { NaturalDuration: > 0 and var natural } && double.IsFinite(natural) ? (int)Math.Min(Math.Round(natural * 1000), MaxTimingMs)
                    : durations.Count > 0 ? Math.Min(durations.Max(), MaxTimingMs) : 500,
                MotionPath = cTn.Descendants<P.AnimateMotion>().FirstOrDefault()?.Path?.Value,
                Repeat = RepeatCount(cTn.RepeatCount?.Value),
                DelayMs = cTn.StartConditionList?.GetFirstChild<P.Condition>()?.Delay?.Value is { } d
                    && int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delay) && delay >= 0 ? Math.Min(delay, MaxTimingMs) : 0,
                SoundPath = sounds is not null && _sound.Target(cTn) is { } target ? sounds(target) : null,
                TriggerShapeId = TriggerShape(cTn, resolve),
                Rewind = cTn.Fill?.Value == P.TimeNodeFillValues.Remove,
            };
            var read = xml is not null
                ? effect with { Native = new NativeAnimation(NativeAnimation.Ooxml, xml, effect.Category, effect.Preset, effect.Subtype, effect.DurationMs, effect.Repeat, effect.MotionPath, behaviours) }
                : effect;
            effects.Add(read);
            for (var member = 1; member < targets.Count; member++)
                effects.Add(read with { Id = $"{read.Id}.{member}", TargetId = targets[member], Trigger = AnimationTrigger.WithPrevious, DelayMs = read.DelayMs });
        }

        return effects;
    }

    private string? TriggerShape(P.CommonTimeNode effect, Func<uint, IReadOnlyList<string>> resolve) =>
        effect.Ancestors<P.CommonTimeNode>().FirstOrDefault(node => node.NodeType?.Value == P.TimeNodeValues.InteractiveSequence)?
            .StartConditionList?.Elements<P.Condition>()
            .Select(condition => condition.TargetElement?.ShapeTarget?.ShapeId?.Value)
            .Select(spid => uint.TryParse(spid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && resolve(value) is [var id, ..] ? id : null)
            .FirstOrDefault(id => id is not null);

    private AnimationBehaviours? Behaviours(string xml) =>
        System.Xml.Linq.XElement.Parse(xml).Element(_codec.PresentationNamespace + "cTn") is { } head ? _codec.Read(head) : null;

    private string? Qualified(OpenXmlElement element)
    {
        var clone = element.CloneNode(true);
        foreach (var (prefix, uri) in new[] { ("p", PNs), ("a", ANs) })
            if (clone.LookupNamespace(prefix) is null)
                clone.AddNamespaceDeclaration(prefix, uri);
        try
        {
            return System.Xml.Linq.XElement.Parse(clone.OuterXml).ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private string RepeatAttribute(int repeat) =>
        repeat > 1 ? string.Create(CultureInfo.InvariantCulture, $" repeatCount=\"{repeat * 1000}\"") : string.Empty;

    private int RepeatCount(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thousandths) && thousandths >= 1000
            ? Math.Min(100, (int)Math.Round(thousandths / 1000.0))
            : 1;
}

internal sealed class PptxAnimationParagraphs
{
    private static readonly System.Xml.Linq.XNamespace Pml = "http://schemas.openxmlformats.org/presentationml/2006/main";

    public int? Read(P.ShapeTarget target) =>
        target.GetFirstChild<P.TextElement>()?.GetFirstChild<P.ParagraphIndexRange>() is { Start.Value: var start, End.Value: var end } && start == end
            ? (int)start
            : null;

    public void Target(System.Xml.Linq.XElement effect, int? paragraph)
    {
        foreach (var target in effect.Descendants(Pml + "spTgt"))
        {
            foreach (var old in target.Elements(Pml + "txEl").ToList()) old.Remove();
            if (paragraph is { } index)
                target.Add(new System.Xml.Linq.XElement(Pml + "txEl", new System.Xml.Linq.XElement(Pml + "pRg",
                    new System.Xml.Linq.XAttribute("st", index), new System.Xml.Linq.XAttribute("end", index))));
        }
    }
}
