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

internal sealed class DeckCanvasMenus(DeckViewState state, IDeckViewHost host, DeckTextEditing text, DeckShapeTextEditing shapes, DeckFilmstrip filmstrip, ControlAncestry ancestry, PulseOffice.UI.Controls.Services.IContextMenuFactory contextMenus, ICanvasContextChooser contextChooser, PulseOffice.UI.Controls.Services.IContextMenuKey menuKey, IDeckDrawingState drawing)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly SlideView _canvas = state.Canvas;
    private readonly DeckViewOptions _options = state.Options;
    private readonly DeckTextEditing _text = text;
    private readonly DeckShapeTextEditing _shapes = shapes;
    private readonly DeckFilmstrip _filmstrip = filmstrip;
    private readonly ControlAncestry _ancestry = ancestry;
    private readonly PulseOffice.UI.Controls.Services.IContextMenuFactory _contextMenus = contextMenus;
    private readonly ICanvasContextChooser _contextChooser = contextChooser;
    private readonly PulseOffice.UI.Controls.Services.IContextMenuKey _menuKey = menuKey;
    private readonly IDeckDrawingState _drawing = drawing;

    public void OnMenuKey(object? sender, KeyEventArgs e)
    {
        if (!_menuKey.Matches(e)) return;
        if (TopLevel.GetTopLevel(_host.View)?.FocusManager?.GetFocusedElement() is Control focused && _ancestry.IsInside(focused, _filmstrip.Panel))
        {
            e.Handled = true;
            _contextMenus.OpenAt("slide", focused, Middle(focused));
            return;
        }
        Control? editor = _shapes.Editor.IsEditing ? _shapes.Editor.Box : _text.Editor;
        var target = editor is not null ? CanvasPressTarget.Editor
            : _state.Selected is not null ? CanvasPressTarget.Element
            : CanvasPressTarget.Empty;
        if (_contextChooser.RootFor(new CanvasPress(target, CanvasModeNow())) is not { } root) return;
        e.Handled = true;
        if (editor is null)
        {
            _contextMenus.OpenAt(root, _canvas, Middle(_canvas), () => _canvas.Focus());
            return;
        }
        _state.EditorMenuOpen = true;
        if (!_contextMenus.OpenAt(root, editor, Middle(editor), () =>
            {
                _state.EditorMenuOpen = false;
                editor.Focus();
            }))
            _state.EditorMenuOpen = false;
    }

    private Rect Middle(Control control) => new(control.Bounds.Width / 2, control.Bounds.Height / 2, 1, 1);

    public bool OpenCanvasMenu(PointerPressedEventArgs e)
    {
        var editor = _text.Editor is { } box && e.Source is Control source && _text.InEditor(source)
            ? box
            : null;
        var element = editor is null ? _canvas.ElementAt(e.GetPosition(_canvas)) : null;
        var target = editor is not null ? CanvasPressTarget.Editor
            : element is not null ? CanvasPressTarget.Element
            : CanvasPressTarget.Empty;

        if (_contextChooser.RootFor(new CanvasPress(target, CanvasModeNow())) is not { } root) return false;

        e.Handled = true;

        if (editor is null)
        {
            _text.CommitEditor();
            if (element is not null) _canvas.EnsureSelected(element);
            _canvas.Focus();
        }

        if (editor is null)
        {
            _contextMenus.Open(root, _canvas);
            return true;
        }

        _state.EditorMenuOpen = true;
        if (!_contextMenus.Open(root, editor, () =>
            {
                _state.EditorMenuOpen = false;
                _text.Editor?.Focus();
            }))
            _state.EditorMenuOpen = false;
        return true;
    }

    private CanvasMode CanvasModeNow() =>
        _drawing.EditingPoints ? CanvasMode.Points
        : _drawing.GluePoints ? CanvasMode.Glue
        : _drawing.Pen != DeckPen.None ? CanvasMode.Drawing
        : _options.Tool != CanvasPointerTool.Select ? CanvasMode.Tool
        : CanvasMode.Normal;
}
