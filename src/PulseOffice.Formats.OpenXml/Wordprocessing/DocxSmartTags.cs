using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxSmartTags
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string WordPrefix = "w";

    public void Unwrap(MainDocumentPart main)
    {
        ArgumentNullException.ThrowIfNull(main);
        var roots = new List<OpenXmlElement?> { main.Document, main.FootnotesPart?.Footnotes, main.EndnotesPart?.Endnotes, main.WordprocessingCommentsPart?.Comments };
        roots.AddRange(main.HeaderParts.Select(part => (OpenXmlElement?)part.Header));
        roots.AddRange(main.FooterParts.Select(part => (OpenXmlElement?)part.Footer));
        foreach (var root in roots.OfType<OpenXmlElement>()) Unwrap(root);
    }

    public void Unwrap(OpenXmlElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var declarations = root.NamespaceDeclarations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        declarations[WordPrefix] = WordNamespace;
        var opening = "<w:p" + string.Concat(declarations.Select(pair => $" xmlns:{pair.Key}=\"{System.Security.SecurityElement.Escape(pair.Value)}\"")) + ">";
        while (root.Descendants<OpenXmlUnknownElement>().Where(IsSmartTag).Where(Outermost).ToList() is { Count: > 0 } tags)
            foreach (var parent in tags.Select(tag => tag.Parent).OfType<OpenXmlElement>().Distinct().ToList())
                Flatten(parent, opening);
    }

    private void Flatten(OpenXmlElement parent, string opening)
    {
        var children = new List<OpenXmlElement>();
        foreach (var child in parent.ChildElements)
            if (child is OpenXmlUnknownElement tag && IsSmartTag(tag)) children.AddRange(Contents(tag, opening));
            else children.Add(child.CloneNode(true));
        parent.RemoveAllChildren();
        parent.Append(children);
    }

    private IEnumerable<OpenXmlElement> Contents(OpenXmlUnknownElement tag, string opening)
    {
        var content = string.Concat(tag.ChildElements.Where(child => child.LocalName != "smartTagPr").Select(child => child.OuterXml));
        var holder = new W.Paragraph(opening + content + "</w:p>");
        var children = holder.ChildElements.ToList();
        holder.RemoveAllChildren();
        return children;
    }

    private bool Outermost(OpenXmlUnknownElement tag) =>
        !tag.Ancestors<OpenXmlUnknownElement>().Any(IsSmartTag);

    private bool IsSmartTag(OpenXmlUnknownElement element) =>
        element.LocalName == "smartTag" && element.NamespaceUri == WordNamespace;
}
