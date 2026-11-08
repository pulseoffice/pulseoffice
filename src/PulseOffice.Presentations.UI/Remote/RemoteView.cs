using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations.Remote;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Remote;

public sealed class QrCodeView(bool[,] matrix) : Control
{
    public override void Render(DrawingContext context)
    {
        var size = matrix.GetLength(0);
        var quiet = 2;
        var cell = Math.Floor(Math.Min(Bounds.Width, Bounds.Height) / (size + quiet * 2));
        if (cell <= 0) return;
        var origin = new Point((Bounds.Width - cell * (size + quiet * 2)) / 2, (Bounds.Height - cell * (size + quiet * 2)) / 2);
        context.FillRectangle(Brushes.White, new Rect(origin, new Size(cell * (size + quiet * 2), cell * (size + quiet * 2))));
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                if (matrix[y, x]) context.FillRectangle(Brushes.Black, new Rect(origin.X + (x + quiet) * cell, origin.Y + (y + quiet) * cell, cell, cell));
    }
}

public sealed class RemoteView : UserControl, IDialogContent, IDialogFooterOwner
{
    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public RemoteView(RemoteEndpoint endpoint, bool[,] qr, bool presenting, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);

        Footer = new DialogFooter(density)
            .WithCancel(texts["remote.turnOff"], () => Completed?.Invoke(this, false))
            .WithAction(texts["remote.keep"], () => Completed?.Invoke(this, true), primary: true);

        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children =
            {
                new QrCodeView(qr) { Width = 200, Height = 200 },
                new StackPanel
                {
                    Margin = new Thickness(metrics.SpacingLarge, 0, 0, 0),
                    Spacing = metrics.SpacingMedium,
                    [Grid.ColumnProperty] = 1,
                    Children =
                    {
                        new TextBlock { Text = texts["remote.explanation"], TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = texts["remote.address"] }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                        new SelectableTextBlock { Text = endpoint.Url, FontFamily = new FontFamily("Menlo, Consolas, monospace") },
                        new TextBlock { Text = texts["remote.pin"] }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                        new TextBlock { Text = endpoint.Pin, FontSize = 30, FontWeight = FontWeight.SemiBold, LetterSpacing = 6 },
                        new TextBlock { Text = texts[presenting ? "remote.presenting" : "remote.waiting"], TextWrapping = TextWrapping.Wrap }
                            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                    },
                },
            },
        };
    }
}
