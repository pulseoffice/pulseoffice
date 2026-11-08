using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging.Legacy;

public interface ILegacyXmlTranslator
{
    bool IsLegacy(XDocument document);

    XDocument Translate(XDocument document);
}

public sealed class LegacyXmlTranslator : ILegacyXmlTranslator
{
    private readonly LegacyVocabulary _vocabulary;
    private readonly IReadOnlyList<ILegacyElementRule> _rules;

    public LegacyXmlTranslator() : this(new LegacyVocabulary())
    {
    }

    public LegacyXmlTranslator(LegacyVocabulary vocabulary)
    {
        _vocabulary = vocabulary;
        var properties = new LegacyPropertyPartition();
        _rules =
        [
            new RenameRule(Office + "script", Office + "scripts"),
            new RenameRule(Office + "events", Office + "event-listeners"),
            new RenameRule(Office + "font-decls", Office + "font-face-decls"),
            new FontDeclarationRule(),
            new RenameRule(Style + "page-master", Style + "page-layout"),
            new RenameRule(Text + "unordered-list", Text + "list"),
            new RenameRule(Text + "ordered-list", Text + "list"),
            new RenameRule(Table + "dependences", Table + "dependencies"),
            new RenameRule(Table + "dependence", Table + "dependency"),
            new AttributeRenameRule(Style + "page-master-name", Style + "page-layout-name"),
            new CellValueRule(),
            new ControlReferenceRule(),
            new FormControlRule(),
            new FormPropertyRule(),
            new PackageReferenceRule(),
            new StylePropertiesRule(properties),
            new FrameRule(),
            new BodyRule(vocabulary),
        ];
    }

    private static XNamespace Office => OdfPackage.Namespaces.Office;
    private static XNamespace Style => OdfPackage.Namespaces.Style;
    private static XNamespace Text => OdfPackage.Namespaces.Text;
    private static XNamespace Table => OdfPackage.Namespaces.Table;

    public bool IsLegacy(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Root is { } root && _vocabulary.IsLegacy(root.Name.Namespace);
    }

    public XDocument Translate(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Root is not { } root) return document;

        var translated = Remap(root);
        foreach (var (prefix, ns) in _vocabulary.Prefixes)
            translated.SetAttributeValue(XNamespace.Xmlns + prefix, ns);
        foreach (var element in translated.DescendantsAndSelf().ToList())
            foreach (var rule in _rules)
                if (rule.Applies(element))
                    rule.Apply(element);

        translated.Attribute(Office + "class")?.Remove();

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), translated);
    }

    private XElement Remap(XElement root)
    {
        var result = Shallow(root);
        var pending = new Stack<(XElement Source, XElement Target)>();
        pending.Push((root, result));
        while (pending.TryPop(out var pair))
            foreach (var node in pair.Source.Nodes())
                switch (node)
                {
                    case XElement child:
                        var copy = Shallow(child);
                        pair.Target.Add(copy);
                        pending.Push((child, copy));
                        break;
                    case XText text:
                        pair.Target.Add(new XText(text));
                        break;
                }

        return result;
    }

    private XElement Shallow(XElement element)
    {
        var result = new XElement(Name(element.Name));
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration) continue;
            result.SetAttributeValue(Name(attribute.Name), attribute.Value);
        }

        return result;
    }

    private XName Name(XName name) => name.Namespace == XNamespace.None ? name : _vocabulary.Map(name.Namespace) + name.LocalName;
}

internal interface ILegacyElementRule
{
    bool Applies(XElement element);

    void Apply(XElement element);
}

internal sealed class RenameRule(XName from, XName to) : ILegacyElementRule
{
    public bool Applies(XElement element) => element.Name == from;

    public void Apply(XElement element) => element.Name = to;
}

internal sealed class ControlReferenceRule : ILegacyElementRule
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Form = OdfPackage.Namespaces.Form;

    public bool Applies(XElement element) => element.Name == Draw + "control" && element.Attribute(Form + "id") is not null;

    public void Apply(XElement element)
    {
        var id = element.Attribute(Form + "id")!;
        id.Remove();
        element.SetAttributeValue(Draw + "control", id.Value);
    }
}

internal sealed class FormControlRule : ILegacyElementRule
{
    private static readonly XNamespace Form = OdfPackage.Namespaces.Form;
    private static readonly XNamespace Xml = OdfPackage.Namespaces.Xml;

    public bool Applies(XElement element) => element.Name == Form + "control" && element.Elements().Count() == 1;

    public void Apply(XElement element)
    {
        var control = element.Elements().Single();
        if (element.Attribute(Form + "name") is { } name) control.SetAttributeValue(Form + "name", name.Value);
        if (element.Attribute(Form + "id") is { } id)
        {
            control.SetAttributeValue(Form + "id", id.Value);
            control.SetAttributeValue(Xml + "id", id.Value);
        }

        if (element.Attribute(Form + "service-name") is { } service)
            control.SetAttributeValue(Form + "control-implementation", "ooo:" + service.Value);
        control.Remove();
        element.ReplaceWith(control);
    }
}

internal sealed class FormPropertyRule : ILegacyElementRule
{
    private static readonly XNamespace Form = OdfPackage.Namespaces.Form;
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;

    public bool Applies(XElement element) =>
        element.Name == Form + "property" && element.Attribute(Form + "property-type") is not null;

    public void Apply(XElement element)
    {
        var type = element.Attribute(Form + "property-type")!;
        type.Remove();
        var valueType = type.Value switch
        {
            "boolean" => "boolean",
            "short" or "int" or "long" or "double" => "float",
            _ => "string",
        };
        element.SetAttributeValue(Office + "value-type", valueType);
        var values = element.Elements(Form + "property-value").ToList();
        if (values.Count == 1)
        {
            var value = values[0].Value;
            element.SetAttributeValue(Office + (valueType switch { "boolean" => "boolean-value", "float" => "value", _ => "string-value" }), value);
            values[0].Remove();
        }
    }
}

internal sealed class PackageReferenceRule : ILegacyElementRule
{
    private static readonly XName Href = OdfPackage.Namespaces.Xlink + "href";
    private static readonly HashSet<string> Embedding = ["image", "object", "object-ole", "fill-image", "background-image", "plugin", "applet"];

    public bool Applies(XElement element) =>
        Embedding.Contains(element.Name.LocalName) && element.Attribute(Href)?.Value is ['#', _, ..];

    public void Apply(XElement element)
    {
        var href = element.Attribute(Href)!;
        href.Value = href.Value[1..];
    }
}

internal sealed class AttributeRenameRule(XName from, XName to) : ILegacyElementRule
{
    public bool Applies(XElement element) => element.Attribute(from) is not null;

    public void Apply(XElement element)
    {
        var attribute = element.Attribute(from)!;
        attribute.Remove();
        element.SetAttributeValue(to, attribute.Value);
    }
}

internal sealed class FontDeclarationRule : ILegacyElementRule
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;

    public bool Applies(XElement element) => element.Name == Style + "font-decl";

    public void Apply(XElement element)
    {
        element.Name = Style + "font-face";
        if (element.Attribute(Fo + "font-family") is not { } family) return;
        family.Remove();
        element.SetAttributeValue(Svg + "font-family", family.Value);
    }
}

internal sealed class CellValueRule : ILegacyElementRule
{
    private static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;

    private static readonly string[] ValueAttributes =
        ["value-type", "value", "currency", "date-value", "time-value", "boolean-value", "string-value"];

    public bool Applies(XElement element) =>
        element.Name == Table + "table-cell" || element.Name == Table + "covered-table-cell" || element.Name == Table + "change-track-table-cell";

    public void Apply(XElement element)
    {
        foreach (var local in ValueAttributes)
        {
            if (element.Attribute(Table + local) is not { } attribute) continue;
            attribute.Remove();
            element.SetAttributeValue(Office + local, attribute.Value);
        }

        if (element.Attribute(Office + "value-type") is null && element.Elements(OdfPackage.Namespaces.Text + "p").Any())
            element.SetAttributeValue(Office + "value-type", "string");

        if (element.Attribute(Table + "validation-name") is { } validation)
        {
            validation.Remove();
            element.SetAttributeValue(Table + "content-validation-name", validation.Value);
        }
    }
}

internal sealed class BodyRule(LegacyVocabulary vocabulary) : ILegacyElementRule
{
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;

    public bool Applies(XElement element) => element.Name == Office + "body" && element.Parent is not null;

    public void Apply(XElement element)
    {
        var documentClass = element.AncestorsAndSelf().Last().Attribute(Office + "class")?.Value ?? ClassOf(element);
        var nodes = element.Nodes().ToList();
        element.RemoveNodes();
        element.Add(new XElement(Office + vocabulary.BodyFor(documentClass), nodes));
    }

    private string ClassOf(XElement body) =>
        body.Elements().FirstOrDefault()?.Name.LocalName switch
        {
            "page" => "presentation",
            "chart" => "chart",
            "table" => "spreadsheet",
            _ => body.Elements().Any(child => child.Name.LocalName == "page") ? "presentation" : "spreadsheet",
        };
}

internal sealed class FrameRule : ILegacyElementRule
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    private static readonly XNamespace Text = OdfPackage.Namespaces.Text;

    private static readonly HashSet<string> Framed = ["text-box", "image", "object", "object-ole", "plugin", "applet", "floating-frame"];
    private static readonly HashSet<string> InnerDrawAttributes = ["corner-radius", "chain-next-name", "filter-name", "class-id", "notify-on-update-of-ranges"];

    public bool Applies(XElement element) =>
        element.Name.Namespace == Draw && Framed.Contains(element.Name.LocalName) && element.Parent?.Name != Draw + "frame";

    public void Apply(XElement element)
    {
        var frame = new XElement(Draw + "frame");
        foreach (var attribute in element.Attributes().Where(BelongsToFrame).ToList())
        {
            attribute.Remove();
            frame.SetAttributeValue(attribute.Name, attribute.Value);
        }

        element.ReplaceWith(frame);
        frame.Add(element);
    }

    private bool BelongsToFrame(XAttribute attribute)
    {
        var ns = attribute.Name.Namespace;
        if (ns == Svg || ns == Presentation || ns == Table || ns == Text) return true;
        return ns == Draw && !InnerDrawAttributes.Contains(attribute.Name.LocalName);
    }
}

internal sealed class StylePropertiesRule(LegacyPropertyPartition partition) : ILegacyElementRule
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;

    public bool Applies(XElement element) => element.Name == Style + "properties";

    public void Apply(XElement element)
    {
        var replacements = partition.Split(element, ContainerOf(element.Parent));
        element.ReplaceWith(replacements);
    }

    private string ContainerOf(XElement? parent)
    {
        if (parent is null) return "graphic";
        var local = parent.Name.LocalName;
        if (local is "page-layout" or "page-master") return "page-layout";
        if (local is "header-style" or "footer-style") return "header-footer";
        if (local.StartsWith("list-level-style", StringComparison.Ordinal)) return "list-level";
        return parent.Attribute(Style + "family")?.Value ?? "graphic";
    }
}

internal sealed class LegacyPropertyPartition
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;

    private static readonly HashSet<string> FoText =
        ["color", "letter-spacing", "text-shadow", "text-transform", "language", "country", "hyphenate", "font-variant", "font-style", "font-weight", "font-size", "font-family"];

    private static readonly HashSet<string> FoParagraph =
        ["text-align", "text-align-last", "text-indent", "line-height", "keep-with-next", "keep-together", "widows", "orphans", "hyphenation-ladder-count"];

    private static readonly HashSet<string> StyleText =
        ["use-window-font-color", "letter-kerning", "font-relief", "text-position", "text-outline", "text-scale", "text-blinking", "text-emphasize", "text-combine"];

    private static readonly HashSet<string> StyleParagraph =
        ["line-spacing", "line-height-at-least", "auto-text-indent", "text-autospace", "punctuation-wrap", "line-break", "tab-stop-distance", "justify-single-word", "register-true"];

    private readonly Dictionary<string, string> _main = new(StringComparer.Ordinal)
    {
        ["text"] = "text-properties",
        ["paragraph"] = "paragraph-properties",
        ["table-cell"] = "table-cell-properties",
        ["table-column"] = "table-column-properties",
        ["table-row"] = "table-row-properties",
        ["table"] = "table-properties",
        ["drawing-page"] = "drawing-page-properties",
        ["page-layout"] = "page-layout-properties",
        ["header-footer"] = "header-footer-properties",
        ["list-level"] = "list-level-properties",
        ["chart"] = "chart-properties",
        ["section"] = "section-properties",
        ["ruby"] = "ruby-properties",
    };

    private readonly HashSet<string> _withText = ["paragraph", "table-cell", "graphics", "graphic", "presentation", "list-level", "chart", "default"];

    public IEnumerable<XElement> Split(XElement properties, string container)
    {
        var mainName = Style + _main.GetValueOrDefault(container, "graphic-properties");
        var main = new XElement(mainName);
        var text = new XElement(Style + "text-properties");
        var paragraph = new XElement(Style + "paragraph-properties");
        var splitsText = container != "text" && _withText.Contains(container);
        var splitsParagraph = container is "table-cell" or "graphics" or "graphic" or "presentation" or "default" || container == "paragraph";

        foreach (var attribute in properties.Attributes())
        {
            var target = splitsText && IsText(attribute.Name) ? text
                : splitsParagraph && IsParagraph(attribute.Name, container) ? paragraph
                : main;
            Add(target, attribute, container);
        }

        foreach (var child in properties.Elements())
            (child.Name.LocalName == "tab-stops" && splitsParagraph ? paragraph : main).Add(new XElement(child));

        if (container == "paragraph")
        {
            paragraph = main.HasAttributes || main.HasElements ? Merge(paragraph, main) : paragraph;
            return new[] { paragraph, text }.Where(Used);
        }

        return new[] { main, paragraph, text }.Where(element => element == main || Used(element));
    }

    private XElement Merge(XElement target, XElement source)
    {
        foreach (var attribute in source.Attributes()) target.SetAttributeValue(attribute.Name, attribute.Value);
        target.Add(source.Elements());
        return target;
    }

    private bool Used(XElement element) => element.HasAttributes || element.HasElements;

    private bool IsText(XName name)
    {
        var local = name.LocalName;
        if (name.Namespace == Fo) return FoText.Contains(local) || local.StartsWith("font-", StringComparison.Ordinal);
        if (name.Namespace != Style) return false;
        return local.StartsWith("font-", StringComparison.Ordinal)
            || local.StartsWith("text-underline", StringComparison.Ordinal)
            || local.StartsWith("text-crossing-out", StringComparison.Ordinal)
            || local.StartsWith("text-rotation", StringComparison.Ordinal)
            || local.StartsWith("language-", StringComparison.Ordinal)
            || local.StartsWith("country-", StringComparison.Ordinal)
            || StyleText.Contains(local);
    }

    private bool IsParagraph(XName name, string container)
    {
        var local = name.LocalName;
        if (name.Namespace == Fo)
            return FoParagraph.Contains(local)
                || (container is "paragraph" && (local.StartsWith("margin", StringComparison.Ordinal) || local.StartsWith("break-", StringComparison.Ordinal)))
                || (container == "table-cell" && local == "margin-left");
        return name.Namespace == Style && StyleParagraph.Contains(local);
    }

    private void Add(XElement target, XAttribute attribute, string container)
    {
        var name = attribute.Name;
        var value = attribute.Value;
        if (name == Style + "text-underline")
        {
            var (style, type, width) = Underline(value);
            target.SetAttributeValue(Style + "text-underline-style", style);
            if (type is not null) target.SetAttributeValue(Style + "text-underline-type", type);
            if (width is not null) target.SetAttributeValue(Style + "text-underline-width", width);
            return;
        }

        if (name == Style + "text-underline-color")
        {
            target.SetAttributeValue(name, value);
            return;
        }

        if (name == Style + "text-crossing-out")
        {
            target.SetAttributeValue(Style + "text-line-through-style", value == "none" ? "none" : "solid");
            if (value == "double-line") target.SetAttributeValue(Style + "text-line-through-type", "double");
            return;
        }

        if (container == "table-cell" && name == Fo + "vertical-align")
        {
            target.SetAttributeValue(Style + "vertical-align", value);
            return;
        }

        target.SetAttributeValue(name, value);
    }

    private (string Style, string? Type, string? Width) Underline(string value) => value switch
    {
        "none" => ("none", null, null),
        "double" => ("solid", "double", null),
        "bold" => ("solid", null, "bold"),
        "dotted" or "bold-dotted" => ("dotted", null, value.StartsWith("bold", StringComparison.Ordinal) ? "bold" : null),
        "dash" or "long-dash" or "bold-dash" or "bold-long-dash" => (value.Contains("long", StringComparison.Ordinal) ? "long-dash" : "dash", null, value.StartsWith("bold", StringComparison.Ordinal) ? "bold" : null),
        "dot-dash" or "bold-dot-dash" => ("dot-dash", null, value.StartsWith("bold", StringComparison.Ordinal) ? "bold" : null),
        "dot-dot-dash" or "bold-dot-dot-dash" => ("dot-dot-dash", null, value.StartsWith("bold", StringComparison.Ordinal) ? "bold" : null),
        "wave" or "small-wave" or "bold-wave" => ("wave", null, value.StartsWith("bold", StringComparison.Ordinal) ? "bold" : null),
        "double-wave" => ("wave", "double", null),
        _ => ("solid", null, null),
    };
}
