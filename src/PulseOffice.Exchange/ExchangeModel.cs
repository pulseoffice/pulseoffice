using System.Text;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Exchange;

public enum ExchangeFlavor
{
    Native,
    OpenDocument,
    OpenXml,
    Html,
    Rtf,
    Text,
    Png,
}

public sealed record ExchangeRepresentation(ExchangeFlavor Flavor, ReadOnlyMemory<byte> Data)
{
    public string? MediaType { get; init; }

    public string AsText() => Flavor == ExchangeFlavor.Rtf ? Encoding.Latin1.GetString(Data.Span) : Encoding.UTF8.GetString(Data.Span);

    public static ExchangeRepresentation OfText(ExchangeFlavor flavor, string text) =>
        new(flavor, flavor == ExchangeFlavor.Rtf ? Encoding.Latin1.GetBytes(text) : Encoding.UTF8.GetBytes(text));
}

public enum ExchangePasteMode
{
    Native,
    Objects,
    Cells,
    Table,
    SheetObject,
    FormattedText,
    UnformattedText,
    Image,
}

public interface IExchangeContentVisitor<out T>
{
    T VisitObjects(ObjectsContent content);

    T VisitCells(CellRangeContent content);
}

public abstract record ExchangeContent
{
    public abstract T Accept<T>(IExchangeContentVisitor<T> visitor);
}

public sealed record ExchangedObject(EmbeddedObject Object, DocumentSize Size);

public sealed record ObjectsContent(IReadOnlyList<ExchangedObject> Objects) : ExchangeContent
{
    public override T Accept<T>(IExchangeContentVisitor<T> visitor) => visitor.VisitObjects(this);
}

public enum ExchangeCellKind
{
    Empty,
    Text,
    Number,
    Boolean,
    Error,
}

public enum ExchangeHorizontalAlignment
{
    General,
    Left,
    Center,
    Right,
    Justify,
}

public enum ExchangeVerticalAlignment
{
    Default,
    Top,
    Middle,
    Bottom,
}

public enum ExchangeLineStyle
{
    Solid,
    Dashed,
    Dotted,
    Double,
}

public sealed record ExchangeBorderLine(double WidthPoints, string ColorHex)
{
    public ExchangeLineStyle Style { get; init; }
}

public sealed record ExchangeBorders
{
    public ExchangeBorderLine? Left { get; init; }

    public ExchangeBorderLine? Top { get; init; }

    public ExchangeBorderLine? Right { get; init; }

    public ExchangeBorderLine? Bottom { get; init; }

    public bool IsEmpty => Left is null && Top is null && Right is null && Bottom is null;
}

public sealed record ExchangeMerge(int Row, int Column, int RowSpan, int ColumnSpan)
{
    public bool IsOrigin(int row, int column) => row == Row && column == Column;

    public bool Covers(int row, int column) =>
        row >= Row && row < Row + RowSpan && column >= Column && column < Column + ColumnSpan && !IsOrigin(row, column);
}

public sealed record ExchangeCell
{
    public static readonly ExchangeCell Empty = new();

    public ExchangeCellKind Kind { get; init; }

    public string Text { get; init; } = string.Empty;

    public double Number { get; init; }

    public string? Formula { get; init; }

    public string? NumberFormat { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public string? FontFamily { get; init; }

    public double? FontSizePoints { get; init; }

    public ExchangeHorizontalAlignment Horizontal { get; init; }

    public ExchangeVerticalAlignment Vertical { get; init; }

    public bool Wrap { get; init; }

    public ExchangeBorders? Borders { get; init; }

    public string? ColorHex { get; init; }

    public string? BackgroundHex { get; init; }
}

public sealed record CellRangeContent : ExchangeContent
{
    public required string SheetName { get; init; }

    public int FirstRow { get; init; }

    public int FirstColumn { get; init; }

    public required int RowCount { get; init; }

    public required int ColumnCount { get; init; }

    public required IReadOnlyList<IReadOnlyList<ExchangeCell>> Rows { get; init; }

    public IReadOnlyList<ExchangeMerge> Merges { get; init; } = [];

    public bool[] CoveredCells()
    {
        var covered = new bool[(long)RowCount * ColumnCount];
        foreach (var merge in Merges)
            for (var row = Math.Max(0, merge.Row); row < Math.Min((long)RowCount, (long)merge.Row + merge.RowSpan); row++)
                for (var column = Math.Max(0, merge.Column); column < Math.Min((long)ColumnCount, (long)merge.Column + merge.ColumnSpan); column++)
                    covered[row * ColumnCount + column] = !merge.IsOrigin(row, column);
        return covered;
    }

    public ExchangeCell At(int row, int column) =>
        row < Rows.Count && column < Rows[row].Count ? Rows[row][column] : ExchangeCell.Empty;

    public override T Accept<T>(IExchangeContentVisitor<T> visitor) => visitor.VisitCells(this);
}

public sealed record ExchangePackage
{
    public required string Id { get; init; }

    public ModuleKind? Origin { get; init; }

    public ExchangeContent? Content { get; init; }

    public IReadOnlyList<ExchangeRepresentation> Representations { get; init; } = [];

    public object? Native { get; init; }

    public bool IsEmpty => Content is null && Native is null && Representations.Count == 0;

    public ExchangeRepresentation? Find(ExchangeFlavor flavor) =>
        Representations.FirstOrDefault(representation => representation.Flavor == flavor);

    public ExchangeRepresentation? Find(ExchangeFlavor flavor, string mediaType) =>
        Representations.FirstOrDefault(representation => representation.Flavor == flavor && representation.MediaType == mediaType);

    public string? TextOf(ExchangeFlavor flavor) =>
        Find(flavor) is { Data.Length: > 0 } representation ? representation.AsText() : null;

    public byte[]? BytesOf(ExchangeFlavor flavor) =>
        Find(flavor) is { Data.Length: > 0 } representation ? representation.Data.ToArray() : null;

    public T? NativeFrom<T>(ModuleKind module) where T : class =>
        Origin == module ? Native as T : null;
}

public static class ExchangeMediaTypes
{
    public const string Spreadsheet = "application/vnd.oasis.opendocument.spreadsheet";
    public const string Text = "application/vnd.oasis.opendocument.text";
    public const string Presentation = "application/vnd.oasis.opendocument.presentation";
    public const string Workbook = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string WordDocument = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Slides = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
}
