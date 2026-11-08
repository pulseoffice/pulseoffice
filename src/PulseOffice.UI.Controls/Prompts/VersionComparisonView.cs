using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Versions;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Prompts;

public sealed record VersionComparisonAnswer(bool OpenAlongside);

public sealed class VersionComparisonView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const int ShownLimit = 500;
    private readonly StackPanel _rows = new() { Spacing = 2 };

    public VersionComparisonView(VersionComparison comparison, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);
        Comparison = comparison;

        var layout = new TwoPaneDialogLayout(metrics) { Header = new HelpTip(texts["versions.compare.explanation"], texts, metrics) };
        var section = layout.AddSection(texts["versions.compare.list"]);

        var summary = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        summary.Children.Add(Chip(comparison.Added, texts["versions.compare.added"], Palette.Keys.Success, metrics));
        summary.Children.Add(Chip(comparison.Removed, texts["versions.compare.removed"], Palette.Keys.Danger, metrics));
        summary.Children.Add(Chip(comparison.Changed, texts["versions.compare.changed"], Palette.Keys.Info, metrics));
        AutomationProperties.SetName(summary, texts.Format("versions.compare.summary", comparison.Added, comparison.Removed, comparison.Changed));
        section.Add(null, summary, DialogRowKind.Wide);

        if (comparison.IsEmpty)
        {
            _rows.Children.Add(new TextBlock { Text = texts["versions.compare.none"], Margin = new Thickness(metrics.SpacingMedium), TextWrapping = TextWrapping.Wrap }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        }
        else
        {
            foreach (var change in comparison.Changes.Take(ShownLimit)) _rows.Children.Add(Row(change, texts, metrics));
            if (comparison.Changes.Count > ShownLimit)
                _rows.Children.Add(new TextBlock { Text = texts.Format("versions.compare.more", comparison.Changes.Count - ShownLimit), Margin = new Thickness(metrics.SpacingMedium) }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        }

        var scroller = new ScrollViewer { Content = _rows, MaxHeight = 420, MinHeight = 160 };
        AutomationProperties.SetName(scroller, texts["versions.compare.list"]);
        section.Add(null, new PanelListFrame(metrics, scroller), DialogRowKind.Wide, editorWidth: 640);

        Content = layout;
        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.close"], () => Completed?.Invoke(this, new VersionComparisonAnswer(false)))
            .WithAction(texts["versions.compare.openSide"], () => Completed?.Invoke(this, new VersionComparisonAnswer(true)), out _, primary: false);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public VersionComparison Comparison { get; }

    public int RowCount => _rows.Children.Count;

    private Control Chip(int count, string label, string colour, DesignTokens.Metrics metrics)
    {
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center }
            .Themed(Border.BackgroundProperty, colour);
        var text = new TextBlock { Text = $"{count} {label}", FontSize = metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(dot);
        row.Children.Add(text);
        return new Border { Padding = new Thickness(8, 3), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Child = row }
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }

    private Control Row(VersionChange change, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var (tag, colour) = change.Kind switch
        {
            VersionChangeKind.Added => (texts["versions.compare.added"], Palette.Keys.Success),
            VersionChangeKind.Removed => (texts["versions.compare.removed"], Palette.Keys.Danger),
            _ => (texts["versions.compare.changed"], Palette.Keys.Info),
        };

        var stripe = new Border { Width = 3, CornerRadius = new CornerRadius(1.5) }.Themed(Border.BackgroundProperty, colour);
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        heading.Children.Add(new TextBlock { Text = change.Location, FontWeight = FontWeight.SemiBold, FontSize = metrics.FontSizeSmall });
        heading.Children.Add(new TextBlock { Text = tag, FontSize = metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, colour));

        var body = new TextBlock { TextWrapping = TextWrapping.Wrap };
        body.Inlines ??= [];
        foreach (var run in change.Runs) body.Inlines.Add(Inline(run));
        if (change.Runs.All(run => string.IsNullOrWhiteSpace(run.Text)))
        {
            body.Inlines.Clear();
            body.Text = texts["versions.compare.noText"];
            body.FontStyle = FontStyle.Italic;
            body.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        }

        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(heading);
        content.Children.Add(body);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(metrics.SpacingSmall, 4) };
        Grid.SetColumn(content, 1);
        content.Margin = new Thickness(metrics.SpacingSmall, 0, 0, 0);
        grid.Children.Add(stripe);
        grid.Children.Add(content);
        AutomationProperties.SetName(grid, $"{change.Location}, {tag}: {string.Concat(change.Runs.Select(run => run.Text))}");
        return grid;
    }

    private Run Inline(VersionRun run)
    {
        var inline = new Run(run.Text);
        switch (run.Kind)
        {
            case VersionRunKind.Added:
                inline.TextDecorations = TextDecorations.Underline;
                inline.FontWeight = FontWeight.SemiBold;
                inline.ThemedInline(TextElement.ForegroundProperty, Palette.Keys.Success);
                break;
            case VersionRunKind.Removed:
                inline.TextDecorations = TextDecorations.Strikethrough;
                inline.ThemedInline(TextElement.ForegroundProperty, Palette.Keys.Danger);
                break;
        }

        return inline;
    }
}
