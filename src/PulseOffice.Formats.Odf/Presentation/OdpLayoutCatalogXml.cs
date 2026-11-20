using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpLayoutCatalogXml
{
    private static readonly XNamespace Pulse = "urn:pulseoffice:presentation-master";
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public bool Needed(SlideMaster master)
    {
        ArgumentNullException.ThrowIfNull(master);
        return master.Layouts.GroupBy(layout => layout.Kind).Any(group => group.Count() > 1)
            || master.Layouts.Any(layout => !string.Equals(layout.Name, Standard(layout.Kind)?.Name, StringComparison.Ordinal));
    }

    private readonly SlideMaster _standard = new MasterFactory().CreateStandard(new Theme { Name = "Light" });

    private SlideLayoutTemplate? Standard(SlideLayout kind) =>
        _standard.Layouts.FirstOrDefault(layout => layout.Kind == kind);

    public void Write(XElement masterPage, SlideMaster master, Action<XElement, SlideLayoutTemplate> decorate)
    {
        ArgumentNullException.ThrowIfNull(masterPage);
        ArgumentNullException.ThrowIfNull(decorate);
        if (!Needed(master)) return;
        var firsts = master.Layouts.DistinctBy(layout => layout.Kind).ToHashSet(ReferenceEqualityComparer.Instance);
        masterPage.Add(new XElement(Pulse + "layouts",
            master.Layouts.Select(layout =>
            {
                var element = new XElement(Pulse + "layout",
                    new XAttribute(Pulse + "id", layout.Id),
                    new XAttribute(Pulse + "name", layout.Name),
                    new XAttribute(Pulse + "kind", layout.Kind.ToString()),
                    new XAttribute(Pulse + "hide-master-objects", layout.HideMasterObjects ? "true" : "false"),
                    layout.Placeholders.Select(placeholder => new XElement(Pulse + "placeholder",
                        new XAttribute(Pulse + "role", placeholder.Role.ToString()),
                        new XAttribute(Pulse + "x", Number(placeholder.Bounds.Left)),
                        new XAttribute(Pulse + "y", Number(placeholder.Bounds.Top)),
                        new XAttribute(Pulse + "width", Number(placeholder.Bounds.Width)),
                        new XAttribute(Pulse + "height", Number(placeholder.Bounds.Height)))));
                if (layout.Background is { } background) element.Add(new XAttribute(Pulse + "background", background.ToString()));
                if (!firsts.Contains(layout))
                {
                    element.Add(new XAttribute(Pulse + "own-decoration", "true"));
                    decorate(element, layout);
                }
                return element;
            })));
    }

    public void WriteSlide(XElement page, SlideMaster master, Slide slide)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (slide.LayoutId is { } id && Needed(master) && master.Layouts.Any(layout => string.Equals(layout.Id, id, StringComparison.Ordinal)))
            page.Add(new XAttribute(Pulse + "layout-id", id));
    }

    public string? SlideLayoutId(XElement page) => (string?)page.Attribute(Pulse + "layout-id");

    public SlideMaster Read(XElement masterPage, SlideMaster master, Func<XElement, string, IReadOnlyList<SlideElement>> decoration)
    {
        ArgumentNullException.ThrowIfNull(masterPage);
        ArgumentNullException.ThrowIfNull(decoration);
        if (masterPage.Element(Pulse + "layouts") is not { } catalog) return master;
        var layouts = new List<SlideLayoutTemplate>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in catalog.Elements(Pulse + "layout"))
        {
            if ((string?)entry.Attribute(Pulse + "id") is not { Length: > 0 } id || !ids.Add(id)) continue;
            if (!Enum.TryParse<SlideLayout>((string?)entry.Attribute(Pulse + "kind"), out var kind) || !Enum.IsDefined(kind)) continue;
            var basis = master.Layouts.FirstOrDefault(layout => string.Equals(layout.Id, id, StringComparison.Ordinal))
                ?? master.Template(kind);
            var seen = new Dictionary<PlaceholderRole, int>();
            var placeholders = entry.Elements(Pulse + "placeholder")
                .Select(placeholder => Placeholder(placeholder, basis, seen))
                .OfType<PlaceholderTemplate>()
                .ToList();
            layouts.Add(basis with
            {
                Id = id,
                Name = (string?)entry.Attribute(Pulse + "name") ?? basis.Name,
                Kind = kind,
                HideMasterObjects = (string?)entry.Attribute(Pulse + "hide-master-objects") == "true",
                Background = Enum.TryParse<ColorRole>((string?)entry.Attribute(Pulse + "background"), out var role) && Enum.IsDefined(role) ? role : null,
                Placeholders = placeholders,
                Objects = (string?)entry.Attribute(Pulse + "own-decoration") == "true" ? decoration(entry, id)
                    : basis.Kind == kind ? basis.Objects : [],
            });
        }
        return layouts.Count == 0 ? master : master with { Layouts = layouts };
    }

    private PlaceholderTemplate? Placeholder(XElement entry, SlideLayoutTemplate basis, Dictionary<PlaceholderRole, int> seen)
    {
        if (!Enum.TryParse<PlaceholderRole>((string?)entry.Attribute(Pulse + "role"), out var role) || !Enum.IsDefined(role)) return null;
        var occurrence = seen[role] = seen.GetValueOrDefault(role) + 1;
        var model = basis.Placeholders.Where(placeholder => placeholder.Role == role).ElementAtOrDefault(occurrence - 1)
            ?? basis.Placeholders.FirstOrDefault(placeholder => placeholder.Role == role);
        var bounds = new DocumentRect(Twips(entry, "x"), Twips(entry, "y"), Twips(entry, "width"), Twips(entry, "height"));
        return model is null
            ? new PlaceholderTemplate(role, bounds, new PulseOffice.Text.Rich.TextParagraph(string.Empty))
            : model with { Bounds = bounds };
    }

    private Twip Twips(XElement entry, string name) =>
        int.TryParse((string?)entry.Attribute(Pulse + name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? new Twip(value) : Twip.Zero;

    private string Number(Twip value) => value.Value.ToString(CultureInfo.InvariantCulture);

}
