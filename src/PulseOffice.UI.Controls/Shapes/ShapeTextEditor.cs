using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record ShapeTextPlacement(Rect Bounds, double PixelsPerPoint, double RotationDegrees = 0);

public sealed record ShapeTextEditStart(string Id, ShapeObject Shape, ShapeTextPlacement Placement, ShapeSurroundings Backdrop)
{
    public string? Typed { get; init; }
}

public sealed record ShapeTextEdited(string Id, ShapeObject Original, ShapeObject Edited)
{
    public bool Changed => !Equals(Original, Edited);
}

public sealed record ShapeTextProbe(ShapeObject Shape, int Start, int End)
{
    public const string Character = "x";
}

public sealed record PendingShapeFormat(int Caret, string Before, Func<ShapeObject, int, int, ShapeObject> Format);

public sealed class ShapeTextEditor : Decorator
{
    private readonly IShapeTextEditing _editing;
    private readonly IShapeTextInk _ink;
    private readonly IShapeBrushes _brushes;
    private readonly TextBox _box;
    private ShapeObject? _original;
    private ShapeObject? _base;
    private string? _id;
    private bool _committing;
    private PendingShapeFormat? _pending;
    private readonly ICaretWord _words;

    private readonly PulseOffice.UI.Controls.Text.ITextEditingFocus _focus;

    public ShapeTextEditor(IShapeTextEditing editing, IShapeTextInk ink, IShapeBrushes brushes, PulseOffice.UI.Controls.Text.ITextEditingFocus focus, ICaretWord words)
    {
        _words = words;
        _focus = focus;
        _editing = editing;
        _ink = ink;
        _brushes = brushes;
        _box = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            Background = Brushes.Transparent,
            Foreground = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MinHeight = 0,
            MinWidth = 0,
            ContextFlyout = null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _box.Styles.Add(Transparent(":focus"));
        _box.Styles.Add(Transparent(":pointerover"));
        _box.Styles.Add(Transparent(string.Empty));
        Child = _box;
        IsVisible = false;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        RenderTransformOrigin = RelativePoint.Center;

        _box.TextChanged += (_, _) =>
        {
            if (!IsEditing) return;
            LiveChanged?.Invoke(this, EventArgs.Empty);
        };
        _box.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        _box.PropertyChanged += (_, change) =>
        {
            if (IsEditing && change.Property == TextBox.TextProperty)
            {
                ApplyPending();
                Follow();
            }
            if (!IsEditing || (change.Property != TextBox.SelectionStartProperty && change.Property != TextBox.SelectionEndProperty)) return;
            if (_pending is { } pending && _box.Text == pending.Before
                && Selection != (pending.Caret, pending.Caret)) Drop();
            SelectionMoved?.Invoke(this, EventArgs.Empty);
        };
        _box.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (IsEditing && !_box.IsFocused && !_focus.KeepsEditing(this)) Commit();
        }, DispatcherPriority.Input);
    }

    public ShapeTextEditor() : this(new ShapeTextEditing(), new ShapeTextInk(), new ShapeBrushes(), new PulseOffice.UI.Controls.Text.TextEditingFocus(), new CaretWord()) { }

    public event EventHandler? LiveChanged;

    public event EventHandler<ShapeTextEdited>? Committed;

    public event EventHandler? Exited;

    public event EventHandler? SelectionMoved;

    public IBrush SelectionBrush
    {
        get => _box.SelectionBrush ?? Brushes.Transparent;
        set => _box.SelectionBrush = value;
    }

    public bool IsEditing => _id is not null;

    public string? EditingId => _id;

    public TextBox Box => _box;

    public ShapeObject? Live => _base is { } shape ? _editing.WithText(shape, _box.Text ?? string.Empty) : null;

    private void Follow()
    {
        if (_base is { } shape) _base = _editing.WithText(shape, _box.Text ?? string.Empty);
    }

    public (int Start, int End) Selection =>
        (Math.Min(_box.SelectionStart, _box.SelectionEnd), Math.Max(_box.SelectionStart, _box.SelectionEnd));

    public bool HasPendingFormat => _pending is not null;

    public event EventHandler? PendingChanged;

    public ShapeTextProbe? PendingProbe()
    {
        if (!IsEditing || _pending is not { } pending || _base is not { } shape) return null;
        if ((_box.Text ?? string.Empty) != pending.Before) return null;
        var text = _editing.Normalized(pending.Before);
        if (pending.Caret > text.Length) return null;
        var probed = _editing.WithText(shape, text.Insert(pending.Caret, ShapeTextProbe.Character));
        return new ShapeTextProbe(pending.Format(probed, pending.Caret, pending.Caret + 1), pending.Caret, pending.Caret + 1);
    }

    public (int Start, int End)? FormatRange()
    {
        var (start, end) = Selection;
        if (end > start) return (start, end);
        var word = _words.Around(_box.Text ?? string.Empty, start);
        return word.End > word.Start ? word : null;
    }

    public void Pend(Func<ShapeObject, int, int, ShapeObject> format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (!IsEditing) return;
        var caret = Selection.Start;
        var before = _box.Text ?? string.Empty;
        _pending = _pending is { } previous && previous.Caret == caret && previous.Before == before
            ? previous with { Format = (shape, from, to) => format(previous.Format(shape, from, to), from, to) }
            : new PendingShapeFormat(caret, before, format);
        PendingChanged?.Invoke(this, EventArgs.Empty);
        _box.Focus();
    }

    private void ApplyPending()
    {
        if (_pending is not { } pending || _base is not { } shape) return;
        var before = _editing.Normalized(pending.Before);
        var after = _editing.Normalized(_box.Text ?? string.Empty);
        if (after == before) return;
        Drop();
        var grown = after.Length - before.Length;
        if (grown <= 0 || pending.Caret > before.Length
            || !after.StartsWith(before[..pending.Caret], StringComparison.Ordinal)
            || !after.EndsWith(before[pending.Caret..], StringComparison.Ordinal)) return;
        _base = pending.Format(_editing.WithText(shape, after), pending.Caret, pending.Caret + grown);
    }

    private void Drop()
    {
        if (_pending is null) return;
        _pending = null;
        PendingChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Begin(ShapeTextEditStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        Commit();
        _pending = null;
        _id = start.Id;
        _original = start.Shape;
        _base = start.Shape;
        var text = _editing.PlainText(start.Shape);
        Restyle(start.Shape, start.Backdrop);
        Place(start.Placement);
        IsVisible = true;
        _box.Text = start.Typed is { } typed ? text + typed : text;
        _box.Focus();
        _box.SelectionStart = _box.SelectionEnd = _box.CaretIndex = _box.Text?.Length ?? 0;
        LiveChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Place(ShapeTextPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        Canvas.SetLeft(this, placement.Bounds.X);
        Canvas.SetTop(this, placement.Bounds.Y);
        Width = Math.Max(placement.Bounds.Width, 1);
        Height = Math.Max(placement.Bounds.Height, 1);
        RenderTransform = Math.Abs(placement.RotationDegrees) < 0.001 ? null : new RotateTransform(placement.RotationDegrees);
        if (_base is { } shape) Typeset(shape, placement.PixelsPerPoint);
        _scale = placement.PixelsPerPoint;
    }

    public void Reformat(ShapeObject formatted, ShapeSurroundings backdrop)
    {
        ArgumentNullException.ThrowIfNull(formatted);
        if (!IsEditing) return;
        var (start, end) = Selection;
        _base = formatted;
        Restyle(formatted, backdrop);
        _box.Text = _editing.PlainText(formatted);
        _box.SelectionStart = start;
        _box.SelectionEnd = end;
        _box.Focus();
        LiveChanged?.Invoke(this, EventArgs.Empty);
    }

    public ShapeTextEdited? Commit()
    {
        if (_committing || _id is not { } id || _original is not { } original || Live is not { } edited) return null;
        _committing = true;
        try
        {
            _id = null;
            _pending = null;
            _original = null;
            _base = null;
            IsVisible = false;
            var result = new ShapeTextEdited(id, original, edited);
            Committed?.Invoke(this, result);
            return result;
        }
        finally
        {
            _committing = false;
        }
    }

    private double _scale = 1;

    private void Restyle(ShapeObject shape, ShapeSurroundings backdrop)
    {
        var ink = _brushes.Colour(_ink.Resolve(shape.Style, Behind(shape.Style.Fill, backdrop).Hex), Colors.Black);
        _box.CaretBrush = new SolidColorBrush(ink);
        Typeset(shape, _scale);
    }

    private ShapeSurroundings Behind(ShapeFill fill, ShapeSurroundings backdrop) =>
        fill is { Kind: ShapeFillKind.Bitmap, Bitmap: { } bitmap } && fill.IsVisible && _brushes.Average(bitmap) is { } average
            ? new ShapeSurroundings(average)
            : backdrop;

    private void Typeset(ShapeObject shape, double pixelsPerPoint)
    {
        var style = shape.Style.Text;
        var run = _editing.RunAt(shape, 0, 0);
        var inset = style.Inset.ToPoints() * pixelsPerPoint;
        _box.FontFamily = new FontFamily(run.Family is { Length: > 0 } family ? family : style.Family);
        _box.FontSize = Math.Max(1, (run.Size is { } size && size > 0 ? size : style.Size) * pixelsPerPoint);
        _box.FontWeight = run.Bold || style.Bold ? FontWeight.Bold : FontWeight.Normal;
        _box.FontStyle = run.Italic || style.Italic ? FontStyle.Italic : FontStyle.Normal;
        _box.Padding = new Thickness(inset);
        _box.TextWrapping = style.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _box.TextAlignment = style.Horizontal switch
        {
            TextHorizontalPlacement.Left => TextAlignment.Left,
            TextHorizontalPlacement.Right => TextAlignment.Right,
            TextHorizontalPlacement.Justify => TextAlignment.Justify,
            _ => TextAlignment.Center,
        };
        _box.VerticalContentAlignment = style.Vertical switch
        {
            TextVerticalPlacement.Top => VerticalAlignment.Top,
            TextVerticalPlacement.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        var commit = e.Key == Key.Escape
            || e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta));
        if (!commit) return;
        Commit();
        Exited?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private Style Transparent(string state)
    {
        var style = new Style(selector => (state.Length == 0 ? selector.OfType<TextBox>() : selector.OfType<TextBox>().Class(state))
            .Template().OfType<Border>().Name("PART_BorderElement"));
        style.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0)));
        return style;
    }
}
