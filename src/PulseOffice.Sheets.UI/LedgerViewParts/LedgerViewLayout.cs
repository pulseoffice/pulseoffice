using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerViewLayout(DesignTokens.Metrics metrics)
{
    public Control Build(TextBox nameBox, TextBox formulaBox, SheetTabStrip sheetTabs, SheetGridView grid, Canvas overlay)
    {
        var formulaBar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            Height = metrics.ControlHeight + metrics.SpacingMedium,
        };

        var marker = new TextBlock
        {
            Text = "fx",
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(metrics.SpacingMedium, 0),
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Grid.SetColumn(nameBox, 0);
        Grid.SetColumn(marker, 1);
        Grid.SetColumn(formulaBox, 2);

        formulaBar.Children.Add(nameBox);
        formulaBar.Children.Add(marker);
        formulaBar.Children.Add(formulaBox);

        var formulaBand = new Border
        {
            Child = formulaBar,
            Padding = new Thickness(metrics.SpacingSmall, metrics.SpacingSmall),
            BorderThickness = new Thickness(0, 0, 0, 1),
        }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var layout = new DockPanel { LastChildFill = true };

        DockPanel.SetDock(formulaBand, Dock.Top);
        layout.Children.Add(formulaBand);

        var sheetBand = new Border
        {
            Child = new Panel { Children = { sheetTabs } },
            BorderThickness = new Thickness(0, 1, 0, 0),
        }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        DockPanel.SetDock(sheetBand, Dock.Bottom);
        layout.Children.Add(sheetBand);

        var surface = new Panel();
        surface.Children.Add(grid);
        surface.Children.Add(overlay);

        layout.Children.Add(surface);
        return layout;
    }
}
