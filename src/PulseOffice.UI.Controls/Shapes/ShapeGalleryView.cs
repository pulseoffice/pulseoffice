using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shapes;

public sealed class ShapeGalleryView : UserControl, IDialogContent
{
    public event EventHandler<object?>? Completed;

    public ShapeGalleryView(
        ITextCatalog texts,
        IReadOnlyList<ShapeKind>? kinds = null,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var metrics = DesignTokens.For(density);
        var geometry = new ShapeGeometry();

        var panel = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var kind in kinds ?? Enum.GetValues<ShapeKind>())
        {
            var preview = new ShapePreview(geometry, kind)
            {
                Width = 40,
                Height = 32,
            };

            var button = new Button
            {
                Content = preview,
                Margin = new Thickness(metrics.SpacingSmall / 2),
                Padding = new Thickness(metrics.SpacingSmall),
                CornerRadius = new CornerRadius(metrics.RadiusTight),
                [ToolTip.TipProperty] = texts[$"shape.{Key(kind)}"],
            };

            Avalonia.Automation.AutomationProperties.SetName(button, texts[$"shape.{Key(kind)}"]);
            button.Click += (_, _) => Completed?.Invoke(this, kind);
            panel.Children.Add(button);
        }

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock
                {
                    Text = texts["shape.gallery.explanation"],
                    FontSize = metrics.FontSize,
                    TextWrapping = TextWrapping.Wrap,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                panel,
            },
        };
    }

    public static string Key(ShapeKind kind) =>
        char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private sealed class ShapePreview : Control
    {
        private readonly IShapeGeometry _geometry;
        private readonly ShapeKind _kind;

        public ShapePreview(IShapeGeometry geometry, ShapeKind kind)
        {
            _geometry = geometry;
            _kind = kind;
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size).Deflate(2);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            var outline = ShapeRenderer.Build(
                _geometry.Outline(_kind).Scaled(bounds.Width, bounds.Height));

            var brush = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;

            using (context.PushTransform(Matrix.CreateTranslation(bounds.X, bounds.Y)))
                context.DrawGeometry(brush, new Pen(brush, 1.2), outline);
        }
    }
}
