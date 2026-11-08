using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.UI.Controls.Text.Rich;
using PulseOffice.Text.Input;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Editing;

public sealed class InlineTextEditor : Decorator, IInlineImeSurface
{
    private readonly ITextCaretGeometry _geometry;
    private readonly IPlainTextPositions _positions;
    private readonly ICaretRowNavigation _navigation;
    private readonly IPreeditComposer _composer;
    private readonly ICaretWord _words;
    private readonly InlineImeClient _ime;
    private readonly DispatcherTimer _blink;
    private bool _caretShown = true;
    private bool _navigating;
    private double? _preferredX;
    private int? _anchor;
    private string? _preedit;
    private int? _preeditCursor;

    public InlineTextEditor(
        TextBox box, ITextCaretGeometry geometry, IPlainTextPositions positions,
        ICaretRowNavigation navigation, IPreeditComposer composer, ICaretWord words)
    {
        ArgumentNullException.ThrowIfNull(box);
        Box = box;
        _geometry = geometry;
        _positions = positions;
        _navigation = navigation;
        _composer = composer;
        _words = words;
        _ime = new InlineImeClient(this);

        box.Opacity = 0;
        box.IsHitTestVisible = false;
        Child = box;
        Cursor = new Cursor(StandardCursorType.Ibeam);

        _blink = new DispatcherTimer(TimeSpan.FromMilliseconds(530), DispatcherPriority.Render, (_, _) =>
        {
            _caretShown = !_caretShown;
            InvalidateVisual();
        });

        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty
                || e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty)
            {
                if (!_navigating) _preferredX = null;
                Wake();
            }
        };
        Focusable = false;
        box.GotFocus += (_, _) => Wake();
        box.LostFocus += (_, _) =>
        {
            SetPreedit(null, null);
            InvalidateVisual();
        };
        box.AddHandler(KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
        box.AddHandler(TextInputMethodClientRequestedEvent, OnClientRequested, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public Visual TextViewVisual => this;

    public event EventHandler? PreeditChanged;

    public PreeditComposition Composition =>
        _composer.Compose(Box.Text ?? string.Empty, Box.SelectionStart, Box.SelectionEnd, _preedit, _preeditCursor);

    public string DisplayText => Composition.Text;

    public bool IsComposing => _preedit is not null;

    public Rect CaretRectangle
    {
        get
        {
            if (Frame is not { } frame) return new Rect(0, 0, 1, Math.Max(Bounds.Height, 1));
            var composition = Composition;
            var caret = _geometry.Caret(frame, _positions.Locate(composition.Text, composition.Caret));
            return new Rect(caret.X - Origin.X, caret.Y - Origin.Y, Math.Max(caret.Width, 1), Math.Max(caret.Height, 4));
        }
    }

    public void SetPreedit(string? text, int? cursor)
    {
        var next = string.IsNullOrEmpty(text) ? null : text;
        if (next == _preedit && cursor == _preeditCursor) return;
        _preedit = next;
        _preeditCursor = cursor;
        PreeditChanged?.Invoke(this, EventArgs.Empty);
        Wake();
    }

    public TextBox Box { get; }

    public ComposedTextFrame? Frame { get; private set; }

    public Point Origin { get; private set; }

    public IBrush CaretBrush { get; set; } = Brushes.Black;

    public IBrush SelectionBrush { get; set; } = new SolidColorBrush(Color.FromArgb(80, 37, 99, 235));

    public void Update(ComposedTextFrame frame, Rect bounds)
    {
        Frame = frame;
        Origin = bounds.TopLeft;
        Width = bounds.Width;
        Height = bounds.Height;
        Box.Width = bounds.Width;
        Box.Height = bounds.Height;
        _ime.CaretMoved();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Frame is not { } frame) return;

        using var shift = context.PushTransform(Matrix.CreateTranslation(-Origin.X, -Origin.Y));
        var composition = Composition;
        if (composition.IsActive)
        {
            PaintPreedit(context, frame, composition);
            return;
        }

        var text = Box.Text ?? string.Empty;
        var start = Math.Min(Box.SelectionStart, Box.SelectionEnd);
        var end = Math.Max(Box.SelectionStart, Box.SelectionEnd);
        if (end > start)
        {
            foreach (var area in _geometry.Selection(frame, _positions.Locate(text, start), _positions.Locate(text, end)))
                context.DrawGeometry(SelectionBrush, null, area);
            return;
        }

        if (!Box.IsFocused || !_caretShown) return;
        var caret = _geometry.Caret(frame, _positions.Locate(text, Box.CaretIndex));
        context.FillRectangle(CaretBrush, new Rect(caret.X - 0.75, caret.Y, 1.5, Math.Max(caret.Height, 4)));
    }

    private void PaintPreedit(DrawingContext context, ComposedTextFrame frame, PreeditComposition composition)
    {
        var from = _positions.Locate(composition.Text, composition.Start);
        var to = _positions.Locate(composition.Text, composition.Start + composition.Length);
        var pen = new Pen(CaretBrush, 1.2);
        foreach (var area in _geometry.Selection(frame, from, to))
        {
            var bounds = area.Bounds;
            context.DrawLine(pen, new Point(bounds.Left, bounds.Bottom - 1), new Point(bounds.Right, bounds.Bottom - 1));
        }
        var caret = _geometry.Caret(frame, _positions.Locate(composition.Text, composition.Caret));
        context.FillRectangle(CaretBrush, new Rect(caret.X - 0.75, caret.Y, 1.5, Math.Max(caret.Height, 4)));
    }

    private void OnClientRequested(object? sender, TextInputMethodClientRequestedEventArgs e)
    {
        if (e.Client is { } client && !ReferenceEquals(client, _ime)) _ime.Wrap(client);
        if (e.Client is not null) e.Client = _ime;
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_preedit is not null || Frame is not { } frame) return;
        if (MoveFor(e) is not { } move) return;

        var text = Box.Text ?? string.Empty;
        var rows = _geometry.Rows(frame, position => _positions.Index(text, position));
        var caret = Box.SelectionEnd;
        var target = _navigation.Move(rows, caret, _preferredX, move, rows.Count);
        var anchor = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? Box.SelectionStart : target.Index;

        _navigating = true;
        try
        {
            Select(anchor, target.Index);
        }
        finally
        {
            _navigating = false;
        }
        _preferredX = target.PreferredX;
        e.Handled = true;
    }

    private CaretMove? MoveFor(KeyEventArgs e)
    {
        var modifiers = e.KeyModifiers & ~KeyModifiers.Shift;
        if (modifiers == KeyModifiers.Meta)
            return e.Key switch
            {
                Key.Left => CaretMove.RowStart,
                Key.Right => CaretMove.RowEnd,
                _ => null,
            };
        if (modifiers != KeyModifiers.None) return null;
        return e.Key switch
        {
            Key.Up => CaretMove.Up,
            Key.Down => CaretMove.Down,
            Key.Home => CaretMove.RowStart,
            Key.End => CaretMove.RowEnd,
            Key.PageUp => CaretMove.PageUp,
            Key.PageDown => CaretMove.PageDown,
            _ => null,
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _blink.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _blink.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Frame is null || IsComposing) return;

        var text = Box.Text ?? string.Empty;
        var index = IndexAt(e.GetPosition(this));
        e.Handled = true;
        if (!Box.IsFocused) Box.Focus();

        if (e.ClickCount >= 3)
        {
            var paragraph = _positions.Locate(text, index).Paragraph;
            var from = _positions.Index(text, new TextPosition(paragraph, 0));
            Select(from, from + _positions.ParagraphLength(text, paragraph));
            _anchor = null;
        }
        else if (e.ClickCount == 2)
        {
            var (from, to) = _words.Enclosing(text, index);
            Select(from, to);
            _anchor = null;
        }
        else
        {
            var anchor = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? Box.SelectionStart : index;
            Select(anchor, index);
            _anchor = anchor;
            e.Pointer.Capture(this);
        }
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_anchor is not { } anchor || !ReferenceEquals(e.Pointer.Captured, this)) return;
        Select(anchor, IndexAt(e.GetPosition(this)));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_anchor is null) return;
        _anchor = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _anchor = null;
    }

    private int IndexAt(Point local)
    {
        var text = Box.Text ?? string.Empty;
        if (Frame is not { } frame) return text.Length;
        var position = _geometry.Hit(frame, new Point(local.X + Origin.X, local.Y + Origin.Y));
        return _positions.Index(text, position);
    }

    private void Select(int anchor, int caret)
    {
        Box.CaretIndex = caret;
        Box.SelectionStart = anchor;
        Box.SelectionEnd = caret;
        Wake();
    }

    private void Wake()
    {
        _caretShown = true;
        _ime.CaretMoved();
        if (_blink.IsEnabled)
        {
            _blink.Stop();
            _blink.Start();
        }
        InvalidateVisual();
    }
}
