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

internal sealed class DeckTextEditing(DeckViewState state, IDeckViewHost host, DeckFilmstrip filmstrip, DeckShapeTextEditing shapes, DeckTextCorrection correction, DeckRulers rulers, ControlAncestry ancestry, PulseOffice.UI.Controls.Text.Rich.ITextFrameComposer composer, PulseOffice.UI.Controls.Text.Rich.ITextCaretGeometry caretGeometry, IPlainTextPositions plainPositions, PulseOffice.UI.Controls.Text.Editing.InlineEditingServices editingServices, ITextEditSplice splice, ICaretWord caretWord, PulseOffice.UI.Controls.Text.ITextEditingFocus editingFocus)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly Presentation _deck = state.Deck;
    private readonly DeckActions _actions = state.Actions;
    private readonly SlideView _canvas = state.Canvas;
    private readonly DeckFilmstrip _filmstrip = filmstrip;
    private readonly DeckShapeTextEditing _shapes = shapes;
    private readonly DeckTextCorrection _correction = correction;
    private readonly DeckRulers _rulers = rulers;
    private readonly ControlAncestry _ancestry = ancestry;
    private readonly PulseOffice.UI.Controls.Text.Rich.ITextFrameComposer _composer = composer;
    private readonly PulseOffice.UI.Controls.Text.Rich.ITextCaretGeometry _caretGeometry = caretGeometry;
    private readonly IPlainTextPositions _plainPositions = plainPositions;
    private readonly PulseOffice.UI.Controls.Text.Editing.InlineEditingServices _editingServices = editingServices;
    private readonly ITextEditSplice _splice = splice;
    private readonly ICaretWord _caretWord = caretWord;
    private readonly PulseOffice.UI.Controls.Text.ITextEditingFocus _editingFocus = editingFocus;

    private TextBox? _editor;
    private PulseOffice.UI.Controls.Text.Editing.InlineTextEditor? _editorHost;
    private IDisposable? _typing;
    private readonly Avalonia.Threading.DispatcherTimer _caretPulse = new() { Interval = TimeSpan.FromMilliseconds(120) };

    public TextBox? Editor => _editor;

    public void Attach() =>
        _caretPulse.Tick += (_, _) =>
        {
            _caretPulse.Stop();
            _host.NotifyRibbon();
        };

    public bool InEditor(Control source) =>
        _editorHost is { } host && (ReferenceEquals(source, host) || _ancestry.IsInside(source, host));

    public (int Start, int End)? EditorRange()
    {
        if (_editor is not { } editor) return null;
        var start = Math.Min(editor.SelectionStart, editor.SelectionEnd);
        var end = Math.Max(editor.SelectionStart, editor.SelectionEnd);
        return end > start ? (start, end) : null;
    }

    public TextSelection? CommitSelection()
    {
        if (_editor is not { } editor || _state.Editing is not { } element) return null;
        var start = Math.Min(editor.SelectionStart, editor.SelectionEnd);
        var end = Math.Max(editor.SelectionStart, editor.SelectionEnd);
        var map = CommitEditor();
        var mapped = new TextSelection(element.Id, map.Map(start), map.Map(end));
        return mapped.End > mapped.Start ? mapped : null;
    }

    public bool InsertParagraphsAtCaret(IReadOnlyList<TextParagraph> paragraphs)
    {
        if (_host.RichInsertion is not { } insertion || _editor is not { } editor || _state.Editing is not { } element || _state.Current is null) return false;

        var start = Math.Min(editor.SelectionStart, editor.SelectionEnd);
        var end = Math.Max(editor.SelectionStart, editor.SelectionEnd);
        var map = CommitEditor();
        if (_state.Current.Find(element.Id) is not TextElement live) return false;

        var range = (map.Map(start), map.Map(end));
        _actions.EditText(_state.Current, element.Id, insertion.Insert(live.Text, range, paragraphs));

        _state.Selected = _state.Current.Find(element.Id);
        _state.SelectedText = _state.Selected as TextElement;
        _canvas.SelectedElement = _state.Selected;
        var caret = range.Item1 + string.Join("\n", paragraphs.Select(paragraph => paragraph.Text)).Length;
        if (_state.SelectedText is { } edited) Resume(edited, (caret, caret));
        return true;
    }

    public bool TypeAtCaret(string text)
    {
        if (_editor is not { } editor) return false;

        var current = editor.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(editor.SelectionStart, editor.SelectionEnd), 0, current.Length);
        var end = Math.Clamp(Math.Max(editor.SelectionStart, editor.SelectionEnd), 0, current.Length);

        editor.Text = current[..start] + text + current[end..];
        editor.SelectionStart = editor.SelectionEnd = editor.CaretIndex = start + text.Length;
        editor.Focus();
        return true;
    }

    public void BeginEdit(TextElement element)
    {
        if (_actions.IsReadOnly) return;
        _shapes.Editor.Commit();

        CommitEditor();
        if (_state.Current is not { } slide) return;

        var bounds = _canvas.BoundsOf(element);
        var paragraph = element.Text.Paragraphs.Count > 0
            ? element.Text.Paragraphs[0]
            : new TextParagraph(string.Empty);

        var singleLine = element.Text.Role is PlaceholderRole.Title or PlaceholderRole.Subtitle;

        _state.Editing = element;
        _editor = new TextBox
        {
            Text = element.Text.PlainText,
            AcceptsReturn = !singleLine,
            AcceptsTab = true,
            FontSize = Math.Max(8, paragraph.FontSize * 20 * _canvas.Scale),
            FontWeight = paragraph.Bold ? FontWeight.SemiBold : FontWeight.Normal,
            Width = bounds.Width,
            Height = bounds.Height,
            TextWrapping = element.Text.Frame.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextAlignment = paragraph.Align == TextAlign.Center ? TextAlignment.Center
                : paragraph.Align == TextAlign.Right ? TextAlignment.Right : TextAlignment.Left,
            ContextFlyout = null,
            Padding = EditorPadding(element.Text.Frame, paragraph),
            VerticalContentAlignment = element.Text.Frame.Anchor switch
            {
                TextAnchor.Middle or TextAnchor.Justified => VerticalAlignment.Center,
                TextAnchor.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top,
            },
        };

        _editorHost = new PulseOffice.UI.Controls.Text.Editing.InlineTextEditor(_editor, _caretGeometry, _plainPositions, _editingServices.Navigation, _editingServices.Preedit, _caretWord)
        {
            CaretBrush = new SolidColorBrush(Color.FromUInt32(_deck.ThemeOf(slide).Colors.Resolve(paragraph.ColorRole ?? ColorRole.Text))),
            SelectionBrush = _host.Accent() is ISolidColorBrush accent
                ? new SolidColorBrush(accent.Color, 0.3)
                : new SolidColorBrush(Color.FromArgb(80, 37, 99, 235)),
        };
        if (paragraph.Color is { } ink) _editorHost.CaretBrush = new SolidColorBrush(Color.FromUInt32(ink));

        _state.Overlay.Margin = new Thickness(_canvas.Target.X, _canvas.Target.Y, 0, 0);
        _state.Overlay.Children.Add(_editorHost);
        RefreshLiveText();

        _editor.TextChanged += (_, _) => RefreshLiveText();
        _editor.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.CaretIndexProperty
                || change.Property == TextBox.SelectionStartProperty
                || change.Property == TextBox.SelectionEndProperty)
                { _caretPulse.Stop(); _caretPulse.Start(); _rulers.Update(); }
        };
        _editorHost.PreeditChanged += (_, _) => RefreshLiveText();
        _typing = _editingServices.Typing?.Attach(_editor, _correction.TextLanguage);
        _editor.AddHandler(InputElement.KeyDownEvent, OnEditorKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var box = _editor;
        box.LostFocus += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!_state.EditorMenuOpen && ReferenceEquals(_editor, box) && !box.IsFocused && !_editingFocus.KeepsEditing(_host.View)) CommitEditor();
        }, Avalonia.Threading.DispatcherPriority.Input);
        _editor.Focus();
        _host.AttachContext();
        _editor.CaretIndex = _editor.Text?.Length ?? 0;
    }

    public TextElement? LiveEditing()
    {
        if (_state.Editing is not { } editing || _editor is not { } editor || _state.Current is null) return null;
        var live = _state.Current.Find(editing.Id) as TextElement ?? editing;
        return _actions.FitPreview(_state.Current, live with { Text = _splice.Apply(live.Text, _editorHost?.DisplayText ?? editor.Text ?? string.Empty) });
    }

    private void RefreshLiveText()
    {
        if (_editorHost is not { } host || LiveEditing() is not { } live) return;

        var bounds = _canvas.BoundsOf(live);
        _canvas.LiveText = live;
        if (_filmstrip.CurrentThumbnail is { } thumbnail) thumbnail.LiveText = live;
        Canvas.SetLeft(host, bounds.X - _canvas.Target.X);
        Canvas.SetTop(host, bounds.Y - _canvas.Target.Y);
        host.Update(_canvas.Compose(_composer, live), bounds);
        _rulers.Update();
    }

    private Thickness EditorPadding(TextFrame frame, TextParagraph paragraph)
    {
        var k = PulseOffice.UI.Controls.Text.Rich.ParagraphTypesetter.PointToTwip * _canvas.Scale;
        var insets = frame.Insets;
        var margin = paragraph.MarginLeft ?? paragraph.BulletLevel * 18;
        return new Thickness((insets.Left + margin) * k, insets.Top * k, insets.Right * k, insets.Bottom * k);
    }

    private void OnEditorKey(object? sender, KeyEventArgs e)
    {
        if (_editorHost?.IsComposing == true) return;
        if (e.Key == Key.Escape)
        {
            CommitEditor();
            _canvas.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter) return;

        var multiline = _editor?.AcceptsReturn == true;
        var commit = !multiline
            || e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            || e.KeyModifiers.HasFlag(KeyModifiers.Control)
            || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (!commit) return;

        CommitEditor();
        e.Handled = true;
    }

    public void ApplyTextFormat(Func<RichText, (int Start, int End)?, RichText> transform)
    {
        if (_state.Current is null) return;
        if (_state.Editing is null && _shapes.ApplyShapeTextFormat(transform)) return;

        (int Start, int End)? range = null;
        (int Start, int End)? reopen = null;
        var target = _state.Editing ?? _state.SelectedText;

        if (_state.Editing is not null && _editor is not null)
        {
            var start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
            var end = Math.Max(_editor.SelectionStart, _editor.SelectionEnd);
            var (wordStart, wordEnd) = end == start ? _caretWord.Around(_editor.Text ?? string.Empty, start) : (start, end);
            var map = CommitEditor();
            reopen = (map.Map(start), map.Map(end));
            range = (map.Map(wordStart), map.Map(wordEnd));
        }

        if (target is null) return;
        if (_state.Current.Find(target.Id) is not TextElement live) return;

        var updated = transform(live.Text, range);
        _actions.EditText(_state.Current, target.Id, updated);

        _state.Selected = _state.Current.Find(target.Id);
        _state.SelectedText = _state.Selected as TextElement;
        _canvas.SelectedElement = _state.Selected;

        if (reopen is { } caret && _state.SelectedText is { } edited) Resume(edited, caret);
    }

    private void Resume(TextElement element, (int Start, int End) selection)
    {
        BeginEdit(element);
        if (_editor is not { } editor) return;

        var length = editor.Text?.Length ?? 0;
        editor.SelectionStart = Math.Clamp(selection.Start, 0, length);
        editor.SelectionEnd = Math.Clamp(selection.End, 0, length);
    }

    public TextOffsetMap CommitEditor()
    {
        if (_editor is null) return TextOffsetMap.Identity;

        var editor = _editor;
        var element = _state.Editing;
        _typing?.Dispose();
        _typing = null;
        _editor = null;
        _state.Editing = null;

        if (_editorHost is { } host) _state.Overlay.Children.Remove(host);
        _editorHost = null;
        _canvas.LiveText = null;
        if (_filmstrip.CurrentThumbnail is { } thumbnail) thumbnail.LiveText = null;
        _host.AttachContext();

        if (element is null || _state.Current is null) return TextOffsetMap.Identity;
        if (_state.Current.Find(element.Id) is not TextElement live) return TextOffsetMap.Identity;

        var raw = editor.Text ?? string.Empty;
        var text = _editingServices.Typing is { } typing ? typing.Finish(raw, _correction.TextLanguage()) : _correction.Corrected(raw);
        var map = new TextOffsetMap(raw, text);
        if (text.Replace("\r\n", "\n").Replace('\r', '\n') == live.Text.PlainText.Replace("\r\n", "\n")) return map;

        _actions.EditText(_state.Current, element.Id, _splice.Apply(live.Text, text));
        if (_canvas.SelectedElement?.Id == element.Id && _state.Current.Find(element.Id) is TextElement updated)
        {
            _canvas.SelectedElement = updated;
            _state.Selected = updated;
            _state.SelectedText = updated;
        }
        return map;
    }
}
