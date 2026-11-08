using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxDropSplit(ParagraphBlock Head, ParagraphBlock Tail, DropCap Cap);

public sealed class DocxDropCaps(IParagraphText text)
{
    private const double LineFactor = 1.15;
    private const int LargestLines = 10;
    private const int LargestDistance = 31_680;

    public DocxDropSplit? Split(ParagraphBlock block)
    {
        if (block.Content.DropCap is not { IsActive: true } cap || block.Length == 0) return null;
        var length = cap.WholeWord ? WordLength(block.Text) : Math.Min(cap.Characters, block.Length);
        if (length <= 0) return null;
        var (left, right) = text.Split(block.State, length);
        var size = (block.Content.Runs.FirstOrDefault()?.FontSize ?? block.Content.FontSize) * cap.Lines * LineFactor;
        var head = text.Map(left, 0, length, run => run with { FontSize = Math.Round(size * 2) / 2 });
        var plain = right.Content with { DropCap = null, PageBreakBefore = null, ColumnBreakBefore = null };
        var frame = new TextParagraph(string.Empty)
        {
            StyleName = block.Content.StyleName,
            PageBreakBefore = block.Content.PageBreakBefore,
            ColumnBreakBefore = block.Content.ColumnBreakBefore,
        }.WithRuns(head.Content.Runs);
        return new DocxDropSplit(
            new ParagraphBlock(block.Id + "-drop", frame) { Marks = head.Marks },
            new ParagraphBlock(block.Id, plain) { Marks = right.Marks },
            cap);
    }

    public W.FrameProperties Frame(DropCap cap) => new()
    {
        DropCap = W.DropCapLocationValues.Drop,
        Lines = cap.Lines,
        Wrap = W.TextWrappingValues.Around,
        VerticalPosition = W.VerticalAnchorValues.Text,
        HorizontalPosition = W.HorizontalAnchorValues.Text,
        HorizontalSpace = ((int)Math.Round(cap.Distance * 20)).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public bool IsDropCap(W.Paragraph paragraph) =>
        paragraph.ParagraphProperties?.FrameProperties?.DropCap?.Value is { } location && location != W.DropCapLocationValues.None;

    public ParagraphBlock Merge(ParagraphBlock head, W.Paragraph frame, ParagraphBlock tail)
    {
        var properties = frame.ParagraphProperties!.FrameProperties!;
        var size = tail.Content.Runs.FirstOrDefault()?.FontSize;
        var letters = text.Map(head.State, 0, head.Length, run => run with { FontSize = size });
        var merged = text.Concat(letters, tail.State);
        var paged = tail.Content.WithRuns(merged.Content.Runs) with
        {
            PageBreakBefore = head.Content.PageBreakBefore == true ? true : tail.Content.PageBreakBefore,
            ColumnBreakBefore = head.Content.ColumnBreakBefore == true ? true : tail.Content.ColumnBreakBefore,
        };
        var distance = int.TryParse(properties.HorizontalSpace?.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var twips) ? Math.Clamp(twips, 0, LargestDistance) / 20.0 : 0;
        var cap = new DropCap(Math.Clamp(properties.Lines?.Value ?? 3, 2, LargestLines), head.Length) { Distance = distance };
        return new ParagraphBlock(tail.Id, paged with { DropCap = cap }) { Marks = merged.Marks };
    }

    private int WordLength(string value)
    {
        var length = 0;
        while (length < value.Length && !char.IsWhiteSpace(value[length])) length++;
        return length;
    }
}
