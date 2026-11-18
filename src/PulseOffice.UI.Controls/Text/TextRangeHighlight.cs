using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PulseOffice.UI.Controls.Text;

public sealed class TextRangeHighlight : Control
{
    private readonly TextBox _target;
    private (int Start, int Length)? _range;

    public TextRangeHighlight(TextBox target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        IsHitTestVisible = false;
        _target.TextChanged += (_, _) => InvalidateVisual();
        _target.LayoutUpdated += (_, _) => InvalidateVisual();
        _target.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty) InvalidateVisual();
        };
    }

    public IBrush? Fill { get; set; }

    public void Show(int start, int length)
    {
        _range = length > 0 && start >= 0 ? (start, length) : null;
        InvalidateVisual();
    }

    public void Hide()
    {
        _range = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_range is not { } range || !_target.IsVisible) return;
        var text = _target.Text ?? string.Empty;
        if (range.Start >= text.Length) return;
        var length = Math.Min(range.Length, text.Length - range.Start);
        if (_target.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is not { } presenter) return;

        foreach (var rect in presenter.TextLayout.HitTestTextRange(range.Start, length))
        {
            if (presenter.TranslatePoint(rect.TopLeft, this) is not { } origin) continue;
            context.FillRectangle(HighlightBrush(), new Rect(origin, rect.Size));
        }
    }

    private IBrush HighlightBrush()
    {
        if (Fill is { } fill) return fill;
        var warning = this.TryFindResource(PulseOffice.UI.Theming.Palette.Keys.Warning, ActualThemeVariant, out var value) && value is ISolidColorBrush solid
            ? solid.Color
            : default;
        return new SolidColorBrush(warning, 0.33);
    }
}
