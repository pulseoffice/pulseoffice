using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpUnsupportedClickActions
{
    private readonly IReadOnlySet<string> _actions = new HashSet<string>(StringComparer.Ordinal) { "execute", "verb" };

    public Diagnostic Loss { get; } = Diagnostic.Warning("ODP216",
        "Click actions that run a program or an object verb are not imported: Deck never starts external programs from a presentation.");

    public bool Present(XContainer? content) =>
        content?.Descendants(OdfPackage.Namespaces.Presentation + "event-listener")
            .Any(listener => (string?)listener.Attribute(OdfPackage.Namespaces.Presentation + "action") is { } action && _actions.Contains(action)) == true;
}
