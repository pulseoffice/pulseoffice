using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Forms;

namespace PulseOffice.UI.Controls.Forms;

public sealed record FormControlPalette(
    IBrush Surface,
    IBrush Field,
    IBrush Border,
    IBrush Text,
    IBrush Muted,
    IBrush Accent,
    IBrush Disabled);

public interface IFormControlRenderer
{
    void Render(
        DrawingContext context,
        FormControlKind kind,
        FormControlProperties control,
        IReadOnlyList<string> items,
        Rect bounds,
        FormControlPalette palette,
        System.Globalization.CultureInfo culture);
}

public sealed class FormControlRenderer : IFormControlRenderer
{
    private readonly ControlPainter _fallback = new FieldPainter();
    private readonly IReadOnlyList<ControlPainter> _painters =
    [
        new LabelPainter(), new CheckBoxPainter(), new OptionPainter(), new PushButtonPainter(), new ImageButtonPainter(),
        new GroupPainter(), new ListPainter(), new ComboPainter(), new SpinPainter(), new ScrollPainter(),
        new ImageFramePainter(), new TablePainter(), new NavigationPainter(), new FileSelectionPainter(),
    ];

    public void Render(
        DrawingContext context,
        FormControlKind kind,
        FormControlProperties control,
        IReadOnlyList<string> items,
        Rect bounds,
        FormControlPalette palette,
        System.Globalization.CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(culture);

        if (!control.Visible) return;

        var canvas = new ControlCanvas(context, control, items, bounds, palette, control.Enabled ? palette.Text : palette.Disabled, culture);
        (_painters.FirstOrDefault(painter => painter.Kind == kind) ?? _fallback).Paint(canvas);
    }
}

internal sealed class ControlCanvas(
    DrawingContext context, FormControlProperties control, IReadOnlyList<string> items, Rect bounds, FormControlPalette palette, IBrush ink, System.Globalization.CultureInfo culture)
{
    public const double Radius = 4;

    public DrawingContext Context => context;
    public FormControlProperties Control => control;
    public IReadOnlyList<string> Items => items;
    public Rect Bounds => bounds;
    public FormControlPalette Palette => palette;
    public IBrush Ink => ink;

    public string Caption => control.Label ?? control.Value.Text ?? control.Name;

    public string Display
    {
        get
        {
            if (control.Value.SelectedIndices.Count > 0)
            {
                var index = control.Value.SelectedIndices[0];
                if (index >= 0 && index < items.Count) return items[index];
            }
            return control.Value.Text ?? control.Value.Number?.ToString("G15", culture) ?? string.Empty;
        }
    }

    public void Field(string text, Rect area)
    {
        context.DrawRectangle(palette.Field, new Pen(palette.Border), area, Radius, Radius);
        Text(text, area.Deflate(new Thickness(6, 2)), TextAlignment.Left);
    }

    public void Button(string text, Rect area)
    {
        context.DrawRectangle(palette.Surface, new Pen(palette.Border), area, Radius, Radius);
        Text(text, area, TextAlignment.Center);
    }

    public void Text(string text, Rect area, TextAlignment alignment)
    {
        if (string.IsNullOrEmpty(text) || area.Width <= 2) return;

        var formatted = new FormattedText(text, culture, FlowDirection.LeftToRight, Typeface.Default, 12, ink)
        {
            MaxTextWidth = area.Width,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = alignment,
        };
        context.DrawText(formatted, new Point(area.X, area.Y + ((area.Height - formatted.Height) / 2)));
    }
}

internal abstract class ControlPainter
{
    public abstract FormControlKind Kind { get; }

    public abstract void Paint(ControlCanvas canvas);
}

internal sealed class FieldPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.TextBox;

    public override void Paint(ControlCanvas canvas) => canvas.Field(canvas.Display, canvas.Bounds);
}

internal sealed class LabelPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.Label;

    public override void Paint(ControlCanvas canvas) => canvas.Text(canvas.Caption, canvas.Bounds.Deflate(2), TextAlignment.Left);
}

internal abstract class ChoicePainter : ControlPainter
{
    public override void Paint(ControlCanvas canvas)
    {
        var size = Math.Min(14, canvas.Bounds.Height - 2);
        Mark(canvas, size);
        canvas.Text(canvas.Caption, canvas.Bounds.Deflate(new Thickness(size + 8, 0, 2, 0)), TextAlignment.Left);
    }

    protected abstract void Mark(ControlCanvas canvas, double size);
}

internal sealed class CheckBoxPainter : ChoicePainter
{
    public override FormControlKind Kind => FormControlKind.CheckBox;

    protected override void Mark(ControlCanvas canvas, double size)
    {
        var bounds = canvas.Bounds;
        var palette = canvas.Palette;
        var box = new Rect(bounds.X + 2, bounds.Y + ((bounds.Height - size) / 2), size, size);
        canvas.Context.DrawRectangle(palette.Field, new Pen(palette.Border), box, 3, 3);

        if (canvas.Control.Value.Checked == CheckState.Checked)
        {
            var pen = new Pen(palette.Accent, 2);
            canvas.Context.DrawLine(pen, new Point(box.X + (size * 0.22), box.Y + (size * 0.52)), new Point(box.X + (size * 0.42), box.Y + (size * 0.74)));
            canvas.Context.DrawLine(pen, new Point(box.X + (size * 0.42), box.Y + (size * 0.74)), new Point(box.X + (size * 0.78), box.Y + (size * 0.28)));
        }
        else if (canvas.Control.Value.Checked == CheckState.Indeterminate)
        {
            canvas.Context.FillRectangle(palette.Accent, box.Deflate(size * 0.3));
        }
    }
}

internal sealed class OptionPainter : ChoicePainter
{
    public override FormControlKind Kind => FormControlKind.OptionButton;

    protected override void Mark(ControlCanvas canvas, double size)
    {
        var centre = new Point(canvas.Bounds.X + 2 + (size / 2), canvas.Bounds.Y + (canvas.Bounds.Height / 2));
        canvas.Context.DrawEllipse(canvas.Palette.Field, new Pen(canvas.Palette.Border), centre, size / 2, size / 2);
        if (canvas.Control.Value.Checked == CheckState.Checked)
            canvas.Context.DrawEllipse(canvas.Palette.Accent, null, centre, size / 4, size / 4);
    }
}

internal sealed class PushButtonPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.PushButton;

    public override void Paint(ControlCanvas canvas) => canvas.Button(canvas.Caption, canvas.Bounds);
}

internal sealed class ImageButtonPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.ImageButton;

    public override void Paint(ControlCanvas canvas) => canvas.Button(canvas.Control.ImagePath is null ? "…" : string.Empty, canvas.Bounds);
}

internal sealed class GroupPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.GroupBox;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        canvas.Context.DrawRectangle(null, new Pen(canvas.Palette.Border), bounds.Deflate(new Thickness(0, 8, 0, 0)), ControlCanvas.Radius, ControlCanvas.Radius);

        var label = new Rect(bounds.X + 10, bounds.Y, Math.Max(0, bounds.Width - 20), 16);
        canvas.Context.FillRectangle(canvas.Palette.Surface, label.Inflate(new Thickness(4, 0)));
        canvas.Text(canvas.Caption, label, TextAlignment.Left);
    }
}

internal sealed class ListPainter : ControlPainter
{
    private readonly ListBoxLayout _layout = new();

    public override FormControlKind Kind => FormControlKind.ListBox;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        canvas.Context.DrawRectangle(canvas.Palette.Field, new Pen(canvas.Palette.Border), bounds, ControlCanvas.Radius, ControlCanvas.Radius);

        var visible = _layout.VisibleRows(bounds.Height);
        for (var i = 0; i < Math.Min(visible, canvas.Items.Count); i++)
        {
            var row = new Rect(
                bounds.X + _layout.Inset, bounds.Y + _layout.RowTop(i), bounds.Width - (2 * _layout.Inset), _layout.RowHeight);
            if (canvas.Control.Value.SelectedIndices.Contains(i)) canvas.Context.FillRectangle(canvas.Palette.Accent, row, 3);
            canvas.Text(canvas.Items[i], row.Deflate(new Thickness(4, 0)), TextAlignment.Left);
        }
    }
}

internal sealed class ComboPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.ComboBox;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        canvas.Field(canvas.Display, bounds);

        var centre = new Point(bounds.Right - 14, bounds.Y + (bounds.Height / 2));
        var pen = new Pen(canvas.Palette.Muted, 1.4);
        var tip = new Point(centre.X, centre.Y + 2);
        canvas.Context.DrawLine(pen, new Point(centre.X - 4, centre.Y - 2), tip);
        canvas.Context.DrawLine(pen, tip, new Point(centre.X + 4, centre.Y - 2));
    }
}

internal sealed class SpinPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.SpinButton;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        var horizontal = bounds.Width >= bounds.Height;

        var (increase, decrease, more, less) = horizontal
            ? (new Rect(bounds.X + (bounds.Width / 2), bounds.Y, bounds.Width / 2, bounds.Height),
               new Rect(bounds.X, bounds.Y, bounds.Width / 2, bounds.Height), "▶", "◀")
            : (new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2),
               new Rect(bounds.X, bounds.Y + (bounds.Height / 2), bounds.Width, bounds.Height / 2), "▲", "▼");

        canvas.Context.DrawRectangle(canvas.Palette.Surface, new Pen(canvas.Palette.Border), increase);
        canvas.Context.DrawRectangle(canvas.Palette.Surface, new Pen(canvas.Palette.Border), decrease);
        canvas.Text(more, increase, TextAlignment.Center);
        canvas.Text(less, decrease, TextAlignment.Center);
    }
}

internal sealed class ScrollPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.ScrollBar;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        var control = canvas.Control;
        canvas.Context.DrawRectangle(canvas.Palette.Field, new Pen(canvas.Palette.Border), bounds, ControlCanvas.Radius, ControlCanvas.Radius);

        var range = control.Range ?? new NumericRange(0, 100, 1, 10);
        var span = range.Maximum - range.Minimum;
        var ratio = span <= 0 ? 0 : Math.Clamp(((control.Value.Number ?? range.Minimum) - range.Minimum) / span, 0, 1);

        var thumb = bounds.Width >= bounds.Height
            ? new Rect(bounds.X + (ratio * Math.Max(0, bounds.Width - 24)), bounds.Y + 1, 24, bounds.Height - 2)
            : new Rect(bounds.X + 1, bounds.Y + (ratio * Math.Max(0, bounds.Height - 24)), bounds.Width - 2, 24);
        canvas.Context.FillRectangle(canvas.Palette.Accent, thumb, 3);
    }
}

internal sealed class ImageFramePainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.ImageControl;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        var pen = new Pen(canvas.Palette.Border);
        canvas.Context.DrawRectangle(canvas.Palette.Field, pen, bounds, ControlCanvas.Radius, ControlCanvas.Radius);
        canvas.Context.DrawLine(pen, bounds.TopLeft, bounds.BottomRight);
        canvas.Context.DrawLine(pen, bounds.TopRight, bounds.BottomLeft);
    }
}

internal sealed class TablePainter : ControlPainter
{
    private const double RowHeight = 20;

    public override FormControlKind Kind => FormControlKind.TableControl;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        canvas.Context.DrawRectangle(canvas.Palette.Field, new Pen(canvas.Palette.Border), bounds, ControlCanvas.Radius, ControlCanvas.Radius);

        var header = new Rect(bounds.X, bounds.Y, bounds.Width, RowHeight);
        canvas.Context.FillRectangle(canvas.Palette.Surface, header);
        canvas.Context.DrawLine(new Pen(canvas.Palette.Border), header.BottomLeft, header.BottomRight);

        for (var i = 0; i < canvas.Items.Count && bounds.Y + ((i + 1) * RowHeight) < bounds.Bottom; i++)
            canvas.Text(canvas.Items[i], new Rect(bounds.X + 4, bounds.Y + ((i + 1) * RowHeight), bounds.Width - 8, RowHeight), TextAlignment.Left);
    }
}

internal sealed class NavigationPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.NavigationBar;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        canvas.Context.DrawRectangle(canvas.Palette.Surface, new Pen(canvas.Palette.Border), bounds, ControlCanvas.Radius, ControlCanvas.Radius);

        var step = bounds.Width / 6;
        for (var i = 1; i < 6; i++)
            canvas.Context.DrawLine(new Pen(canvas.Palette.Border), new Point(bounds.X + (i * step), bounds.Y), new Point(bounds.X + (i * step), bounds.Bottom));
        canvas.Text("|◀  ◀  ▶  ▶|", bounds, TextAlignment.Center);
    }
}

internal sealed class FileSelectionPainter : ControlPainter
{
    public override FormControlKind Kind => FormControlKind.FileSelection;

    public override void Paint(ControlCanvas canvas)
    {
        var bounds = canvas.Bounds;
        canvas.Field(canvas.Display, bounds.WithWidth(Math.Max(0, bounds.Width - 70)));
        canvas.Button("…", new Rect(bounds.Right - 66, bounds.Y, 66, bounds.Height));
    }
}
