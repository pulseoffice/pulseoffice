using PulseOffice.Drawing.Shapes;
using Avalonia.Input;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.GridParts.Input;

internal sealed class GridKeyboardInput(
    GridViewport viewport,
    SheetSelection selection,
    GridDisplayOptions options,
    IGridSurface surface,
    GridNotifications notifications,
    GridObjectSelection objects,
    GridRenderServices services,
    FreeformDrawing drawing,
    PulseOffice.UI.Controls.Shortcuts.TypingChordGuard chords,
    PulseOffice.UI.Controls.Shapes.IObjectNudgeKeys nudges)
{
    private static readonly PulseOffice.UI.Controls.Shapes.NudgeSteps PixelSteps = new(1, 10);

    public bool KeyDown(KeyEventArgs e)
    {
        if (options.PickingReference && PickKey(e)) return true;

        if (e.Key == Key.Escape && (drawing.Drawing || drawing.Sketch.Active))
        {
            drawing.Cancel();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Enter && drawing.Sketch.Points.Count >= 2)
        {
            drawing.Complete(closed: false);
            e.Handled = true;
            return true;
        }

        var extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var jump = e.KeyModifiers.HasFlag(PulseOffice.UI.Controls.Shortcuts.ShortcutGesture.Primary);
        var sheet = viewport.Sheet;
        var navigator = services.Navigator;

        if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt && sheet is not null)
        {
            notifications.Choices(viewport.AnchorOf(selection.Active));
            e.Handled = true;
            return true;
        }

        if (objects.Count > 0 && nudges.Read(e.Key, e.KeyModifiers) is { } nudge)
        {
            Nudge(nudge);
            e.Handled = true;
            return true;
        }

        switch (e.Key)
        {
            case Key.Up when jump: Jump(-1, 0, extend); break;
            case Key.Down when jump: Jump(1, 0, extend); break;
            case Key.Left when jump: Jump(0, -1, extend); break;
            case Key.Right when jump: Jump(0, 1, extend); break;
            case Key.Home when jump: Place(viewport.AnchorOf(new CellAddress(0, 0)), extend); break;
            case Key.End when jump && sheet is { } usedSheet: Place(navigator.LastUsed(usedSheet), extend); break;
            case Key.End when sheet is { } rowSheet: Place(viewport.AnchorOf(navigator.RowEnd(rowSheet, extend ? selection.Corner : selection.Active)), extend); break;
            case Key.Up: Navigate(-1, 0, extend); break;
            case Key.Down: Navigate(1, 0, extend); break;
            case Key.Enter when !selection.IsSingleCell: selection.Advance(byRow: true, backwards: extend); break;
            case Key.Enter: AdvanceSingle(byRow: true, backwards: extend); break;
            case Key.Left: Navigate(0, -1, extend); break;
            case Key.Right: Navigate(0, 1, extend); break;
            case Key.Tab when !selection.IsSingleCell: selection.Advance(byRow: false, backwards: extend); break;
            case Key.Tab: AdvanceSingle(byRow: false, backwards: extend); break;
            case Key.PageUp: Navigate(-viewport.PageRows(), 0, extend); break;
            case Key.PageDown: Navigate(viewport.PageRows(), 0, extend); break;
            case Key.Home:
                Place(viewport.AnchorOf(new CellAddress((extend ? selection.Corner : selection.Active).Row, 0)), extend);
                break;

            case Key.F2:
                notifications.Edit(selection.Active);
                e.Handled = true;
                return true;

            case Key.Delete or Key.Back when objects.Count > 0:
                notifications.DeleteObjects([.. objects.Items]);
                e.Handled = true;
                return true;

            case Key.Delete or Key.Back:
                notifications.Clear(selection.Range);
                break;

            default:
                return false;
        }

        viewport.EnsureVisible(extend && !selection.IsSingleCell ? selection.Corner : selection.Active);
        surface.Invalidate();
        e.Handled = true;
        return true;
    }

    private void Nudge(PulseOffice.UI.Controls.Shapes.ObjectNudge nudge)
    {
        var (dx, dy) = nudge.Distance(PixelSteps);
        notifications.ObjectChanges([.. objects.Items.Select(placed =>
        {
            var bounds = viewport.BoundsOf(placed);
            return viewport.ChangeOf(placed, new ObjectBox(bounds.X + dx, bounds.Y + dy, bounds.Width, bounds.Height), null);
        })]);
    }

    public void TextInput(TextInputEventArgs e)
    {
        if (options.PickingReference)
        {
            e.Handled = true;
            return;
        }

        if (chords.Rejects(e) || e.Text is not { } typed) return;

        if (objects.Count == 1 && objects.Last is { Content: PulseOffice.Drawing.Shapes.ShapeObject } shape)
        {
            notifications.ShapeText(new ShapeTextRequest(shape, typed));
            e.Handled = true;
            return;
        }

        if (objects.Count > 0)
        {
            e.Handled = true;
            return;
        }

        notifications.Typing(typed);
    }

    public void DoubleTapped(Avalonia.Input.TappedEventArgs e)
    {
        if (options.PickingReference)
        {
            e.Handled = true;
            return;
        }

        if (objects.Last is { Content: PulseOffice.Drawing.Shapes.ShapeObject } shape && objects.Count == 1)
        {
            notifications.ShapeText(new ShapeTextRequest(shape, null));
            e.Handled = true;
            return;
        }

        if (objects.Last is { } placed)
        {
            notifications.Activate(placed);
            e.Handled = true;
            return;
        }

        notifications.Edit(selection.Active);
        e.Handled = true;
    }

    private bool PickKey(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                notifications.CommitReference();
                break;

            case Key.Escape:
                notifications.CancelReference();
                break;

            case Key.F2 or Key.Delete or Key.Back:
                break;

            default:
                return false;
        }

        e.Handled = true;
        return true;
    }

    private void Jump(int rowStep, int columnStep, bool extend)
    {
        if (viewport.Sheet is not { } sheet) return;
        Place(viewport.AnchorOf(services.Navigator.Jump(sheet, extend ? selection.Corner : selection.Active, rowStep, columnStep)), extend);
    }

    private void Place(CellAddress target, bool extend)
    {
        if (extend) selection.ExtendTo(target);
        else selection.MoveTo(target);
    }

    private void AdvanceSingle(bool byRow, bool backwards)
    {
        if (viewport.Sheet is not { } sheet)
        {
            selection.Advance(byRow, backwards);
            return;
        }

        var step = backwards ? -1 : 1;
        var target = selection.RowEntryReturn(byRow, backwards)
            ?? services.Navigator.Step(sheet, selection.Active, byRow ? step : 0, byRow ? 0 : step);
        selection.AdvanceTo(target, byRow, backwards);
    }

    private void Navigate(int rowDelta, int columnDelta, bool extend)
    {
        if (viewport.Sheet is not { } sheet)
        {
            selection.Move(rowDelta, columnDelta, extend);
            return;
        }

        Place(services.Navigator.Step(sheet, extend ? selection.Corner : selection.Active, rowDelta, columnDelta), extend);
    }
}
