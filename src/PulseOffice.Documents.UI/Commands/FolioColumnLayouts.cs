using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Colors;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Pages;

namespace PulseOffice.Documents.UI.Commands;

public interface IFolioColumnLayouts
{
    ColumnLayout Layout(SectionColumns columns, double textWidth);

    SectionColumns Columns(ColumnLayout layout, bool balanced, SectionColumns previous);
}

public sealed class FolioColumnLayouts : IFolioColumnLayouts
{
    private const int LargestCount = 10;

    private const double RoundingTolerance = 0.026;

    public ColumnLayout Layout(SectionColumns columns, double textWidth)
    {
        ArgumentNullException.ThrowIfNull(columns);
        return new ColumnLayout(columns.Count, columns.Gap.ToCentimeters(), columns.Separator)
        {
            TextWidth = textWidth,
            Line = Line(columns.SeparatorLine),
            Widths = columns.Uneven ? [.. Scaled(columns, textWidth)] : [],
        };
    }

    public SectionColumns Columns(ColumnLayout layout, bool balanced, SectionColumns previous)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(previous);
        if (layout.Count <= 1 && !layout.Separator) return SectionColumns.Single with { Balanced = balanced };
        var count = Math.Clamp(layout.Count, 1, LargestCount);
        var columns = new SectionColumns(count, Twip.FromCentimeters(layout.Gap)) { Separator = layout.Separator, SeparatorLine = SeparatorLine(layout.Line, previous.SeparatorLine), Balanced = balanced, BalancedBeforeSection = previous.BalancedBeforeSection };
        if (layout.Widths.Count != count || count <= 1) return columns;
        if (previous.Uneven && previous.Count == count && Unchanged(layout.Widths, [.. Scaled(previous, layout.TextWidth)])) return columns with { Spans = previous.Spans };
        return columns with { Spans = [.. layout.Widths.Select(width => new ColumnSpan(Twip.FromCentimeters(Math.Max(0.05, width.Width)), Twip.FromCentimeters(Math.Max(0, width.Gap))))] };
    }

    private ColumnLine Line(ColumnSeparatorLine line) => new(
        line.Style, line.Width, new ArgbColor(line.Color).ToHashedHex(), line.HeightPercent,
        line.Alignment switch
        {
            ColumnSeparatorAlignment.Middle => ColumnLinePosition.Middle,
            ColumnSeparatorAlignment.Bottom => ColumnLinePosition.Bottom,
            _ => ColumnLinePosition.Top,
        });

    private ColumnSeparatorLine SeparatorLine(ColumnLine line, ColumnSeparatorLine previous)
    {
        if (Line(previous) == line) return previous;
        var colour = ArgbColor.FromRgbHex(line.ColorHex)?.Value ?? previous.Color;
        return new ColumnSeparatorLine(line.Style, Math.Clamp(line.Width, 0.05, ColumnLine.LargestWidth), (previous.Color & 0xFF000000) | (colour & 0x00FFFFFF))
        {
            HeightPercent = Math.Clamp(line.HeightPercent, 1, ColumnSeparatorLine.FullHeight),
            Alignment = line.Position switch
            {
                ColumnLinePosition.Middle => ColumnSeparatorAlignment.Middle,
                ColumnLinePosition.Bottom => ColumnSeparatorAlignment.Bottom,
                _ => ColumnSeparatorAlignment.Top,
            },
        };
    }

    private bool Unchanged(IReadOnlyList<ColumnWidth> edited, IReadOnlyList<ColumnWidth> shown) =>
        edited.Count == shown.Count && edited.Zip(shown).All(pair => Math.Abs(pair.First.Width - pair.Second.Width) < RoundingTolerance && Math.Abs(pair.First.Gap - pair.Second.Gap) < RoundingTolerance);

    private IEnumerable<ColumnWidth> Scaled(SectionColumns columns, double textWidth)
    {
        var gaps = columns.Spans.Take(columns.Count - 1).Sum(span => span.Gap.ToCentimeters());
        var weights = columns.Spans.Sum(span => Math.Max(0, span.Width.ToCentimeters()));
        var room = Math.Max(0.5 * columns.Count, textWidth - gaps);
        var last = columns.Spans.Count - 1;
        return columns.Spans.Select((span, index) => new ColumnWidth(
            weights > 0 ? Math.Max(0, span.Width.ToCentimeters()) * room / weights : room / columns.Count,
            index < last ? span.Gap.ToCentimeters() : 0));
    }
}
