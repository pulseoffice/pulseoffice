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

internal sealed class DeckShapeTextEditing(DeckViewState state, IDeckViewHost host, DeckFilmstrip filmstrip, PulseOffice.UI.Controls.Shapes.ShapeTextEditor shapeText, PulseOffice.Drawing.Shapes.IShapeRichText shapeRichText)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly DeckActions _actions = state.Actions;
    private readonly SlideView _canvas = state.Canvas;
    private readonly DeckFilmstrip _filmstrip = filmstrip;
    private readonly PulseOffice.UI.Controls.Shapes.ShapeTextEditor _shapeText = shapeText;
    private readonly PulseOffice.Drawing.Shapes.IShapeRichText _shapeRichText = shapeRichText;

    private Slide? _shapeTextSlide;

    public PulseOffice.UI.Controls.Shapes.ShapeTextEditor Editor => _shapeText;

    public void Attach()
    {
        _shapeText.LiveChanged += (_, _) => RefreshLiveShape();
        _shapeText.Committed += (_, edited) => OnShapeTextCommitted(edited);
        _shapeText.Exited += (_, _) => _host.FocusDocument();
        _shapeText.SelectionMoved += (_, _) => _host.Reannounce();
        _shapeText.PendingChanged += (_, _) =>
        {
            _host.Reannounce();
            _host.NotifyRibbon();
        };
        _state.Overlay.Children.Add(_shapeText);
    }

    public PulseOffice.Text.Rich.RichText? ShapeRichTextInFocus()
    {
        if (_shapeText.PendingProbe() is { } probe) return _shapeRichText.ToRich(probe.Shape);
        if (_shapeText.IsEditing) return _shapeText.Live is { } live ? _shapeRichText.ToRich(live) : null;
        if (_state.Editing is not null || _state.SelectedText is not null) return null;
        return _state.Selected is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject shape } ? _shapeRichText.ToRich(shape) : null;
    }

    public void BeginShapeText(ObjectElement element, string? typed)
    {
        if (_actions.IsReadOnly || _state.Current is null || element.Content is not PulseOffice.Drawing.Shapes.ShapeObject shape) return;
        _host.CommitEditor();
        _shapeText.Commit();
        _shapeTextSlide = _state.Current;
        _canvas.SelectedElement = element;
        _state.Selected = element;
        _state.SelectedText = null;
        _state.Overlay.Margin = new Thickness(_canvas.Target.X, _canvas.Target.Y, 0, 0);
        _shapeText.SelectionBrush = _host.Accent() is ISolidColorBrush accent ? new SolidColorBrush(accent.Color, 0.3) : new SolidColorBrush(Color.FromArgb(80, 37, 99, 235));
        _shapeText.Begin(new PulseOffice.UI.Controls.Shapes.ShapeTextEditStart(element.Id, shape, ShapePlacement(element, shape), new PulseOffice.UI.Controls.Shapes.ShapeSurroundings()) { Typed = typed });
        _host.AttachContext();
    }

    private PulseOffice.UI.Controls.Shapes.ShapeTextPlacement ShapePlacement(SlideElement element, PulseOffice.Drawing.Shapes.ShapeObject shape)
    {
        var bounds = _canvas.BoundsOf(element);
        return new(bounds.Translate(new Vector(-_canvas.Target.X, -_canvas.Target.Y)),
            _canvas.Scale * PulseOffice.Foundation.Units.Twip.PerPoint, element.RotationDegrees + shape.RotationDegrees);
    }

    private void RefreshLiveShape()
    {
        if (_state.Current is null || _shapeText.EditingId is not { } id || _shapeText.Live is not { } shape) return;
        if (_state.Current.Find(id) is not ObjectElement element) return;
        var live = element with { Content = shape };
        _canvas.LiveText = live;
        if (_filmstrip.CurrentThumbnail is { } thumbnail) thumbnail.LiveText = live;
        _shapeText.Place(ShapePlacement(element, shape));
    }

    private void OnShapeTextCommitted(PulseOffice.UI.Controls.Shapes.ShapeTextEdited edited)
    {
        _canvas.LiveText = null;
        if (_filmstrip.CurrentThumbnail is { } thumbnail) thumbnail.LiveText = null;
        var slide = _shapeTextSlide ?? _state.Current;
        _shapeTextSlide = null;
        if (slide is null || slide.Find(edited.Id) is not ObjectElement element) return;
        if (edited.Changed) _actions.EditShapeText(slide, element, edited.Edited, format: false);
        if (ReferenceEquals(slide, _state.Current) && slide.Find(edited.Id) is { } updated)
        {
            _canvas.SelectedElement = updated;
            _state.Selected = updated;
        }
        _host.AttachContext();
    }

    public bool ApplyShapeTextFormat(Func<RichText, (int Start, int End)?, RichText> transform)
    {
        if (_state.Current is not { } slide) return false;
        var id = _shapeText.EditingId ?? (_state.Selected as ObjectElement)?.Id;
        if (id is null || slide.Find(id) is not ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject } element) return false;

        if (_shapeText.IsEditing)
        {
            if (_shapeText.FormatRange() is not { } range)
            {
                _shapeText.Pend((pending, from, to) => _shapeRichText.FromRich(pending, transform(_shapeRichText.ToRich(pending), (from, to))));
                return true;
            }
            var live = _shapeText.Live!;
            var formatted = _shapeRichText.FromRich(live, transform(_shapeRichText.ToRich(live), range));
            _actions.EditShapeText(slide, element, formatted, format: true);
            _shapeText.Reformat(formatted, new PulseOffice.UI.Controls.Shapes.ShapeSurroundings());
            return true;
        }

        var shape = (PulseOffice.Drawing.Shapes.ShapeObject)element.Content;
        var whole = _shapeRichText.FromRich(shape, transform(_shapeRichText.ToRich(shape), null));
        _actions.EditShapeText(slide, element, whole, format: true);
        _state.Selected = slide.Find(id);
        _canvas.SelectedElement = _state.Selected;
        return true;
    }
}
