using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI.ShapeText;

public interface ISheetShapeText
{
    bool IsActive { get; }

    CellStyle Current { get; }

    void Apply(string description, Func<CellStyle, CellStyle> change);

    bool Supports(string commandId);

    PulseOffice.Text.Rich.TextParagraph? Paragraph { get; }

    void ApplyParagraphs(string description, Func<PulseOffice.Text.Rich.TextParagraph, PulseOffice.Text.Rich.TextParagraph> change);
}

public sealed record SheetShapeParagraphs(
    Func<PulseOffice.Text.Rich.TextParagraph?> Current,
    Action<string, Func<PulseOffice.Text.Rich.TextParagraph, PulseOffice.Text.Rich.TextParagraph>> Apply);

public interface IShapeTextCommandScope
{
    bool Supports(string commandId);
}

public sealed class ShapeTextCommandScope : IShapeTextCommandScope
{
    private readonly HashSet<string> _commands =
    [
        "ledger.format.bold",
        "ledger.format.italic",
        "ledger.format.underline",
        "ledger.format.grow",
        "ledger.format.shrink",
        "ledger.font.family",
        "ledger.font.size",
        "ledger.font.color",
    ];

    public bool Supports(string commandId) => _commands.Contains(commandId);
}

public sealed class SheetShapeText(
    Func<bool> active, Func<CellStyle> current, Action<string, Func<CellStyle, CellStyle>> apply, IShapeTextCommandScope scope, SheetShapeParagraphs paragraphs) : ISheetShapeText
{
    public SheetShapeText(Func<bool> active, Func<CellStyle> current, Action<string, Func<CellStyle, CellStyle>> apply)
        : this(active, current, apply, new ShapeTextCommandScope(), new SheetShapeParagraphs(() => null, (_, _) => { })) { }

    public SheetShapeText(Func<bool> active, Func<CellStyle> current, Action<string, Func<CellStyle, CellStyle>> apply, SheetShapeParagraphs paragraphs)
        : this(active, current, apply, new ShapeTextCommandScope(), paragraphs) { }

    public PulseOffice.Text.Rich.TextParagraph? Paragraph => paragraphs.Current();

    public void ApplyParagraphs(string description, Func<PulseOffice.Text.Rich.TextParagraph, PulseOffice.Text.Rich.TextParagraph> change) =>
        paragraphs.Apply(description, change);

    public bool IsActive => active();

    public bool Supports(string commandId) => scope.Supports(commandId);

    public CellStyle Current => current();

    public void Apply(string description, Func<CellStyle, CellStyle> change) => apply(description, change);
}

public interface IShapeTextCellStyle
{
    CellStyle Read(ShapeObject shape, int start, int end);

    ShapeObject Write(ShapeObject shape, int start, int end, Func<CellStyle, CellStyle> change);
}

public sealed class ShapeTextCellStyle(IShapeTextEditing editing) : IShapeTextCellStyle
{
    public CellStyle Read(ShapeObject shape, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var style = shape.Style.Text;
        var run = editing.RunAt(shape, start, end);
        return CellStyle.Default with
        {
            Font = CellFont.Default with
            {
                Family = run.Family is { Length: > 0 } family ? family : style.Family,
                Size = run.Size is { } size && size > 0 ? size : style.Size,
                Bold = run.Bold || style.Bold,
                Italic = run.Italic || style.Italic,
                Underline = run.Underline || style.Underline,
                ColorHex = run.ColorHex ?? style.ColorHex,
            },
        };
    }

    public ShapeObject Write(ShapeObject shape, int start, int end, Func<CellStyle, CellStyle> change)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(change);
        var style = shape.Style.Text;
        var before = Read(shape, start, end).Font;
        var after = change(Read(shape, start, end)).Font;
        if (before == after) return shape;

        return editing.Format(shape, start, end, run => run with
        {
            Bold = after.Bold != before.Bold ? after.Bold && !style.Bold : run.Bold,
            Italic = after.Italic != before.Italic ? after.Italic && !style.Italic : run.Italic,
            Underline = after.Underline != before.Underline ? after.Underline && !style.Underline : run.Underline,
            Size = Math.Abs(after.Size - before.Size) > 0.001 ? (Math.Abs(after.Size - style.Size) < 0.001 ? null : after.Size) : run.Size,
            Family = after.Family != before.Family ? (after.Family == style.Family ? null : after.Family) : run.Family,
            ColorHex = after.ColorHex != before.ColorHex ? after.ColorHex : run.ColorHex,
        });
    }
}
