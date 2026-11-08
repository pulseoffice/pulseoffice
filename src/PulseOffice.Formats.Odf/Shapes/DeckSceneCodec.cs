using System.Xml;
using System.Xml.Linq;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class DeckSceneCodec : DeckObjectCodec
{
    private const int MaximumMarkupLength = 4 * 1024 * 1024;
    private static readonly XNamespace Dr3d = OdfPackage.Namespaces.Dr3d;
    private readonly OdfGraphicStyleInheritance _inheritance = new();

    public XName Element => Dr3d + "scene";

    public ObjectElement Read(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions)
    {
        ArgumentNullException.ThrowIfNull(node);
        var styles = node.DescendantsAndSelf()
            .Select(element => (string?)element.Attribute(Draw + "style-name"))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(name => Flattened(name, definitions))
            .OfType<string>()
            .ToList();
        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(node),
            Content = new Scene3DObject
            {
                Id = id,
                Markup = node.ToString(SaveOptions.DisableFormatting),
                Styles = styles,
            },
        };
    }

    private string? Flattened(string name, IReadOnlyDictionary<string, XElement> definitions)
    {
        if (!definitions.TryGetValue(name, out var style) && !definitions.TryGetValue(_inheritance.Key(name), out style)) return null;
        var effective = _inheritance.Effective(style, definitions);
        if (ReferenceEquals(effective, style)) return style.ToString(SaveOptions.DisableFormatting);
        var copy = new XElement(effective);
        copy.SetAttributeValue(Style + "name", name);
        copy.Attribute(Style + "parent-style-name")?.Remove();
        copy.Attribute(Style + "display-name")?.Remove();
        return copy.ToString(SaveOptions.DisableFormatting);
    }

    public bool Valid(Scene3DObject scene) => Markup(scene) is not null;

    public XElement? Build(ObjectElement element, Scene3DObject scene, Func<string> nextStyle, Action<XElement> addStyle)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(scene);
        if (Markup(scene) is not { } node) return null;

        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in scene.Styles.Select(Parse).OfType<XElement>().Where(GraphicStyle).Where(Inert))
        {
            var name = nextStyle();
            renames[(string)style.Attribute(Style + "name")!] = name;
            style.SetAttributeValue(Style + "name", name);
            style.Attribute(Style + "parent-style-name")?.Remove();
            addStyle(style);
        }

        foreach (var styled in node.DescendantsAndSelf())
            if ((string?)styled.Attribute(Draw + "style-name") is { } used)
                styled.SetAttributeValue(Draw + "style-name", renames.TryGetValue(used, out var renamed) ? renamed : null);

        node.Attribute(Draw + "transform")?.Remove();
        node.SetAttributeValue(Xml + "id", element.Id);
        node.SetAttributeValue(Draw + "id", element.Id);
        node.SetAttributeValue(Svg + "x", Cm(element.Bounds.Left));
        node.SetAttributeValue(Svg + "y", Cm(element.Bounds.Top));
        node.SetAttributeValue(Svg + "width", Cm(element.Bounds.Width));
        node.SetAttributeValue(Svg + "height", Cm(element.Bounds.Height));
        return node;
    }

    private XElement? Markup(Scene3DObject scene)
    {
        if (scene.Format != Scene3DObject.OdfFormat || scene.Markup.Length > MaximumMarkupLength) return null;
        if (Parse(scene.Markup) is not { } node || node.Name != Element) return null;
        if (!node.Descendants().All(child => SceneNamespace(child.Name.Namespace))) return null;
        node.DescendantsAndSelf().Attributes().Where(attribute => !attribute.IsNamespaceDeclaration && !SceneAttribute(attribute.Name)).Remove();
        return node;
    }

    private bool SceneNamespace(XNamespace space) => space == Dr3d || space == Draw || space == Svg;

    private bool SceneAttribute(XName name) => !_active.Contains(name.Namespace) && name != Xml + "base";

    private bool Inert(XElement style) =>
        !style.DescendantsAndSelf().Any(element => _active.Contains(element.Name.Namespace)
            || element.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration && (_active.Contains(attribute.Name.Namespace) || attribute.Name == Xml + "base")));

    private readonly HashSet<XNamespace> _active = [OdfPackage.Namespaces.Office, OdfPackage.Namespaces.Script, OdfPackage.Namespaces.Xlink, OdfPackage.Namespaces.Dom];

    private bool GraphicStyle(XElement style) =>
        style.Name == Style + "style" && (string?)style.Attribute(Style + "family") == "graphic" && style.Attribute(Style + "name") is not null;

    private XElement? Parse(string markup)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(markup), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumMarkupLength });
            return XElement.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
