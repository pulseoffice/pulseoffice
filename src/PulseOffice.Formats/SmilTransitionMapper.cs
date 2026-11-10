using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed class SmilTransitionMapper(INativeTransitionTable natives)
{
    public SmilTransitionMapper() : this(new NativeTransitionTable())
    {
    }

    private readonly IReadOnlyList<(TransitionKind Kind, string Type, string Subtype, bool Reverse)> _kinds =
    [
        (TransitionKind.Fade, "fade", "crossfade", false),
        (TransitionKind.Cut, "fade", "crossfade", false),
        (TransitionKind.Dissolve, "dissolve", "dissolve", false),
        (TransitionKind.Push, "pushWipe", "fromRight", false),
        (TransitionKind.Cover, "slideWipe", "fromRight", false),
        (TransitionKind.Uncover, "slideWipe", "fromLeft", true),
        (TransitionKind.Wipe, "barWipe", "leftToRight", false),
        (TransitionKind.Split, "barnDoorWipe", "vertical", false),
        (TransitionKind.Reveal, "fade", "fadeOverColor", false),
        (TransitionKind.Zoom, "fade", "crossfade", false),
        (TransitionKind.Morph, "fade", "crossfade", false),
    ];

    private readonly DirectionSubtypes _slides = new(
        (TransitionDirection.Left, "fromRight"), (TransitionDirection.Right, "fromLeft"),
        (TransitionDirection.Up, "fromBottom"), (TransitionDirection.Down, "fromTop"));

    private readonly IReadOnlyList<(TransitionDirection Direction, string Subtype, bool Reverse)> _barWipes =
    [
        (TransitionDirection.Right, "leftToRight", false),
        (TransitionDirection.Left, "leftToRight", true),
        (TransitionDirection.Down, "topToBottom", false),
        (TransitionDirection.Up, "topToBottom", true),
    ];

    private readonly DirectionSubtypes _wipes = new(
        (TransitionDirection.Right, "leftToRight"), (TransitionDirection.Left, "rightToLeft"),
        (TransitionDirection.Down, "topToBottom"), (TransitionDirection.Up, "bottomToTop"));

    public SmilTransition ToSmil(SlideTransition transition)
    {
        if (Native(transition) is { } native) return native;
        var entry = _kinds.FirstOrDefault(k => k.Kind == transition.Kind) is { Type: not null } known ? known : _kinds[0];
        if (transition.Kind == TransitionKind.Wipe && _barWipes.FirstOrDefault(wipe => wipe.Direction == transition.Direction) is { Subtype: not null } bar)
            return new SmilTransition(entry.Type, bar.Subtype, bar.Reverse);
        var subtype = DirectionSubtype(transition) ?? entry.Subtype;
        return new SmilTransition(entry.Type, subtype, entry.Reverse);
    }

    public SlideTransition FromSmil(string type, string subtype, bool reverse)
    {
        var entry = _kinds.FirstOrDefault(k => SameType(k.Type, type) && SameType(k.Subtype, subtype) && k.Reverse == reverse);

        if (entry.Type is null)
            entry = _kinds.FirstOrDefault(k => SameType(k.Type, type) && k.Reverse == reverse);

        if (entry.Type is null)
            entry = _kinds.FirstOrDefault(k => SameType(k.Type, type));

        var kind = entry.Type is null ? TransitionKind.Fade : entry.Kind;
        var direction = kind == TransitionKind.Wipe && _barWipes.FirstOrDefault(wipe => SameType(wipe.Subtype, subtype) && wipe.Reverse == reverse) is { Subtype: not null } bar
            ? bar.Direction
            : DirectionFromSubtype(subtype);
        var read = new SlideTransition { Kind = kind, Direction = direction };
        var payload = Payload(new SmilTransition(type, subtype, reverse));
        return payload == Payload(ToSmil(read))
            ? read
            : read with { Native = new NativeTransition(NativeTransition.Odf, payload, kind, direction) };
    }

    public System.Xml.Linq.XNamespace ExtensionNamespace { get; } = "urn:pulseoffice:transition";

    private readonly IReadOnlyList<(TransitionKind Kind, string Token)> _extensionKinds =
        [(TransitionKind.Cut, "cut"), (TransitionKind.Zoom, "zoom"), (TransitionKind.Morph, "morph")];

    public string? ExtensionKind(SlideTransition transition) =>
        Native(transition) is null ? _extensionKinds.FirstOrDefault(entry => entry.Kind == transition.Kind).Token : null;

    public SlideTransition WithExtensionKind(SlideTransition read, string? token) =>
        token is not null && _extensionKinds.FirstOrDefault(entry => entry.Token == token) is { Token: not null } entry && read.CurrentNative is null
            ? read with { Kind = entry.Kind, Direction = TransitionDirection.None }
            : read;

    public bool ReachesOdf(SlideTransition transition) =>
        transition.CurrentNative is not { Format: NativeTransition.Ooxml } native || natives.SmilFor(native.Payload) is not null;

    private SmilTransition? Native(SlideTransition transition) => transition.CurrentNative switch
    {
        { Format: NativeTransition.Odf } native => Parse(native.Payload),
        { Format: NativeTransition.Ooxml } native when natives.SmilFor(native.Payload) is { } smil => Parse(smil),
        _ => null,
    };

    private string Payload(SmilTransition smil) =>
        smil.Reverse ? $"{smil.Type} {smil.Subtype} reverse" : $"{smil.Type} {smil.Subtype}";

    private SmilTransition? Parse(string payload) =>
        payload.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var type, var subtype, ..] parts
            ? new SmilTransition(type, subtype, parts.Length > 2 && parts[2] == "reverse")
            : null;

    private bool SameType(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private string? DirectionSubtype(SlideTransition transition) => transition.Kind switch
    {
        TransitionKind.Push or TransitionKind.Cover or TransitionKind.Uncover => _slides.Find(transition.Direction),
        TransitionKind.Wipe => _barWipes[0].Subtype,
        _ => null,
    };

    private TransitionDirection DirectionFromSubtype(string subtype) =>
        _slides.Value(subtype) is var slide and not TransitionDirection.None ? slide : _wipes.Find(subtype) ?? TransitionDirection.None;
}

internal sealed class DirectionSubtypes(params (TransitionDirection Direction, string Subtype)[] entries)
{
    public string? Find(TransitionDirection direction) =>
        entries.Where(entry => entry.Direction == direction).Select(entry => entry.Subtype).FirstOrDefault();

    public string Token(TransitionDirection direction) => Find(direction) ?? entries[0].Subtype;

    public TransitionDirection Value(string subtype) =>
        entries.Where(entry => entry.Subtype == subtype).Select(entry => entry.Direction).DefaultIfEmpty(TransitionDirection.None).First();

    public TransitionDirection? Find(string subtype) =>
        entries.Where(entry => entry.Subtype == subtype).Select(entry => (TransitionDirection?)entry.Direction).FirstOrDefault();
}
