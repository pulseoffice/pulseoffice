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

internal sealed class DeckStageKeys(DeckViewState state, IDeckViewHost host, DeckTextEditing text, DeckShapeTextEditing shapes, DeckCanvasEditing canvasEditing, IDeckDrawingState drawing, ISlideKeyNavigation slideKeys, PulseOffice.UI.Controls.Services.IDocumentKeyScope keys, PulseOffice.UI.Controls.Shortcuts.TypingChordGuard chords, PulseOffice.UI.Controls.Shapes.IObjectNudgeKeys nudges)
{
    private static readonly PulseOffice.UI.Controls.Shapes.NudgeSteps TwipSteps = new(60, 300);
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly Presentation _deck = state.Deck;
    private readonly DeckActions _actions = state.Actions;
    private readonly SlideView _canvas = state.Canvas;
    private readonly DeckTextEditing _text = text;
    private readonly DeckShapeTextEditing _shapes = shapes;
    private readonly DeckCanvasEditing _canvasEditing = canvasEditing;
    private readonly IDeckDrawingState _drawing = drawing;
    private readonly ISlideKeyNavigation _slideKeys = slideKeys;
    private readonly PulseOffice.UI.Controls.Services.IDocumentKeyScope _keys = keys;
    private readonly PulseOffice.UI.Controls.Shortcuts.TypingChordGuard _chords = chords;

    private bool IsTextInput(object? source)
    {
        for (var node = source as Avalonia.Visual; node is not null && !ReferenceEquals(node, _host.View); node = Avalonia.VisualTree.VisualExtensions.GetVisualParent(node))
            if (node is TextBox or PulseOffice.UI.Controls.Text.ITextEditingTarget) return true;
        return false;
    }

    public void TextInput(TextInputEventArgs e)
    {
        if (e.Handled || _text.Editor is not null || _actions.IsReadOnly || IsTextInput(e.Source)) return;
        if (!ReferenceEquals(e.Source, _host.View) && !_keys.FromSurface(e.Source, _canvas)) return;
        if (_chords.Rejects(e) || e.Text is not { } typed || _canvas.Selection.Count > 1) return;
        if (_shapes.Editor.IsEditing) return;
        if (_state.Current?.Find(_state.Selected?.Id ?? string.Empty) is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject } shapeElement)
        {
            _shapes.BeginShapeText(shapeElement, typed);
            e.Handled = true;
            return;
        }
        if (_state.Current is null || _state.Selected is null || _state.Current.Find(_state.Selected.Id) is not TextElement text) return;

        _text.BeginEdit(text);
        if (_text.Editor is not { } editor) return;

        editor.Text = (editor.Text ?? string.Empty) + typed;
        editor.CaretIndex = editor.Text.Length;
        e.Handled = true;
    }

    public void KeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _drawing.Pen != DeckPen.None)
        {
            _drawing.Finish();
            e.Handled = true;
            return;
        }

        if (e.Handled || IsTextInput(e.Source) || !ReferenceEquals(e.Source, _host.View) && !_keys.FromSurface(e.Source, _canvas)) return;
        if (_text.Editor is null && _state.Selected is null && _state.Current is not null && !_shapes.Editor.IsEditing
            && _slideKeys.Target(e.Key, e.KeyModifiers, _deck.IndexOf(_state.Current), _deck.Slides.Count, listFocused: false) is { } slideTarget)
        {
            _host.GoToSlide(slideTarget);
            e.Handled = true;
            return;
        }
        if (_text.Editor is not null || _state.Current is null || _state.Selected is null) return;
        if (_state.Current.Find(_state.Selected.Id) is not { } element) return;

        var ids = _canvas.Selection.Select(s => s.Id).ToList();
        if (ids.Count == 0) ids.Add(element.Id);

        if (e.Key is Key.Enter or Key.F2 && e.KeyModifiers == KeyModifiers.None && ids.Count == 1 && element is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject } shapeElement && !_actions.IsReadOnly)
        {
            _shapes.BeginShapeText(shapeElement, null);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.F2 && e.KeyModifiers == KeyModifiers.None && ids.Count == 1 && element is TextElement text && !_actions.IsReadOnly)
        {
            _text.BeginEdit(text);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            if (_actions.IsReadOnly) return;
            _actions.DeleteElements(_state.Current, [.. ids.Select(_state.Current.Find).OfType<SlideElement>()], "deck.action.deleteObject");
            _state.Selected = null;
            _state.SelectedText = null;
            _canvas.SelectedElement = null;
            e.Handled = true;
            return;
        }

        if (nudges.Read(e.Key, e.KeyModifiers) is not { } nudge) return;

        var (dx, dy) = nudge.Distance(TwipSteps);
        _canvasEditing.MoveBy(ids, (int)dx, (int)dy);
        e.Handled = true;
    }
}
