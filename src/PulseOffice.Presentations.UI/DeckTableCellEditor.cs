using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

internal interface IDeckCellTextTarget
{
    bool Owns(string id);

    void Refresh();

    void Finish(ShapeTextEdited edited);

    bool Format(Func<RichText, (int Start, int End)?, RichText> transform);
}

internal sealed class DeckTableCellEditor(DeckViewState state, IDeckTableKeys keys, DeckShapeTextEditing shapes, IDeckTableCellShapes cells, IShapeRichText richText) : IDeckCellTextTarget
{
    private const string CellSuffix = "#cell";

    private readonly TableGeometry _geometry = new();
    private CellSession? _session;
    private bool _cancelled;

    private sealed record CellSession(Slide Slide, string ElementId, string ShapeId, TableCellRange Cell, TableCellText Original, TableCellShapeLook Look);

    public bool IsEditing => _session is not null && shapes.Editor.IsEditing;

    public void Attach()
    {
        shapes.Route(this);
        shapes.Editor.AddHandler(InputElement.KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    public bool Begin(Slide slide, ObjectElement element, TableCellRange cell, string? typed)
    {
        if (state.Actions.IsReadOnly || element.Content is not DataTableObject table) return false;
        Commit();
        var single = new TableCellRange(cell.Top, cell.Left, cell.Top, cell.Left);
        if (keys.Text(table, single) is not { } current) return false;

        var format = new TableCellIndex(table).Format(single.Top, single.Left);
        var plainLook = single.Top == TableCellFormat.HeaderRow && !table.Style.OwnLook ? TableCellLook.Plain with { Bold = true } : TableCellLook.Plain;
        var look = new TableCellShapeLook(format?.Look ?? plainLook, format?.VerticalAlign ?? CellVerticalAlign.Top, table.Style.FontSize, format?.Margins);
        var own = table.CellTexts.FirstOrDefault(text => text.Row == single.Top && text.Column == single.Left);
        var original = cells.CellText(single.Top, single.Left, current, own);
        var start = typed is null ? original : cells.CellText(single.Top, single.Left, string.Empty, null);
        var shapeId = element.Id + CellSuffix;

        _session = new CellSession(slide, element.Id, shapeId, single, original, look);
        shapes.BeginCell(new ShapeTextEditStart(shapeId, cells.Shape(shapeId, start, look), Placement(element, table, single), shapes.Surroundings()) { Typed = typed });
        return true;
    }

    public void Commit()
    {
        if (_session is not null && shapes.Editor.IsEditing) shapes.Editor.Commit();
    }

    public void Cancel()
    {
        _cancelled = true;
        try
        {
            Commit();
        }
        finally
        {
            _cancelled = false;
        }
    }

    public bool Owns(string id) => _session is { } session && session.ShapeId == id;

    public void Refresh()
    {
        if (_session is not { } session || shapes.Editor.Live is not { } live) return;
        if (session.Slide.Find(session.ElementId) is not ObjectElement { Content: DataTableObject table } element) return;
        state.Canvas.LiveText = element with { Content = keys.WriteRich(table, session.Cell, string.Empty, null) };
        state.Canvas.LiveOverlay = new ObjectElement { Id = session.ShapeId, Bounds = CellBounds(element, table, session.Cell), Content = live };
        shapes.Editor.Place(Placement(element, table, session.Cell));
    }

    public void Finish(ShapeTextEdited edited)
    {
        ArgumentNullException.ThrowIfNull(edited);
        state.Canvas.LiveText = null;
        state.Canvas.LiveOverlay = null;
        if (_session is not { } session) return;
        _session = null;
        if (!_cancelled && edited.Changed)
        {
            var text = cells.Cell(edited.Edited, session.Original, session.Look);
            state.Actions.EditTable(session.Slide, session.ElementId, current => keys.WriteRich(current, session.Cell, text.PlainText, text));
        }
        Reselect(session.Slide, session.ElementId);
    }

    public bool Format(Func<RichText, (int Start, int End)?, RichText> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var editor = shapes.Editor;
        if (_session is null || !editor.IsEditing || editor.Live is not { } live) return false;
        if (editor.FormatRange() is not { } range)
        {
            editor.Pend((pending, from, to) => richText.FromRich(pending, transform(richText.ToRich(pending), (from, to))));
            return true;
        }
        editor.Reformat(richText.FromRich(live, transform(richText.ToRich(live), range)), shapes.Surroundings());
        return true;
    }

    private ShapeTextPlacement Placement(ObjectElement element, DataTableObject table, TableCellRange cell)
    {
        var bounds = state.Canvas.BoundsOf(element);
        var area = _geometry.Area(table, cell);
        var scale = state.Canvas.Scale * Twip.PerPoint;
        return new ShapeTextPlacement(
            new Rect(bounds.X + area.X * bounds.Width - state.Canvas.Target.X, bounds.Y + area.Y * scale - state.Canvas.Target.Y,
                Math.Max(8, area.Width * bounds.Width), Math.Max(8, area.Height * scale)),
            scale, element.RotationDegrees);
    }

    private DocumentRect CellBounds(ObjectElement element, DataTableObject table, TableCellRange cell)
    {
        var area = _geometry.Area(table, cell);
        var box = element.Bounds;
        return new DocumentRect(
            new Twip((int)Math.Round(box.Left.Value + area.X * box.Width.Value)),
            new Twip((int)Math.Round(box.Top.Value + area.Y * Twip.PerPoint)),
            new Twip((int)Math.Round(area.Width * box.Width.Value)),
            new Twip((int)Math.Round(area.Height * Twip.PerPoint)));
    }

    private void Reselect(Slide slide, string elementId)
    {
        if (!ReferenceEquals(state.Current, slide)) return;
        state.Selected = slide.Find(elementId);
        state.Canvas.SelectedElement = state.Selected;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_session is not { } session || !shapes.Editor.IsEditing) return;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            Cancel();
            state.Canvas.Focus();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Tab || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift)) return;
        Commit();
        state.Canvas.Focus();
        e.Handled = true;
        if (session.Slide.Find(session.ElementId) is not ObjectElement { Content: DataTableObject table }) return;
        if (keys.Read(table, session.Cell, e.Key, e.KeyModifiers) is not { } outcome) return;
        if (outcome is not TableKeyMove)
        {
            state.Actions.EditTable(session.Slide, session.ElementId, current => keys.Apply(current, outcome));
            Reselect(session.Slide, session.ElementId);
        }
        state.Options.SetTableCells((session.ElementId, outcome.Range));
    }
}
