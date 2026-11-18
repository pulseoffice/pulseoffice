using System.Globalization;
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxParagraphMarkChanges
{
    public W.TrackChangeType? Deletion(W.Paragraph paragraph) =>
        paragraph.ParagraphProperties?.ParagraphMarkRunProperties is { } mark
            ? (W.TrackChangeType?)mark.GetFirstChild<W.Deleted>() ?? mark.GetFirstChild<W.MoveFrom>()
            : null;

    public W.TrackChangeType? Insertion(W.Paragraph paragraph) =>
        paragraph.ParagraphProperties?.ParagraphMarkRunProperties is { } mark
            ? (W.TrackChangeType?)mark.GetFirstChild<W.Inserted>() ?? mark.GetFirstChild<W.MoveTo>()
            : null;

    public string? MarkMoveId(W.Paragraph paragraph, W.TrackChangeType mark) => mark switch
    {
        W.MoveFrom => MoveId(MarkRangeName<W.MoveFromRangeStart>(paragraph, start => start.Name?.Value), mark.Author?.Value, mark.Date?.Value),
        W.MoveTo => MoveId(MarkRangeName<W.MoveToRangeStart>(paragraph, start => start.Name?.Value), mark.Author?.Value, mark.Date?.Value),
        _ => null,
    };

    public string MoveId(string? name, string? author, DateTime? date) =>
        "move-" + (name is { Length: > 0 } ? name : (author ?? string.Empty) + "|" + Date(date).ToString("O", CultureInfo.InvariantCulture));

    public DateTimeOffset Date(DateTime? value) =>
        value is { } date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)) : DateTimeOffset.MinValue;

    public string? RangeName<TStart>(OpenXmlElement element, Func<TStart, string?> name) where TStart : OpenXmlElement
    {
        for (var node = element; node.Parent is not null; node = node.Parent)
        {
            foreach (var sibling in node.ElementsBefore().Reverse())
            {
                if (sibling is TStart start) return name(start);
                if (sibling.Descendants<TStart>().LastOrDefault() is { } inner) return name(inner);
            }
        }
        return null;
    }

    private string? MarkRangeName<TStart>(W.Paragraph paragraph, Func<TStart, string?> name) where TStart : OpenXmlElement =>
        paragraph.Descendants<TStart>().LastOrDefault() is { } inside ? name(inside) : RangeName(paragraph, name);
}
