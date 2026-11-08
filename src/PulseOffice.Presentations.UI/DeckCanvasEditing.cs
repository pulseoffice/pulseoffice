using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckCanvasEditing(DeckViewState state, IDeckViewHost host, IDeckConnectors connectors)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly DeckActions _actions = state.Actions;
    private readonly SlideView _canvas = state.Canvas;
    private readonly DeckViewOptions _options = state.Options;
    private readonly IDeckConnectors _connectors = connectors;

    public void Attach()
    {
        _canvas.ControlPressed += OnControlPressed;
        _canvas.TableCellPicked += (_, picked) =>
        {
            var range = picked.Extend && _options.TableCells is { } current && current.ElementId == picked.Element.Id
                ? current.Range with { LastRow = picked.Row, LastColumn = picked.Column }
                : new PulseOffice.Document.Tables.TableCellRange(picked.Row, picked.Column, picked.Row, picked.Column);
            _options.SetTableCells((picked.Element.Id, range));
        };
        _canvas.SelectionChanged += (_, element) =>
        {
            if (_options.TableCells is { } cells && element?.Id != cells.ElementId) _options.SetTableCells(null);
        };
        _canvas.ElementChangeRequested += OnElementChangeRequested;
        _canvas.ElementsMoveRequested += OnElementsMoveRequested;
        _canvas.GroupTransformRequested += OnGroupTransformRequested;
        _canvas.FreeformCompleted += OnFreeformCompleted;
        _canvas.GlueConnectRequested += (_, glue) =>
        {
            if (_state.Current is null) return;
            if (_state.Current.Find(glue.FromId) is not ObjectElement from || _state.Current.Find(glue.ToId) is not ObjectElement to) return;
            if (_connectors.Connect(_state.Current, from, to, Guid.NewGuid().ToString("N"), PulseOffice.Drawing.Shapes.ConnectorKind.Elbow, glue.FromSite, glue.ToSite) is { } element)
                _actions.InsertObject(_state.Current, element);
        };
        _canvas.ShapeAdjusted += (_, edit) =>
        {
            if (_state.Current is null) return;
            _actions.AdjustShape(_state.Current, edit.Element, edit.Shape);
            _state.Selected = _state.Current.Find(edit.Element.Id);
            _canvas.SelectedElement = _state.Selected;
        };
        _canvas.FreeformPathEdited += (_, edit) =>
        {
            if (_state.Current is null) return;
            _actions.EditFreeformPath(_state.Current, edit.Element, edit.Path);
            _state.Selected = _state.Current.Find(edit.Element.Id);
            _canvas.SelectedElement = _state.Selected;
        };
    }

    private void OnControlPressed(object? sender, (ObjectElement Element, PulseOffice.Document.Forms.FormControlProperties Changed) e)
    {
        if (_state.Current is not { } slide || e.Element.Content is not PulseOffice.Document.Forms.FormControlObject form) return;
        _actions.ChangeControl(slide, e.Element with { Content = form with { Properties = e.Changed } });
    }

    private void OnElementChangeRequested(object? sender, ElementTransform change)
    {
        if (_state.Current is null) return;
        _actions.TransformElement(_state.Current, change.Element.Id, change.Bounds, change.Rotation);
        _state.Selected = _state.Current.Find(change.Element.Id);
        _state.SelectedText = _state.Selected as TextElement;
        _canvas.SelectedElement = _state.Selected;
    }

    private void OnGroupTransformRequested(object? sender, GroupTransformRequest change)
    {
        if (_state.Current is null) return;
        var ids = _canvas.Selection.Select(element => element.Id).ToList();
        _actions.TransformGroup(_state.Current, change.Group, change.Bounds, change.Rotation);
        var current = _state.Current;
        if (_state.Selected is not null) _state.Selected = current.Find(_state.Selected.Id);
        _state.SelectedText = _state.Selected as TextElement;
        _canvas.SetSelection([.. ids.Select(current.Find).OfType<SlideElement>()], _state.Selected);
    }

    private void OnElementsMoveRequested(object? sender, (IReadOnlyList<string> Ids, int Dx, int Dy) move) =>
        MoveBy(move.Ids, move.Dx, move.Dy);

    private void OnFreeformCompleted(object? sender, FreeformReady ready)
    {
        if (_state.Current is null) return;

        var freeform = new PulseOffice.Drawing.Freeform.FreeformObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Path = ready.Path,
            Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default,
        };

        var element = new ObjectElement { Id = freeform.Id, Bounds = ready.Bounds, ZOrder = _state.Current.Elements.Count, Content = freeform };
        _actions.InsertObject(_state.Current, element);
        _host.SelectElements([element]);
    }

    public void MoveBy(IReadOnlyList<string> ids, int dx, int dy)
    {
        if (_state.Current is null || (dx == 0 && dy == 0)) return;

        var placements = new List<(string Id, PulseOffice.Foundation.Units.DocumentRect Bounds)>();
        foreach (var id in ids)
        {
            if (_state.Current.Find(id) is not { } target) continue;
            var b = target.Bounds;
            placements.Add((id, new PulseOffice.Foundation.Units.DocumentRect(
                b.Left + new PulseOffice.Foundation.Units.Twip(dx),
                b.Top + new PulseOffice.Foundation.Units.Twip(dy),
                b.Width, b.Height)));
        }

        if (placements.Count == 0) return;
        _actions.AlignElements(_state.Current, placements);
    }
}
