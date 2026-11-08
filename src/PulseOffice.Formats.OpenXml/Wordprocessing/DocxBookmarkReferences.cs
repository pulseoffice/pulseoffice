using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed partial class DocxBookmarkReferences
{
    [GeneratedRegex(@"\b(?:PAGEREF|NOTEREF|REF)\s+""?([^\s""\\]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Reference();

    [GeneratedRegex(@"\bXE\s+""(?:[^""\\]|\\.)*""(?:\s+\\[bfity](?:\s+""[^""]*"")?)*\s+\\r\s+""?([^\s""\\]+)", RegexOptions.IgnoreCase)]
    private static partial Regex IndexRange();

    public IEnumerable<string> Targets(MainDocumentPart main) => Matches(main, Reference()).Where(name => !name.StartsWith("_Toc", StringComparison.Ordinal));

    public IEnumerable<string> IndexRanges(MainDocumentPart main) => Matches(main, IndexRange());

    private IEnumerable<string> Matches(MainDocumentPart main, Regex pattern)
    {
        var roots = new List<DocumentFormat.OpenXml.OpenXmlElement?> { main.Document, main.FootnotesPart?.Footnotes, main.EndnotesPart?.Endnotes };
        roots.AddRange(main.HeaderParts.Select(part => (DocumentFormat.OpenXml.OpenXmlElement?)part.Header));
        roots.AddRange(main.FooterParts.Select(part => (DocumentFormat.OpenXml.OpenXmlElement?)part.Footer));
        foreach (var root in roots.OfType<DocumentFormat.OpenXml.OpenXmlElement>())
        {
            var instructions = string.Concat(root.Descendants<W.FieldCode>().Select(code => code.Text))
                + " " + string.Join(" ", root.Descendants<W.SimpleField>().Select(field => field.Instruction?.Value));
            foreach (Match match in pattern.Matches(instructions)) yield return match.Groups[1].Value;
        }
    }
}
