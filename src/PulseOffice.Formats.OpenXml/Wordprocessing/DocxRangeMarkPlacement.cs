using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxRangeMarkPlacement
{
    public void Place(MainDocumentPart main)
    {
        ArgumentNullException.ThrowIfNull(main);
        var roots = new List<OpenXmlElement?> { main.Document, main.FootnotesPart?.Footnotes, main.EndnotesPart?.Endnotes, main.WordprocessingCommentsPart?.Comments };
        roots.AddRange(main.HeaderParts.Select(part => (OpenXmlElement?)part.Header));
        roots.AddRange(main.FooterParts.Select(part => (OpenXmlElement?)part.Footer));
        foreach (var root in roots.OfType<OpenXmlElement>()) Place(root);
    }

    public void Place(OpenXmlElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var sequence = new List<OpenXmlElement>();
        foreach (var element in root.Descendants())
        {
            if (element is W.Paragraph || (IsRangeMark(element) && IsBlockLevel(element.Parent))) sequence.Add(element);
        }

        if (!sequence.Any(element => element is not W.Paragraph)) return;
        var depth = sequence.ToDictionary(element => element, element => element.Ancestors<W.TextBoxContent>().Count());
        var moves = new List<(OpenXmlElement Mark, W.Paragraph Target, bool AtEnd)>();
        for (var index = 0; index < sequence.Count; index++)
        {
            var mark = sequence[index];
            if (mark is W.Paragraph) continue;
            var before = Nearest(sequence, index, -1, depth[mark], depth);
            var after = Nearest(sequence, index, 1, depth[mark], depth);
            var closes = mark is W.BookmarkEnd or W.CommentRangeEnd;
            if (closes ? before is not null : after is null)
            {
                if (before is not null) moves.Add((mark, before, true));
            }
            else if (after is not null) moves.Add((mark, after, false));
        }

        foreach (var (mark, target, _) in moves.Where(move => move.AtEnd))
        {
            mark.Remove();
            target.Append(mark);
        }

        foreach (var (mark, target, _) in moves.Where(move => !move.AtEnd).Reverse())
        {
            mark.Remove();
            if (target.ParagraphProperties is { } properties) properties.InsertAfterSelf(mark);
            else target.PrependChild(mark);
        }
    }

    private W.Paragraph? Nearest(List<OpenXmlElement> sequence, int from, int step, int level, Dictionary<OpenXmlElement, int> depth)
    {
        for (var index = from + step; index >= 0 && index < sequence.Count; index += step)
        {
            if (sequence[index] is W.Paragraph paragraph && depth[paragraph] == level) return paragraph;
        }

        return null;
    }

    private bool IsRangeMark(OpenXmlElement element) =>
        element is W.BookmarkStart or W.BookmarkEnd or W.CommentRangeStart or W.CommentRangeEnd;

    private bool IsBlockLevel(OpenXmlElement? parent) =>
        parent is W.Body or W.TableCell or W.Table or W.TableRow or W.SdtContentBlock or W.TextBoxContent or W.CustomXmlBlock
            or W.Header or W.Footer or W.Footnote or W.Endnote or W.Comment;
}
