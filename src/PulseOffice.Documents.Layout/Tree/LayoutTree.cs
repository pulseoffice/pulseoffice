using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Tree;

public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;

    public LayoutRect Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

    public bool Intersects(LayoutRect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

public enum GlyphRunKind
{
    Text,
    Label,
    Tab,
    Hyphen,
    Object,
    Citation,
}

public enum StoryKind
{
    Body,
    Header,
    Footer,
    Footnote,
    Endnote,
    Cell,
    Frame,
}

public enum AreaKind
{
    Header,
    Footer,
    Column,
    Footnotes,
}

public enum DecorationKind
{
    ParagraphBackground,
    ParagraphBorder,
    PageBorder,
    PageBackground,
    CellBackground,
    CellBorder,
    CellEdge,
    FootnoteSeparator,
    ColumnSeparator,
}

public enum ObjectLayer
{
    Inline,
    Wrapped,
    Behind,
    Front,
}

public interface ILayoutVisitor
{
    void VisitPage(PageBox page);

    void VisitLine(LineBox line);

    void VisitRun(GlyphRun run, LineBox line);

    void VisitArea(AreaBox area)
    {
    }

    void VisitTable(TableBox table)
    {
    }

    void VisitCell(CellBox cell)
    {
    }

    void VisitObject(ObjectBox item)
    {
    }

    void LeaveObject(ObjectBox item)
    {
    }

    void VisitTurn(TurnedBox turn)
    {
    }

    void LeaveTurn(TurnedBox turn)
    {
    }

    void VisitDecoration(DecorationBox decoration)
    {
    }

    void VisitWatermark(WatermarkBox watermark)
    {
    }
}

public sealed class WatermarkBox(GlyphRun run, double x, double baseline, double angle, uint color) : LayoutBox
{
    public GlyphRun Run { get; } = run;

    public double X { get; } = x;

    public double Baseline { get; } = baseline;

    public double Angle { get; } = angle;

    public uint Color { get; } = color;

    public override LayoutRect Bounds => new(X, Baseline - Run.Font.Size, Run.Width, Run.Font.Size);

    public override IReadOnlyList<LayoutBox> Children => [];

    public override void Accept(ILayoutVisitor visitor) => visitor.VisitWatermark(this);

    public override LayoutBox Moved(double dx, double dy) => new WatermarkBox(Run, X + dx, Baseline + dy, Angle, Color);
}

public abstract class LayoutBox
{
    public abstract LayoutRect Bounds { get; }

    public abstract IReadOnlyList<LayoutBox> Children { get; }

    public abstract void Accept(ILayoutVisitor visitor);

    public abstract LayoutBox Moved(double dx, double dy);
}

public sealed record GlyphRun(
    GlyphRunKind Kind,
    FontKey Font,
    string Text,
    double X,
    double BaselineShift,
    int SourceOffset,
    IReadOnlyList<double> Advances)
{
    public TabLeader Leader { get; init; }

    public TextRun? Style { get; init; }

    public string? ObjectId { get; init; }

    public PulseOffice.Text.Rich.ListBulletPicture? Picture { get; init; }

    public string? NoteId { get; init; }

    public double Ascent { get; init; }

    public byte Level { get; init; }

    public bool RightToLeft => (Level & 1) == 1;

    public double Width => Advances.Sum();

    public int SourceLength => Kind is GlyphRunKind.Text or GlyphRunKind.Tab ? Text.Length : 0;
}

public sealed record ComposedLine(
    string ParagraphId,
    int Index,
    int Start,
    int End,
    double Width,
    double Height,
    double Ascent,
    double Descent,
    IReadOnlyList<GlyphRun> Runs)
{
    public bool IsLast { get; init; }

    public bool Broken { get; init; }

    public bool Forced { get; init; }

    public double Origin { get; init; }

    public int Next { get; init; }

    public double? TextHeight { get; init; }

    public bool RightToLeft { get; init; }

    public bool Bidirectional { get; init; }

    public double CaretHeight => TextHeight ?? Height;

    public bool IsFirst => Index == 0;

    public IEnumerable<string> Notes => Runs.Where(run => run.NoteId is not null).Select(run => run.NoteId!);
}

public sealed class LineBox(ComposedLine line, double left, double top) : LayoutBox
{
    public ComposedLine Line { get; } = line;

    public double Left { get; } = left;

    public double Top { get; } = top;

    public StoryKind Story { get; init; }

    public string? StoryId { get; init; }

    public double Baseline => Top + Line.Ascent;

    public string ParagraphId => Line.ParagraphId;

    public override LayoutRect Bounds => new(Left, Top, Line.Width, Line.Height);

    public override IReadOnlyList<LayoutBox> Children => [];

    public override void Accept(ILayoutVisitor visitor)
    {
        visitor.VisitLine(this);
        foreach (var run in Line.Runs) visitor.VisitRun(run, this);
    }

    public override LayoutBox Moved(double dx, double dy) => MovedLine(dx, dy);

    public LineBox MovedLine(double dx, double dy) => new(Line, Left + dx, Top + dy) { Story = Story, StoryId = StoryId };

    public LineBox InStory(StoryKind story, string? storyId) => new(Line, Left, Top) { Story = story, StoryId = storyId };
}

public sealed class DecorationBox(DecorationKind kind, LayoutRect bounds, uint? color, double strokeWidth) : LayoutBox
{
    public DecorationKind Kind { get; } = kind;

    public uint? Color { get; } = color;

    public double StrokeWidth { get; } = strokeWidth;

    public string? Owner { get; init; }

    public PulseOffice.Text.Rich.BorderLineStyle LineStyle { get; init; }

    public override LayoutRect Bounds { get; } = bounds;

    public override IReadOnlyList<LayoutBox> Children => [];

    public override void Accept(ILayoutVisitor visitor) => visitor.VisitDecoration(this);

    public override LayoutBox Moved(double dx, double dy) => new DecorationBox(Kind, Bounds.Offset(dx, dy), Color, StrokeWidth) { Owner = Owner, LineStyle = LineStyle };
}

public static class RotatedRect
{
    public static LayoutRect Around(LayoutRect rect, double degrees)
    {
        if (!double.IsFinite(degrees) || degrees % 360 == 0) return rect;
        var radians = degrees * Math.PI / 180;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        var width = rect.Width * cos + rect.Height * sin;
        var height = rect.Width * sin + rect.Height * cos;
        return new LayoutRect(rect.X + (rect.Width - width) / 2, rect.Y + (rect.Height - height) / 2, width, height);
    }
}

public sealed class ObjectBox(string objectId, LayoutRect bounds, ObjectLayer layer) : LayoutBox, ITurnedFrame
{
    public string ObjectId { get; } = objectId;

    public ObjectLayer Layer { get; } = layer;

    public override LayoutRect Bounds { get; } = bounds;

    public IReadOnlyList<LayoutBox> Content { get; init; } = [];

    public double RotationDegrees { get; init; }

    public bool FlipHorizontal { get; init; }

    public bool FlipVertical { get; init; }

    public LayoutRect RotatedBounds => RotatedRect.Around(Bounds, RotationDegrees);

    public IEnumerable<LineBox> Lines => Content.OfType<LineBox>().Concat(Content.OfType<TableBox>().SelectMany(table => table.Lines));

    public override IReadOnlyList<LayoutBox> Children => Content;

    public override void Accept(ILayoutVisitor visitor)
    {
        visitor.VisitObject(this);
        foreach (var child in Content) child.Accept(visitor);
        visitor.LeaveObject(this);
    }

    public override LayoutBox Moved(double dx, double dy) => new ObjectBox(ObjectId, Bounds.Offset(dx, dy), Layer) { Content = [.. Content.Select(child => child.Moved(dx, dy))], RotationDegrees = RotationDegrees, FlipHorizontal = FlipHorizontal, FlipVertical = FlipVertical };
}

public sealed class AreaBox(AreaKind kind, LayoutRect bounds, IReadOnlyList<LayoutBox> content) : LayoutBox
{
    public AreaKind Kind { get; } = kind;

    public override LayoutRect Bounds { get; } = bounds;

    public IReadOnlyList<LayoutBox> Content { get; } = content;

    public IEnumerable<LineBox> Lines => Content.OfType<LineBox>().Concat(Content.OfType<TableBox>().SelectMany(table => table.Lines));

    public override IReadOnlyList<LayoutBox> Children => Content;

    public override void Accept(ILayoutVisitor visitor)
    {
        visitor.VisitArea(this);
        foreach (var child in Content) child.Accept(visitor);
    }

    public override LayoutBox Moved(double dx, double dy) => new AreaBox(Kind, Bounds.Offset(dx, dy), [.. Content.Select(child => child.Moved(dx, dy))]);
}

public sealed class CellBox(string tableId, int row, int column, LayoutRect bounds, IReadOnlyList<LayoutBox> content) : LayoutBox
{
    public string TableId { get; } = tableId;

    public int Row { get; } = row;

    public int Column { get; } = column;

    public override LayoutRect Bounds { get; } = bounds;

    public IReadOnlyList<LayoutBox> Content { get; } = content;

    public IEnumerable<LineBox> Lines => Content.OfType<LineBox>()
        .Concat(Content.OfType<TableBox>().SelectMany(table => table.Lines))
        .Concat(Content.OfType<TurnedBox>().SelectMany(turn => turn.Lines));

    public override IReadOnlyList<LayoutBox> Children => Content;

    public override void Accept(ILayoutVisitor visitor)
    {
        visitor.VisitCell(this);
        foreach (var child in Content) child.Accept(visitor);
    }

    public override LayoutBox Moved(double dx, double dy) => new CellBox(TableId, Row, Column, Bounds.Offset(dx, dy), [.. Content.Select(child => child.Moved(dx, dy))]);
}

public sealed class TableBox(string tableId, LayoutRect bounds, IReadOnlyList<CellBox> cells) : LayoutBox
{
    public string TableId { get; } = tableId;

    public override LayoutRect Bounds { get; } = bounds;

    public IReadOnlyList<CellBox> Cells { get; } = cells;

    public IEnumerable<LineBox> Lines => Cells.SelectMany(cell => cell.Lines);

    public override IReadOnlyList<LayoutBox> Children => Cells;

    public override void Accept(ILayoutVisitor visitor)
    {
        visitor.VisitTable(this);
        foreach (var cell in Cells) cell.Accept(visitor);
    }

    public override LayoutBox Moved(double dx, double dy) => new TableBox(TableId, Bounds.Offset(dx, dy), [.. Cells.Select(cell => (CellBox)cell.Moved(dx, dy))]);
}

public sealed record ValueList<T>(IReadOnlyList<T> Items)
{
    public static readonly ValueList<T> Empty = new([]);

    public int Count => Items.Count;

    public bool Equals(ValueList<T>? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(17, (hash, item) => hash * 31 + (item?.GetHashCode() ?? 0));
}

public sealed record PendingNote(string NoteId, int Atom);

public sealed record CarriedCell(int Row, int Cell, int From, int To);

public sealed record PageStart(int FlowIndex, int Line, string Style)
{
    public int Number { get; init; } = 1;

    public int Offset { get; init; }

    public int Row { get; init; }

    public ValueList<int> Cells { get; init; } = ValueList<int>.Empty;

    public ValueList<PendingNote> Pending { get; init; } = ValueList<PendingNote>.Empty;

    public ValueList<CarriedCell> Carried { get; init; } = ValueList<CarriedCell>.Empty;
}

public sealed record PageStories(AreaBox? Header, AreaBox? Footer)
{
    public static readonly PageStories None = new(null, null);
}

public sealed class PageContent
{
    public IReadOnlyList<LineBox> Lines { get; init; } = [];

    public IReadOnlyList<TableBox> Tables { get; init; } = [];

    public IReadOnlyList<AreaBox> Columns { get; init; } = [];

    public AreaBox? Footnotes { get; init; }

    public IReadOnlyList<ObjectBox> Objects { get; init; } = [];

    public IReadOnlyList<DecorationBox> Decorations { get; init; } = [];

    public LayoutRect Body { get; init; }
}

public sealed class PageBox(int index, string styleName, double width, double height, LayoutRect content, PageContent body, PageStart start, PageStories stories) : LayoutBox
{
    public PageBox(int index, string styleName, double width, double height, LayoutRect content, IReadOnlyList<LineBox> lines, PageStart start)
        : this(index, styleName, width, height, content, new PageContent { Lines = lines, Body = content }, start, PageStories.None)
    {
    }

    public int Index { get; } = index;

    public string StyleName { get; } = styleName;

    public double Width { get; } = width;

    public double Height { get; } = height;

    public LayoutRect Content { get; } = content;

    public PageContent Body { get; } = body;

    public IReadOnlyList<LineBox> Lines => Body.Lines;

    public IReadOnlyList<TableBox> Tables => Body.Tables;

    public IReadOnlyList<AreaBox> Columns => Body.Columns;

    public AreaBox? Footnotes => Body.Footnotes;

    public IReadOnlyList<ObjectBox> Objects => Body.Objects;

    public IReadOnlyList<DecorationBox> Decorations => Body.Decorations;

    public AreaBox? Header => Stories.Header;

    public AreaBox? Footer => Stories.Footer;

    public PageStories Stories { get; } = stories;

    public IReadOnlyList<LayoutBox> Backdrop { get; init; } = [];

    public PageStart Start { get; } = start;

    public int Number => Start.Number;

    public override LayoutRect Bounds => new(0, 0, Width, Height);

    public IEnumerable<ITurnedFrame> TurnedFrames => Turned(Children);

    private IEnumerable<ITurnedFrame> Turned(IEnumerable<LayoutBox> boxes)
    {
        foreach (var box in boxes)
        {
            if (box is ITurnedFrame { RotationDegrees: not 0 } frame) yield return frame;
            foreach (var nested in Turned(box.Children)) yield return nested;
        }
    }

    public IEnumerable<LineBox> AllLines =>
        Lines.Concat(Header?.Lines ?? []).Concat(Footer?.Lines ?? []).Concat(Footnotes?.Lines ?? []).Concat(Objects.SelectMany(item => item.Lines));

    public override IReadOnlyList<LayoutBox> Children
    {
        get
        {
            var inTables = new HashSet<LineBox>(Tables.SelectMany(table => table.Lines));
            var children = new List<LayoutBox>(Backdrop);
            if (Header is not null) children.Add(Header);
            children.AddRange(Columns);
            children.AddRange(Decorations);
            children.AddRange(Objects.Where(item => item.Layer == ObjectLayer.Behind));
            children.AddRange(Lines.Where(line => !inTables.Contains(line)));
            children.AddRange(Tables);
            children.AddRange(Objects.Where(item => item.Layer != ObjectLayer.Behind));
            if (Footnotes is not null) children.Add(Footnotes);
            if (Footer is not null) children.Add(Footer);
            return children;
        }
    }

    public PageBox Renumbered(int index, PageStart start) => index == Index && start == Start ? this : new PageBox(index, StyleName, Width, Height, Content, Body, start, Stories) { Backdrop = Backdrop };

    public PageBox WithStories(PageStories stories) => new(Index, StyleName, Width, Height, Content, Body, Start, stories) { Backdrop = Backdrop };

    public override void Accept(ILayoutVisitor visitor)
    {
        visitor.VisitPage(this);
        foreach (var child in Children) child.Accept(visitor);
    }

    public override LayoutBox Moved(double dx, double dy) => this;
}

public sealed record FlowKey(string ParagraphId, IReadOnlyList<object> Parts, string Label)
{
    public FlowKey(string paragraphId, TextParagraph content, string label) : this(paragraphId, [content], label)
    {
    }

    public bool Matches(FlowKey other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (ParagraphId != other.ParagraphId || Label != other.Label || Parts.Count != other.Parts.Count) return false;
        for (var i = 0; i < Parts.Count; i++)
        {
            var first = Parts[i];
            var second = other.Parts[i];
            if (!ReferenceEquals(first, second) && (first is TextParagraph || !Equals(first, second))) return false;
        }
        return true;
    }
}

public sealed class DocumentLayout(IReadOnlyList<PageBox> pages, IReadOnlyList<FlowKey> flow) : LayoutBox
{
    public IReadOnlyList<PageBox> Pages { get; } = pages;

    public IReadOnlyList<FlowKey> Flow { get; } = flow;

    public FlowKey? Signature { get; init; }

    public override LayoutRect Bounds => Pages.Count == 0 ? default : new(0, 0, Pages.Max(page => page.Width), Pages.Sum(page => page.Height));

    public override IReadOnlyList<LayoutBox> Children => Pages;

    public override void Accept(ILayoutVisitor visitor)
    {
        foreach (var page in Pages) page.Accept(visitor);
    }

    public override LayoutBox Moved(double dx, double dy) => this;
}
