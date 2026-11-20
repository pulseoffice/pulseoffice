using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckTableCellEditor(DeckViewState state, IDeckTableKeys keys)
{
    private readonly TableGeometry _geometry = new();
    private TextBox? _box;
    private (Slide Slide, string ElementId, TableCellRange Cell)? _session;

    public bool IsEditing => _box is not null;

    public TextBox? Box => _box;

    public bool Begin(Slide slide, ObjectElement element, TableCellRange cell, string? typed)
    {
        if (state.Actions.IsReadOnly || element.Content is not DataTableObject table) return false;
        Commit();
        var single = new TableCellRange(cell.Top, cell.Left, cell.Top, cell.Left);
        if (keys.Text(table, single) is not { } current) return false;

        var bounds = state.Canvas.BoundsOf(element);
        var area = _geometry.Area(table, single);
        var scale = state.Canvas.Scale * PulseOffice.Foundation.Units.Twip.PerPoint;
        var look = new TableCellIndex(table).Format(single.Top, single.Left)?.Look;
        var box = new TextBox
        {
            Text = typed ?? current,
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = Math.Max(6, (look?.FontSize ?? table.Style.FontSize) * scale),
            Width = Math.Max(8, area.Width * bounds.Width),
            MinHeight = Math.Max(8, area.Height * scale),
            Padding = new Thickness(4, 2),
            VerticalContentAlignment = VerticalAlignment.Center,
            ContextFlyout = null,
        }.Themed(TextBox.BackgroundProperty, Palette.Keys.Surface);
        if (look?.FontFamily is { Length: > 0 } family) box.FontFamily = Avalonia.Media.FontFamily.Parse(family);
        if (look?.Bold == true) box.FontWeight = Avalonia.Media.FontWeight.Bold;
        if (look?.Italic == true) box.FontStyle = Avalonia.Media.FontStyle.Italic;

        state.Overlay.Margin = new Thickness(state.Canvas.Target.X, state.Canvas.Target.Y, 0, 0);
        Canvas.SetLeft(box, bounds.X + area.X * bounds.Width - state.Canvas.Target.X);
        Canvas.SetTop(box, bounds.Y + area.Y * scale - state.Canvas.Target.Y);
        state.Overlay.Children.Add(box);
        _box = box;
        _session = (slide, element.Id, single);

        box.AddHandler(InputElement.KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        box.LostFocus += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_box, box) && !box.IsFocused) Commit();
        }, Avalonia.Threading.DispatcherPriority.Input);
        box.Focus();
        box.CaretIndex = box.Text?.Length ?? 0;
        return true;
    }

    public void Commit()
    {
        if (Close() is not { } finished) return;
        var (slide, elementId, cell, text) = finished;
        state.Actions.EditTable(slide, elementId, current => keys.Write(current, cell, text));
        Reselect(slide, elementId);
    }

    public void Cancel() => Close();

    private (Slide Slide, string ElementId, TableCellRange Cell, string Text)? Close()
    {
        if (_box is not { } box || _session is not { } session) return null;
        _box = null;
        _session = null;
        state.Overlay.Children.Remove(box);
        state.Canvas.Focus();
        return (session.Slide, session.ElementId, session.Cell, box.Text ?? string.Empty);
    }

    private void Reselect(Slide slide, string elementId)
    {
        if (!ReferenceEquals(state.Current, slide)) return;
        state.Selected = slide.Find(elementId);
        state.Canvas.SelectedElement = state.Selected;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_session is not { } session) return;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            Cancel();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Tab || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift)) return;
        Commit();
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
