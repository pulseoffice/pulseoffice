using PulseOffice.Formats;
using DocumentFormat.OpenXml;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxTransitionMapper
{
    private const int LongestTimingMs = 3_600_000;

    private readonly IReadOnlyList<PptxTransitionEffect> _effects;
    private readonly INativeTransitionTable _natives;

    public PptxTransitionMapper() : this(new NativeTransitionTable())
    {
    }

    public PptxTransitionMapper(INativeTransitionTable natives)
    {
        _natives = natives;
        var directions = new TransitionDirections();
        _effects =
        [
            new FadeTransition(TransitionKind.Reveal, throughBlack: true),
            new FadeTransition(TransitionKind.Fade, throughBlack: false),
            new PlainTransition<P.CutTransition>(TransitionKind.Cut, () => new P.CutTransition()),
            new PlainTransition<P.DissolveTransition>(TransitionKind.Dissolve, () => new P.DissolveTransition()),
            new SlidingTransition<P.PushTransition>(TransitionKind.Push, directions, (t, d) => t.Direction = d, t => t.Direction),
            new SlidingTransition<P.WipeTransition>(TransitionKind.Wipe, directions, (t, d) => t.Direction = d, t => t.Direction),
            new LetteredTransition<P.CoverTransition>(TransitionKind.Cover, directions, (t, d) => t.Direction = d, t => t.Direction?.Value),
            new LetteredTransition<P.PullTransition>(TransitionKind.Uncover, directions, (t, d) => t.Direction = d, t => t.Direction?.Value),
            new PlainTransition<P.SplitTransition>(TransitionKind.Split, () => new P.SplitTransition { Orientation = P.DirectionValues.Vertical }),
            new PlainTransition<P.ZoomTransition>(TransitionKind.Zoom, () => new P.ZoomTransition { Direction = P.TransitionInOutDirectionValues.In }),
        ];
    }

    public P.Transition? ToTransition(SlideTransition transition)
    {
        if (transition.IsEmpty) return null;

        var element = new P.Transition
        {
            Speed = transition.DurationMs switch
            {
                <= 300 => P.TransitionSpeedValues.Fast,
                >= 800 => P.TransitionSpeedValues.Slow,
                _ => P.TransitionSpeedValues.Medium,
            },
        };

        if (transition.Advance == AdvanceMode.AfterTime)
        {
            element.AdvanceOnClick = false;
            element.AdvanceAfterTime = transition.AdvanceAfterMs.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            element.AdvanceOnClick = true;
        }

        if (!transition.IsNone) element.Append(Effect(transition));
        return element;
    }

    public bool ReachesOoxml(SlideTransition transition) =>
        transition.CurrentNative is not { Format: NativeTransition.Odf } native || _natives.OoxmlFor(native.Payload) is not null;

    private string? NativeXml(SlideTransition transition) => transition.CurrentNative switch
    {
        { Format: NativeTransition.Ooxml } native => native.Payload,
        { Format: NativeTransition.Odf } native => _natives.OoxmlFor(native.Payload),
        _ => null,
    };

    private void ReplaceEffect(P.Transition transition, string xml)
    {
        foreach (var effect in transition.ChildElements.Where(IsEffect).ToList())
            effect.Remove();
        var holder = new P.Transition($"<p:transition xmlns:p=\"{PMain}\">{xml}</p:transition>");
        transition.PrependChild(holder.FirstChild!.CloneNode(true));
    }

    private bool IsEffect(OpenXmlElement element) =>
        element is not (P.SoundAction or P.ExtensionListWithModification) && element.LocalName != "extLst";

    private string? NativeEffect(P.Slide? slide)
    {
        var transition = slide?.Descendants<AlternateContentChoice>().SelectMany(choice => choice.Elements<P.Transition>()).FirstOrDefault()
            ?? Find(slide);
        return transition?.ChildElements.FirstOrDefault(IsEffect) is { } effect
            ? System.Xml.Linq.XElement.Parse(Qualified(effect)).ToString(System.Xml.Linq.SaveOptions.DisableFormatting)
            : null;
    }

    private bool Standardized(string native) =>
        _effects.Any(effect => System.Xml.Linq.XElement.Parse(Qualified(effect.Build(TransitionDirection.None))).Name == System.Xml.Linq.XElement.Parse(native).Name);

    private string Standard(SlideTransition transition) => Qualified(Effect(transition));

    private string Qualified(OpenXmlElement effect)
    {
        var clone = effect.CloneNode(true);
        if (clone.LookupNamespace(clone.Prefix) is null)
            clone.AddNamespaceDeclaration(clone.Prefix, clone.NamespaceUri);
        return clone.OuterXml;
    }

    private OpenXmlElement Effect(SlideTransition transition) =>
        (_effects.FirstOrDefault(effect => effect.Kind == transition.Kind) ?? _effects[0]).Build(transition.Direction);

    public P.Transition? Find(P.Slide? slide) =>
        slide?.GetFirstChild<P.Transition>()
        ?? slide?.Elements<AlternateContent>().SelectMany(content => content.Elements<AlternateContentFallback>()).SelectMany(fallback => fallback.Elements<P.Transition>()).FirstOrDefault()
        ?? slide?.Descendants<P.Transition>().LastOrDefault();

    private const string PMain = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private const string P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    public void Append(P.Slide slide, SlideTransition transition)
    {
        if (ToTransition(transition) is not { } fallback) return;

        var choice = (P.Transition)fallback.CloneNode(true);
        if (NativeXml(transition) is { } native)
        {
            ReplaceEffect(choice, native);
            if (System.Xml.Linq.XElement.Parse(native).Name.NamespaceName != P14)
                ReplaceEffect(fallback, native);
        }
        choice.SetAttribute(new OpenXmlAttribute("p14", "dur", P14,
            Math.Max(1, transition.DurationMs).ToString(System.Globalization.CultureInfo.InvariantCulture)));

        slide.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
        slide.AddNamespaceDeclaration("p14", P14);
        slide.Append(new AlternateContent(
            new AlternateContentChoice(choice) { Requires = "p14" },
            new AlternateContentFallback(fallback)));
    }

    public SlideTransition? FromSlide(P.Slide? slide)
    {
        if (FromTransition(Find(slide)) is not { } transition) return null;

        var exact = slide?.Descendants<P.Transition>()
            .Select(element => element.GetAttributes().FirstOrDefault(a => a.LocalName == "dur" && a.NamespaceUri == P14).Value)
            .FirstOrDefault(value => value is not null);

        if (NativeEffect(slide) is { } native && _natives.Key(native) != _natives.Key(Standard(transition)) && (_natives.Knows(native) || !Standardized(native)))
            transition = transition with { Native = new NativeTransition(NativeTransition.Ooxml, native, transition.Kind, transition.Direction) };

        return int.TryParse(exact, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ms) && ms > 0
            ? transition with { DurationMs = Math.Min(ms, LongestTimingMs) }
            : transition;
    }

    public SlideTransition? FromTransition(P.Transition? transition)
    {
        if (transition is null) return null;

        var (kind, direction) = Read(transition);

        return new SlideTransition
        {
            Kind = kind,
            Direction = direction,
            DurationMs = transition.Speed?.Value switch
            {
                var s when s == P.TransitionSpeedValues.Fast => 300,
                var s when s == P.TransitionSpeedValues.Slow => 900,
                _ => 500,
            },
            Advance = transition.AdvanceAfterTime is not null
                ? AdvanceMode.AfterTime : AdvanceMode.OnClick,
            AdvanceAfterMs = int.TryParse(transition.AdvanceAfterTime?.Value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var ms) ? Math.Clamp(ms, 0, LongestTimingMs) : 0,
        };
    }

    private (TransitionKind, TransitionDirection) Read(P.Transition transition) =>
        !transition.ChildElements.Any(IsEffect) ? (TransitionKind.None, TransitionDirection.None) : transition.ChildElements
            .Select(child => _effects.Select(effect => effect.Read(child)).FirstOrDefault(read => read is not null))
            .FirstOrDefault(read => read is not null)
        ?? (TransitionKind.Fade, TransitionDirection.None);
}

internal sealed class TransitionDirections
{
    public TokenMap<TransitionDirection> Letters { get; } = new(TransitionDirection.None, "l",
        (TransitionDirection.Left, "l"), (TransitionDirection.Right, "r"), (TransitionDirection.Up, "u"), (TransitionDirection.Down, "d"));

    public OoxmlValueMap<TransitionDirection, P.TransitionSlideDirectionValues> Slides { get; } =
        new(TransitionDirection.None, P.TransitionSlideDirectionValues.Left,
            (TransitionDirection.Left, P.TransitionSlideDirectionValues.Left), (TransitionDirection.Right, P.TransitionSlideDirectionValues.Right),
            (TransitionDirection.Up, P.TransitionSlideDirectionValues.Up), (TransitionDirection.Down, P.TransitionSlideDirectionValues.Down));
}

internal abstract class PptxTransitionEffect(TransitionKind kind)
{
    public TransitionKind Kind => kind;

    public abstract OpenXmlElement Build(TransitionDirection direction);

    public abstract (TransitionKind, TransitionDirection)? Read(OpenXmlElement element);
}

internal sealed class FadeTransition(TransitionKind kind, bool throughBlack) : PptxTransitionEffect(kind)
{
    public override OpenXmlElement Build(TransitionDirection direction) =>
        throughBlack ? new P.FadeTransition { ThroughBlack = true } : new P.FadeTransition();

    public override (TransitionKind, TransitionDirection)? Read(OpenXmlElement element) =>
        element is P.FadeTransition fade && (fade.ThroughBlack?.Value == true) == throughBlack ? (Kind, TransitionDirection.None) : null;
}

internal sealed class PlainTransition<T>(TransitionKind kind, Func<T> create) : PptxTransitionEffect(kind) where T : OpenXmlElement
{
    public override OpenXmlElement Build(TransitionDirection direction) => create();

    public override (TransitionKind, TransitionDirection)? Read(OpenXmlElement element) =>
        element is T ? (Kind, TransitionDirection.None) : null;
}

internal sealed class SlidingTransition<T>(TransitionKind kind, TransitionDirections directions, Action<T, EnumValue<P.TransitionSlideDirectionValues>> set, Func<T, EnumValue<P.TransitionSlideDirectionValues>?> get)
    : PptxTransitionEffect(kind) where T : OpenXmlElement, new()
{
    public override OpenXmlElement Build(TransitionDirection direction)
    {
        var element = new T();
        set(element, directions.Slides.Sdk(direction));
        return element;
    }

    public override (TransitionKind, TransitionDirection)? Read(OpenXmlElement element) =>
        element is T typed
            ? (Kind, get(typed)?.Value is { } value ? directions.Slides.Model(value) : TransitionDirection.None)
            : null;
}

internal sealed class LetteredTransition<T>(TransitionKind kind, TransitionDirections directions, Action<T, string> set, Func<T, string?> get)
    : PptxTransitionEffect(kind) where T : OpenXmlElement, new()
{
    public override OpenXmlElement Build(TransitionDirection direction)
    {
        var element = new T();
        set(element, directions.Letters.Token(direction));
        return element;
    }

    public override (TransitionKind, TransitionDirection)? Read(OpenXmlElement element) =>
        element is T typed ? (Kind, directions.Letters.Value(get(typed))) : null;
}
