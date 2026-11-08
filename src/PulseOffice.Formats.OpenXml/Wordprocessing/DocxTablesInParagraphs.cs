using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxTablesInParagraphs
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public void Lift(MainDocumentPart main)
    {
        ArgumentNullException.ThrowIfNull(main);
        var roots = new List<OpenXmlElement?> { main.Document, main.FootnotesPart?.Footnotes, main.EndnotesPart?.Endnotes, main.WordprocessingCommentsPart?.Comments };
        roots.AddRange(main.HeaderParts.Select(part => (OpenXmlElement?)part.Header));
        roots.AddRange(main.FooterParts.Select(part => (OpenXmlElement?)part.Footer));
        foreach (var root in roots.OfType<OpenXmlElement>()) Lift(root);
    }

    public void Lift(OpenXmlElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        foreach (var misplaced in root.Descendants().Where(element => element.Parent is W.Paragraph && IsTable(element)).Reverse().ToList())
        {
            if (misplaced.Parent is not W.Paragraph paragraph) continue;
            var rest = misplaced.ElementsAfter().ToList();
            misplaced.Remove();
            var table = misplaced as W.Table ?? new W.Table(misplaced.OuterXml);
            paragraph.InsertAfterSelf(table);
            var section = paragraph.ParagraphProperties?.SectionProperties;
            if (rest.Count == 0 && section is null) continue;
            var tail = new W.Paragraph();
            if (paragraph.ParagraphProperties is { } properties) tail.Append(properties.CloneNode(true));
            section?.Remove();
            foreach (var element in rest)
            {
                element.Remove();
                tail.Append(element);
            }

            table.InsertAfterSelf(tail);
        }
    }

    private bool IsTable(OpenXmlElement element) =>
        element is W.Table || element is OpenXmlUnknownElement { LocalName: "tbl", NamespaceUri: WordNamespace };
}
