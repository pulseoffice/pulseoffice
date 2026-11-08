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
}
