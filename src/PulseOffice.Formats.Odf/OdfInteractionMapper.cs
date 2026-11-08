using PulseOffice.Formats;
using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdfInteractionMapper
{
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Pres = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Script = OdfPackage.Namespaces.Script;
    private static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;

    private readonly TokenMap<InteractionKind> _actions = new(InteractionKind.None, "none",
        (InteractionKind.NextSlide, "next-page"), (InteractionKind.PreviousSlide, "previous-page"),
        (InteractionKind.FirstSlide, "first-page"), (InteractionKind.LastSlide, "last-page"),
        (InteractionKind.GoToSlide, "show"), (InteractionKind.Hyperlink, "show"),
        (InteractionKind.RunMacro, "execute-macro"), (InteractionKind.PlaySound, "sound"), (InteractionKind.StopShow, "stop"));

    public XElement? Build(InteractiveAction? action)
    {
        if (action is null || action.Kind == InteractionKind.None) return null;

        var listener = new XElement(Pres + "event-listener",
            new XAttribute(Script + "event-name", "dom:click"),
            new XAttribute(Pres + "action", _actions.Token(action.Kind)));

        var href = HrefFor(action);
        if (href is not null) listener.Add(new XAttribute(Xlink + "href", href));

        if (action.SoundPath is { Length: > 0 } sound)
            listener.Add(new XElement(Pres + "sound", new XAttribute(Xlink + "href", sound), new XAttribute(Xlink + "type", "simple"), new XAttribute(Xlink + "show", "new"), new XAttribute(Xlink + "actuate", "onRequest")));

        return new XElement(Office + "event-listeners", listener);
    }

    public InteractiveAction? Read(XElement frame, string targetId)
    {
        var listener = frame.Element(Office + "event-listeners")?.Element(Pres + "event-listener");
        if (listener is null) return null;

        var kind = _actions.Value((string?)listener.Attribute(Pres + "action"));
        if (kind == InteractionKind.None) return null;

        var href = (string?)listener.Attribute(Xlink + "href");
        var sound = (string?)listener.Element(Pres + "sound")?.Attribute(Xlink + "href");

        if (kind == InteractionKind.GoToSlide && href is { } u && !u.StartsWith('#'))
            kind = InteractionKind.Hyperlink;

        return new InteractiveAction
        {
            TargetId = targetId,
            Kind = kind,
            Reference = kind == InteractionKind.GoToSlide && href is { } h && h.StartsWith('#') ? h[1..] : href,
            SoundPath = sound,
        };
    }

    private string? HrefFor(InteractiveAction action) => action.Kind switch
    {
        InteractionKind.GoToSlide when action.Reference is { } reference => "#" + reference,
        InteractionKind.Hyperlink or InteractionKind.RunMacro => action.Reference,
        _ => null,
    };
}
