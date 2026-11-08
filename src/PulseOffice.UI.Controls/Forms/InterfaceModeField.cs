using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Forms;

public enum InterfaceLayoutKind { Ribbon, Compact }

public sealed record InterfaceModeChoice(InterfaceLayoutKind Kind, string Caption);

public sealed class InterfaceModeSketch(InterfaceLayoutKind kind) : Control
{
    public InterfaceLayoutKind Kind { get; } = kind;

    public override void Render(DrawingContext context)
    {
        var frame = new Rect(Bounds.Size).Deflate(3);
        if (frame.Width <= 0 || frame.Height <= 0) return;

        var chrome = new SolidColorBrush(Color.FromRgb(0xE6, 0xE9, 0xEE));
        var bar = new SolidColorBrush(Color.FromRgb(0xC5, 0xCB, 0xD5));
        var accent = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
        context.DrawRectangle(Brushes.White, new Pen(bar, 1), frame, 4, 4);

        var title = new Rect(frame.X, frame.Y, frame.Width, frame.Height * 0.12);
        context.DrawRectangle(chrome, null, title, 4, 4);
        for (var index = 0; index < 3; index++)
            context.DrawEllipse(bar, null, new Point(title.X + 6 + index * 6, title.Center.Y), 2, 2);

        var top = title.Bottom;
        var menu = new Rect(frame.X, top, frame.Width, frame.Height * 0.09);
        context.DrawRectangle(chrome, null, menu);
        for (var index = 0; index < 5; index++)
            context.FillRectangle(bar,
                new Rect(menu.X + 6 + index * frame.Width * 0.15, menu.Center.Y - 1.5, frame.Width * 0.1, 3));
        top = menu.Bottom;

        var height = frame.Height * (Kind == InterfaceLayoutKind.Ribbon ? 0.22 : 0.1);
        var ribbon = new Rect(frame.X, top, frame.Width, height);
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xF5, 0xF8)), null, ribbon);
        var size = Kind == InterfaceLayoutKind.Ribbon ? height * 0.55 : height * 0.6;
        for (var index = 0; index < (Kind == InterfaceLayoutKind.Ribbon ? 6 : 10); index++)
            context.FillRectangle(index == 0 ? accent : bar,
                new Rect(ribbon.X + 6 + index * (size + 5), ribbon.Center.Y - size / 2, size, size));
        top = ribbon.Bottom;

        var page = new Rect(frame.Center.X - frame.Width * 0.22, top + 5, frame.Width * 0.44, frame.Bottom - top - 5);
        context.DrawRectangle(Brushes.White, new Pen(bar, 0.75), page);
        var lines = new Pen(bar, 1);
        for (var y = page.Y + 6; y < page.Bottom - 3; y += 5)
            context.DrawLine(lines, new Point(page.X + 5, y), new Point(page.Right - 5, y));
    }
}

public sealed class InterfaceModePainter : IThumbnailPainter<InterfaceModeChoice>
{
    public Control Create(InterfaceModeChoice item, Size size)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new InterfaceModeSketch(item.Kind) { Width = size.Width, Height = size.Height };
    }

    public void Repaint(Control thumbnail, InterfaceModeChoice item) => thumbnail.InvalidateVisual();
}
