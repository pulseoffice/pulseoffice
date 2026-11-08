using PulseOffice.Drawing.Shapes;
using PulseOffice.Document.Forms;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI.GridParts.Gestures;
using PulseOffice.Sheets.UI.GridParts.Painting;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.GridParts.Input;

internal sealed class GridPointerInput(
    GridViewport viewport,
    SheetSelection selection,
    GridDisplayOptions options,
    IGridSurface surface,
    GridNotifications notifications,
    GridObjectSelection objects,
    GridObjectRenderers renderers,
    GridGestureFactory gestures,
    FreeformDrawing drawing,
    GridOverlayPainter overlay,
    CellDecorationPainter decorations,
    IObjectOrdering ordering,
    IObjectManipulator manipulator,
    IAdjustHandleChrome adjusting,
    IObjectFrameChrome frame,
    IPathEditing pathEditing,
    IFormControlInteraction formInteraction,
    IGroupChildLinks groupLinks)
{
    private readonly Dictionary<StandardCursorType, Cursor> _cursors = [];

    public GridGesture? Gesture { get; private set; }

    public void AbandonForReferencePick()
    {
        if (Gesture is { EndsWithReferencePick: true }) Gesture = null;
    }

    public void AbandonControlScroll()
    {
        if (Gesture is FormControlScrollGesture) Gesture = null;
    }

    public void Press(Point point, PointerPressedEventArgs e, bool rightButton)
    {
        if (rightButton)
        {
            if (!options.PickingReference) ShowContextMenu(point);
            e.Handled = true;
            return;
        }

        if (options.PickingReference)
        {
            PressSelection(point, e);
            e.Handled = true;
            return;
        }

        if (drawing.Begin(point, e)) return;

        if (BeginPathEdit(point, e)) return;

        if (!options.DesignMode && ObjectAt(point) is { Content: FormControlObject form } control
            && formInteraction.Responds(form.ControlKind)
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            PressControl(control, point, e);
            return;
        }

        if (FollowLink(point, e)) return;

        if (BeginAdjust(point, e)) return;

        if (BeginObjectDrag(point, e)) return;

        if (ObjectAt(point) is { } hitObject)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) objects.Toggle(hitObject);
            else objects.Select(hitObject);

            BeginObjectDrag(point, e);
            e.Handled = true;
            return;
        }

        objects.Select(null);

        if (BeginResize(point, e)) return;

        if (overlay.FillHandleRect().Inflate(3).Contains(point))
        {
            Begin(gestures.Fill(selection.Range), e.Pointer);
            e.Handled = true;
            return;
        }

        if (TryFilterButton(point)) return;
        if (TryChoiceButton(point))
        {
            e.Handled = true;
            return;
        }

        PressSelection(point, e);
    }

    public void Move(Point point, KeyModifiers modifiers)
    {
        if (Gesture is { Stage: GestureStage.BeforeDrawing } exclusive)
        {
            exclusive.Move(point, modifiers);
            return;
        }

        if (drawing.Drawing)
        {
            drawing.Continue(point);
            return;
        }

        ShowCommentUnder(point);

        if (Gesture is { Stage: GestureStage.BeforeCursor } leading)
        {
            leading.Move(point, modifiers);
            return;
        }

        UpdateCursor(point);
        Gesture?.Move(point, modifiers);
    }

    public void Release()
    {
        drawing.Release();

        var ending = Gesture;
        Gesture = null;
        ending?.Release();
    }

    private void Begin(GridGesture gesture, IPointer pointer)
    {
        Gesture = gesture;
        surface.Capture(pointer);
    }

    private void PressSelection(Point point, PointerPressedEventArgs e)
    {
        var hit = viewport.HitTest(point);

        switch (hit.Part)
        {
            case SheetGridView.GridPart.RowHeader when hit.Row >= 0:
                selection.SelectRow(hit.Row);
                surface.Capture(e.Pointer);
                return;

            case SheetGridView.GridPart.ColumnHeader when hit.Column >= 0:
                selection.SelectColumn(hit.Column);
                surface.Capture(e.Pointer);
                return;

            case SheetGridView.GridPart.Cell when hit.Address is { } pressed:
                var address = viewport.AnchorOf(pressed);

                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) selection.ExtendTo(address);
                else selection.MoveTo(address);

                Begin(gestures.Selection(), e.Pointer);
                return;

            default:
                return;
        }
    }

    private bool BeginResize(Point point, PointerPressedEventArgs e)
    {
        if (viewport.ColumnBorderAt(point) is var column and >= 0)
        {
            Begin(gestures.ColumnResize(column, point.X), e.Pointer);
            return true;
        }

        if (viewport.RowBorderAt(point) is var row and >= 0)
        {
            Begin(gestures.RowResize(row, point.Y), e.Pointer);
            return true;
        }

        return false;
    }

    private void UpdateCursor(Point point)
    {
        surface.ShowCursor(CursorOf(
            options.PickingReference ? StandardCursorType.Cross
            : viewport.ColumnBorderAt(point) >= 0 ? StandardCursorType.SizeWestEast
            : viewport.RowBorderAt(point) >= 0 ? StandardCursorType.SizeNorthSouth
            : StandardCursorType.Arrow));
    }

    private Cursor CursorOf(StandardCursorType type)
    {
        if (type == StandardCursorType.Arrow) return Cursor.Default;
        if (_cursors.TryGetValue(type, out var cursor)) return cursor;

        cursor = new Cursor(type);
        _cursors[type] = cursor;
        return cursor;
    }

    private void ShowContextMenu(Point point)
    {
        if (TryFilterButton(point)) return;

        if (ObjectAt(point) is { } target && !objects.Contains(target)) objects.Select(target);

        var hit = viewport.HitTest(point);

        switch (hit.Part)
        {
            case SheetGridView.GridPart.RowHeader when hit.Row >= 0:
                if (!selection.Range.Contains(new CellAddress(hit.Row, selection.Range.Start.Column)))
                    selection.SelectRow(hit.Row);

                notifications.ContextMenu("row");
                return;

            case SheetGridView.GridPart.ColumnHeader when hit.Column >= 0:
                if (!selection.Range.Contains(new CellAddress(selection.Range.Start.Row, hit.Column)))
                    selection.SelectColumn(hit.Column);

                notifications.ContextMenu("column");
                return;

            case SheetGridView.GridPart.Cell when hit.Address is { } address:
                if (!selection.Range.Contains(address)) selection.MoveTo(viewport.AnchorOf(address));

                notifications.ContextMenu("cell");
                return;
        }
    }

    private void ShowCommentUnder(Point point)
    {
        var comment = viewport.CellAt(point) is { } address && viewport.Sheet?.Comments.GetValueOrDefault(address) is { } found
            ? found
            : null;

        var text = comment is null
            ? null
            : comment.Author is { Length: > 0 } author ? $"{author}: {comment.Text}" : comment.Text;

        surface.ShowTip(text);
    }

    private bool TryFilterButton(Point point)
    {
        if (viewport.Sheet is not { } sheet) return false;
        if (viewport.HitTest(point) is not { Part: SheetGridView.GridPart.Cell, Address: { } address }) return false;
        if (decorations.FilterButtonAt(sheet, address) is not { } offset) return false;

        if (!decorations.FilterButtonBox(viewport.CellRect(address)).Contains(point)) return false;

        notifications.Filter(offset);
        return true;
    }

    private bool TryChoiceButton(Point point)
    {
        if (viewport.Sheet is null || !selection.IsSingleCell) return false;
        var active = viewport.AnchorOf(selection.Active);
        if (!options.ChoicesAvailable(active) || !overlay.ChoiceButtonBounds().Contains(point)) return false;

        notifications.Choices(active);
        return true;
    }

    private bool FollowLink(Point point, PointerPressedEventArgs e)
    {
        var modifiers = e.KeyModifiers;
        if (!(modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta)) || modifiers.HasFlag(KeyModifiers.Shift)) return false;
        if (ObjectAt(point) is not { } placed || LinkAt(placed, point) is not { } link) return false;
        notifications.FollowLink(link);
        e.Handled = true;
        return true;
    }

    private string? LinkAt(PlacedObject placed, Point point)
    {
        var bounds = viewport.BoundsOf(placed);
        if (placed.Content is ShapeGroupObject group && bounds.Width > 0 && bounds.Height > 0
            && groupLinks.MemberAt(group, (point.X - bounds.X) / bounds.Width, (point.Y - bounds.Y) / bounds.Height) is { } member
            && groupLinks.EffectiveLink(group, member) is { } own)
            return own;
        return placed.Hyperlink is { Length: > 0 } link ? link : null;
    }

    private PlacedObject? ObjectAt(Point point)
    {
        if (viewport.Sheet is not { } sheet) return null;

        var drawn = ordering.Drawn(sheet);

        for (var index = drawn.Count - 1; index >= 0; index--)
            if (viewport.BoundsOf(drawn[index]).Contains(point)) return drawn[index];

        return null;
    }

    private bool BeginPathEdit(Point point, PointerPressedEventArgs e)
    {
        if (drawing.Tools is not { EditingPoints: true }) return false;
        if (objects.Last is not { Content: FreeformObject freeform } placed) return false;

        var bounds = viewport.BoundsOf(placed);
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        var local = new PulseOffice.Drawing.Shapes.ShapePoint(
            (point.X - bounds.X) / bounds.Width, (point.Y - bounds.Y) / bounds.Height);

        var handle = pathEditing.HandleAt(freeform.Path, local, 8 / Math.Max(bounds.Width, bounds.Height));

        if (!handle.Exists) return false;

        Begin(gestures.PathEdit(placed, handle), e.Pointer);
        e.Handled = true;

        return true;
    }

    private void PressControl(PlacedObject placed, Point point, PointerPressedEventArgs e)
    {
        e.Handled = true;

        if (placed.Content is not FormControlObject form) return;

        var bounds = viewport.BoundsOf(placed);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var relativeX = (point.X - bounds.X) / bounds.Width;
        var relativeY = (point.Y - bounds.Y) / bounds.Height;
        var horizontal = bounds.Width >= bounds.Height;

        var changed = formInteraction.Press(
            form.ControlKind, form.Properties, relativeX, relativeY, renderers.ItemsOf(viewport, form).Count, horizontal, bounds.Height);

        if (form.ControlKind == FormControlKind.ScrollBar) Begin(gestures.FormControlScroll(placed), e.Pointer);

        if (changed is null) return;

        notifications.FormControl(new FormControlInteractionEventArgs(placed, changed));
    }

    private double AdjustSize => manipulator.HandleSize / 2 + 1;

    private bool BeginAdjust(Point point, PointerPressedEventArgs e)
    {
        if (objects.Last is not { } placed || objects.Adjustable(placed) is not { } shape) return false;
        var frame = new AdjustFrame(viewport.BoundsOf(placed), viewport.RotationOf(placed));
        if (adjusting.Grip(shape, frame, point, AdjustSize + 1) is not { } grip) return false;

        Begin(gestures.ShapeAdjust(placed, grip), e.Pointer);
        e.Handled = true;
        return true;
    }

    private bool BeginObjectDrag(Point point, PointerPressedEventArgs e)
    {
        if (objects.Last is not { } placed) return false;

        var bounds = viewport.BoundsOf(placed);
        var box = new ObjectBox(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var rotation = viewport.RotationOf(placed);
        var handle = frame.HandleAt(new FrameScene(box, rotation, Brushes.Transparent), point);

        if (handle is ObjectHandle.None) return false;

        Begin(gestures.ObjectDrag(handle, point, box, rotation), e.Pointer);
        e.Handled = true;
        return true;
    }
}
