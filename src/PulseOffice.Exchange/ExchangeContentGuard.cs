using PulseOffice.Foundation.Units;

namespace PulseOffice.Exchange;

public interface IExchangeContentGuard
{
    ExchangeContent? Admit(ExchangeContent content);
}

public sealed class ExchangeContentGuard : IExchangeContentGuard, IExchangeContentVisitor<ExchangeContent?>
{
    public const long MaximumCells = 1L << 24;

    public const int MaximumExtent = 567 * 500;

    public const double MaximumBorderPoints = 12;

    public const double MaximumFontPoints = 409;

    public const int MaximumFamilyLength = 256;

    public ExchangeContent? Admit(ExchangeContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.Accept(this);
    }

    public ExchangeContent? VisitObjects(ObjectsContent content) =>
        content.Objects is { } objects && objects.All(entry => entry?.Object is not null)
            ? content with { Objects = [.. objects.Select(Sized)] }
            : null;

    private ExchangedObject Sized(ExchangedObject entry)
    {
        var size = entry.Size.IsEmpty ? entry.Object.NaturalSize : entry.Size;
        return size.IsEmpty ? entry with { Size = size } : entry with { Size = Capped(size) };
    }

    private DocumentSize Capped(DocumentSize size)
    {
        var scale = Math.Min(1.0, Math.Min((double)MaximumExtent / size.Width.Value, (double)MaximumExtent / size.Height.Value));
        return scale >= 1.0
            ? size
            : new DocumentSize(new Twip(Math.Max(1, (int)(size.Width.Value * scale))), new Twip(Math.Max(1, (int)(size.Height.Value * scale))));
    }

    public ExchangeContent? VisitCells(CellRangeContent content)
    {
        if (content.SheetName is null || content.Rows is null) return null;
        if (content.FirstRow < 0 || content.FirstColumn < 0 || content.RowCount < 0 || content.ColumnCount < 0) return null;
        if ((long)content.RowCount * content.ColumnCount > MaximumCells || content.Rows.Count != content.RowCount) return null;
        var rows = new List<IReadOnlyList<ExchangeCell>>(content.RowCount);
        foreach (var row in content.Rows)
        {
            if (row is null || row.Count != content.ColumnCount || row.Any(cell => cell is null)) return null;
            rows.Add([.. row.Select(Cleaned)]);
        }
        return content.Merges is { } merges && Disjoint(content, merges) ? content with { Rows = rows } : null;
    }

    private bool Disjoint(CellRangeContent content, IReadOnlyList<ExchangeMerge> merges)
    {
        if (merges.Count == 0) return true;
        if (merges.Count > (long)content.RowCount * content.ColumnCount) return false;
        var taken = new bool[(long)content.RowCount * content.ColumnCount];
        foreach (var merge in merges)
        {
            if (merge is null || merge.Row < 0 || merge.Column < 0 || merge.RowSpan < 1 || merge.ColumnSpan < 1) return false;
            if ((long)merge.Row + merge.RowSpan > content.RowCount || (long)merge.Column + merge.ColumnSpan > content.ColumnCount) return false;
            for (var row = merge.Row; row < merge.Row + merge.RowSpan; row++)
                for (var column = merge.Column; column < merge.Column + merge.ColumnSpan; column++)
                {
                    var index = (long)row * content.ColumnCount + column;
                    if (taken[index]) return false;
                    taken[index] = true;
                }
        }
        return true;
    }

    private ExchangeCell Cleaned(ExchangeCell cell) =>
        cell with
        {
            Text = cell.Text ?? string.Empty,
            ColorHex = Colour(cell.ColorHex),
            BackgroundHex = Colour(cell.BackgroundHex),
            FontSizePoints = cell.FontSizePoints is { } size && double.IsFinite(size) && size > 0 ? Math.Min(size, MaximumFontPoints) : null,
            FontFamily = cell.FontFamily is { Length: > 0 and <= MaximumFamilyLength } family && !family.Any(char.IsControl) ? family : null,
            Borders = cell.Borders is { } borders ? Cleaned(borders) : null,
        };

    private ExchangeBorders Cleaned(ExchangeBorders borders) =>
        new() { Left = Line(borders.Left), Top = Line(borders.Top), Right = Line(borders.Right), Bottom = Line(borders.Bottom) };

    private ExchangeBorderLine? Line(ExchangeBorderLine? line) =>
        line is { } edge && double.IsFinite(edge.WidthPoints) && edge.WidthPoints > 0 && Colour(edge.ColorHex) is { } colour
            ? edge with { WidthPoints = Math.Min(edge.WidthPoints, MaximumBorderPoints), ColorHex = colour }
            : null;

    private string? Colour(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && hex.Skip(1).All(char.IsAsciiHexDigit) ? hex : null;
}
