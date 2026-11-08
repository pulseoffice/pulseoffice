using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerShapeTextSession
{
    private readonly Workbook _workbook;
    private readonly SheetGridView _grid;
    private readonly Canvas _overlay;
    private readonly Control _host;
    private readonly ITextCatalog _texts;
    private readonly ShapeText.IShapeTextCellStyle _style;
    private readonly LedgerCellWriter _writer;
    private readonly LedgerCellEditing _editing;
    private readonly LedgerObjectEditing _objects;
    private readonly Drawing.Shapes.IShapeTextParagraphs _paragraphs;
    private (Worksheet Sheet, PlacedObject Placed)? _target;

    public LedgerShapeTextSession(
        Workbook workbook, SheetGridView grid, Canvas overlay, Control host, ITextCatalog texts,
        ShapeText.IShapeTextCellStyle style, Drawing.Shapes.IShapeTextEditing editing,
        LedgerCellWriter writer, LedgerCellEditing cellEditing, LedgerObjectEditing objects, Drawing.Shapes.IShapeTextParagraphs paragraphs)
    {
        _paragraphs = paragraphs;
        _workbook = workbook;
        _grid = grid;
        _overlay = overlay;
        _host = host;
        _texts = texts;
        _style = style;
        _writer = writer;
        _editing = cellEditing;
        _objects = objects;

        Editor = new ShapeTextEditor(editing, new Drawing.Shapes.ShapeTextInk(), new ShapeBrushes(), new PulseOffice.UI.Controls.Text.TextEditingFocus(), new PulseOffice.Text.Rich.CaretWord());
        Editor.LiveChanged += (_, _) => RefreshLiveShape();
        Editor.Committed += (_, edited) => OnCommitted(edited);
        Editor.Exited += (_, _) => _grid.Focus();
        Editor.PendingChanged += (_, _) => PendingChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PendingChanged;

    public event EventHandler? SelectionStale;

    public ShapeTextEditor Editor { get; }

    public ShapeText.SheetShapeText Facade() => new(IsActive, CurrentStyle, ApplyStyle, new ShapeText.SheetShapeParagraphs(CurrentParagraph, ApplyParagraphs));

    public void FollowSelectedObject()
    {
        if (Editor.IsEditing && _grid.SelectedObject?.Id != _target?.Placed.Id) Editor.Commit();
    }

    public void Begin(ShapeTextRequest request)
    {
        if (request.Placed.Content is not Drawing.Shapes.ShapeObject shape) return;
        var sheet = _workbook.Sheets[_grid.SheetIndex];
        if (!_objects.CanEditObjects(sheet)) return;
        if (_editing.IsEditing) _editing.CommitEditor();
        Editor.Commit();
        _target = (sheet, request.Placed);
        _overlay.IsHitTestVisible = true;
        Editor.SelectionBrush = _host.FindResource("PulseAccent") is ISolidColorBrush accent
            ? new SolidColorBrush(accent.Color, 0.3)
            : new SolidColorBrush(Color.FromArgb(80, 22, 163, 74));
        Editor.Begin(new ShapeTextEditStart(shape.Id, shape, Placement(request.Placed), _grid.ShapeTextBackdrop) { Typed = request.Typed });
        SelectionStale?.Invoke(this, EventArgs.Empty);
    }

    private PlacedObject? Placed() =>
        Editor.IsEditing && _target is { } target ? target.Placed
        : _grid.SelectedObjects.Count == 1 && _grid.SelectedObject is { Content: Drawing.Shapes.ShapeObject } selected ? selected
        : null;

    private bool IsActive() => Placed() is not null;

    private (int Start, int End) Range() => Editor.IsEditing ? Editor.Selection : (0, 0);

    private Drawing.Shapes.ShapeObject? Shape() =>
        Editor.IsEditing ? Editor.Live : Placed()?.Content as Drawing.Shapes.ShapeObject;

    private Styling.CellStyle CurrentStyle()
    {
        if (Editor.PendingProbe() is { } probe) return _style.Read(probe.Shape, probe.Start, probe.End);
        if (Shape() is not { } shape) return Styling.CellStyle.Default;
        var (start, end) = Range();
        return _style.Read(shape, start, end);
    }

    private void ApplyStyle(string description, Func<Styling.CellStyle, Styling.CellStyle> change)
    {
        if (!_writer.CanEdit() || Placed() is not { } placed || Shape() is not { } shape) return;
        var sheet = _target?.Sheet ?? _workbook.Sheets[_grid.SheetIndex];
        if (Editor.IsEditing && Editor.FormatRange() is null)
        {
            Editor.Pend((pending, from, to) => _style.Write(pending, from, to, change));
            return;
        }
        var (start, end) = Editor.IsEditing ? Editor.FormatRange()!.Value : Range();
        var formatted = _style.Write(shape, start, end, change);
        if (Equals(formatted, placed.Content)) return;
        var replacement = _objects.ReplaceShape(sheet, placed, formatted, _texts["history.draw.shapeTextFormat"]);
        if (replacement is null) return;
        if (Editor.IsEditing)
        {
            _target = (sheet, replacement);
            Editor.Reformat(formatted, _grid.ShapeTextBackdrop);
        }
        else _grid.SelectObject(replacement);
    }

    private (int Start, int End) ParagraphRange() => Editor.IsEditing ? Editor.Selection : (-1, -1);

    private PulseOffice.Text.Rich.TextParagraph? CurrentParagraph()
    {
        if (Shape() is not { } shape) return null;
        var (start, end) = ParagraphRange();
        return _paragraphs.First(shape, start, end);
    }

    private void ApplyParagraphs(string description, Func<PulseOffice.Text.Rich.TextParagraph, PulseOffice.Text.Rich.TextParagraph> change)
    {
        if (!_writer.CanEdit() || Placed() is not { } placed || Shape() is not { } shape) return;
        var sheet = _target?.Sheet ?? _workbook.Sheets[_grid.SheetIndex];
        var (start, end) = ParagraphRange();
        var formatted = _paragraphs.Apply(shape, start, end, change);
        if (Equals(formatted, placed.Content)) return;
        var replacement = _objects.ReplaceShape(sheet, placed, formatted, description);
        if (replacement is null) return;
        if (Editor.IsEditing)
        {
            _target = (sheet, replacement);
            Editor.Reformat(formatted, _grid.ShapeTextBackdrop);
        }
        else _grid.SelectObject(replacement);
    }

    private ShapeTextPlacement Placement(PlacedObject placed) =>
        new(_grid.ScreenBoundsOf(placed), _grid.Zoom,
            placed.Content is Drawing.Shapes.ShapeObject shape ? shape.RotationDegrees : 0);

    private void RefreshLiveShape()
    {
        if (_target is not { } target || Editor.Live is not { } live) return;
        _grid.LiveShape = live;
        Editor.Place(Placement(target.Placed));
    }

    private void OnCommitted(ShapeTextEdited edited)
    {
        _grid.LiveShape = null;
        _overlay.IsHitTestVisible = _editing.IsEditing;
        if (_target is not { } target) return;
        _target = null;
        if (!edited.Changed) return;
        var replacement = _objects.ReplaceShape(target.Sheet, target.Placed, edited.Edited, _texts["history.draw.shapeText"]);
        if (replacement is not null && ReferenceEquals(target.Sheet, _workbook.Sheets[_grid.SheetIndex])) _grid.SelectObject(replacement);
    }
}
