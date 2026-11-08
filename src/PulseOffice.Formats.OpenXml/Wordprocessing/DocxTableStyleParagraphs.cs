using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxTableStyleParagraphs
{
    private const string Main = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly string[] SpacingAttributes = ["before", "after", "line"];

    public W.ParagraphProperties? Defaults(string? tableStyleId, DocxReadContext context)
    {
        if (tableStyleId is null || Styles(context) is not { } styles) return null;
        var spacing = new W.SpacingBetweenLines();
        W.Justification? justification = null;
        foreach (var style in Chain(styles, tableStyleId))
        {
            var properties = style.StyleParagraphProperties;
            if (properties?.SpacingBetweenLines is { } own)
                foreach (var name in SpacingAttributes)
                    if (Attribute(spacing, name) is null && Attribute(own, name) is { } value)
                    {
                        Set(spacing, name, value);
                        if (name == "line" && Attribute(own, "lineRule") is { } rule) Set(spacing, "lineRule", rule);
                    }
            justification ??= properties?.Justification?.CloneNode(true) as W.Justification;
        }
        if (!spacing.GetAttributes().Any() && justification is null) return null;
        var result = new W.ParagraphProperties();
        if (justification is not null) result.Append(justification);
        if (spacing.GetAttributes().Any()) result.Append(spacing);
        return result;
    }

    public W.ParagraphProperties? Missing(W.ParagraphProperties defaults, W.ParagraphProperties? direct, string? paragraphStyleId, DocxReadContext context)
    {
        var styles = Styles(context);
        var chain = styles is null ? [] : Chain(styles, paragraphStyleId ?? DefaultParagraphStyle(styles)).ToList();
        var defined = chain.Select(style => style.StyleParagraphProperties as OpenXmlElement).Prepend(direct).OfType<OpenXmlElement>().ToList();
        var result = new W.ParagraphProperties();
        if (defaults.Justification is { } justification && !defined.Any(properties => properties.GetFirstChild<W.Justification>() is not null))
            result.Append(justification.CloneNode(true));
        if (defaults.SpacingBetweenLines is { } spacing)
        {
            var missing = new W.SpacingBetweenLines();
            foreach (var name in SpacingAttributes)
            {
                if (Attribute(spacing, name) is not { } value) continue;
                if (defined.Any(properties => properties.GetFirstChild<W.SpacingBetweenLines>() is { } own && Attribute(own, name) is not null)) continue;
                Set(missing, name, value);
                if (name == "line" && Attribute(spacing, "lineRule") is { } rule) Set(missing, "lineRule", rule);
            }
            if (missing.GetAttributes().Any()) result.Append(missing);
        }
        return result.HasChildren ? result : null;
    }

    public string? DefaultTableStyle(DocxReadContext context) =>
        Styles(context)?.Elements<W.Style>().FirstOrDefault(style => style.Type?.Value == W.StyleValues.Table && style.Default?.Value == true)?.StyleId?.Value;

    private W.Styles? Styles(DocxReadContext context) =>
        (context.Part.OpenXmlPackage as WordprocessingDocument)?.MainDocumentPart?.StyleDefinitionsPart?.Styles;

    private string? DefaultParagraphStyle(W.Styles styles) =>
        styles.Elements<W.Style>().FirstOrDefault(style => style.Type?.Value == W.StyleValues.Paragraph && style.Default?.Value == true)?.StyleId?.Value;

    private IEnumerable<W.Style> Chain(W.Styles styles, string? id)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (id is not null && visited.Add(id) && styles.Elements<W.Style>().FirstOrDefault(style => style.StyleId?.Value == id) is { } style)
        {
            yield return style;
            id = style.BasedOn?.Val?.Value;
        }
    }

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name && attribute.NamespaceUri == Main).Value is { Length: > 0 } value ? value : null;

    private void Set(OpenXmlElement element, string name, string value) =>
        element.SetAttribute(new OpenXmlAttribute("w", name, Main, value));
}
