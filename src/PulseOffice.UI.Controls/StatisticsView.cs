using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed record StatisticsRow(string Label, string Whole, string? Selected = null);

public sealed class StatisticsView : UserControl, IDialogContent, IDialogFooterOwner
{
    public StatisticsView(IReadOnlyList<StatisticsRow> rows, string wholeHeader, string? selectedHeader, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);
        var withSelection = selectedHeader is not null;
        var grid = new Grid
        {
            ColumnDefinitions = withSelection ? new ColumnDefinitions("*,Auto,Auto") : new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions(string.Join(',', Enumerable.Repeat("Auto", rows.Count + 1))),
        };
        Header(grid, wholeHeader, withSelection ? 2 : 1, metrics);
        if (selectedHeader is not null) Header(grid, selectedHeader, 1, metrics);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var line = index + 1;
            var band = new Border { CornerRadius = new CornerRadius(metrics.RadiusTight) };
            if (index % 2 == 0) band.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
            Grid.SetRow(band, line);
            Grid.SetColumnSpan(band, withSelection ? 3 : 2);
            grid.Children.Add(band);
            Cell(grid, row.Label, line, 0, HorizontalAlignment.Left, Palette.Keys.TextSecondary, metrics);
            Cell(grid, row.Whole, line, withSelection ? 2 : 1, HorizontalAlignment.Right, Palette.Keys.TextPrimary, metrics);
            if (withSelection) Cell(grid, row.Selected ?? string.Empty, line, 1, HorizontalAlignment.Right, Palette.Keys.TextPrimary, metrics);
        }
        Footer = new DialogFooter(density).WithAction(texts["prompt.close"], () => Completed?.Invoke(this, null), primary: true);
        Content = new Border { Padding = new Thickness(0, metrics.SpacingSmall), Child = grid };
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private void Header(Grid grid, string text, int column, DesignTokens.Metrics metrics)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = metrics.FontSize - 2,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.6,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(metrics.SpacingLarge, 0, metrics.SpacingSmall, metrics.SpacingSmall),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private void Cell(Grid grid, string text, int row, int column, HorizontalAlignment alignment, string brush, DesignTokens.Metrics metrics)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = metrics.FontSize,
            HorizontalAlignment = alignment,
            FontFeatures = alignment == HorizontalAlignment.Right ? FontFeatureCollection.Parse("tnum") : null,
            Margin = new Thickness(metrics.SpacingSmall + (column == 0 ? 2 : metrics.SpacingLarge), metrics.SpacingSmall, metrics.SpacingSmall, metrics.SpacingSmall),
        }.Themed(TextBlock.ForegroundProperty, brush);
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }
}
