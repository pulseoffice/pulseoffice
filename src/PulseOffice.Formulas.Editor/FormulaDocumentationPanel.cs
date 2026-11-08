using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Formulas;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Formulas.Editor;

public sealed class FormulaDocumentationPanel : UserControl
{
    private static readonly FontFamily Mono = new("Menlo, Consolas, monospace");

    private readonly DesignTokens.Metrics _metrics;
    private readonly IReadOnlyDictionary<string, string> _texts;
    private readonly StackPanel _body;

    public FormulaDocumentationPanel(IReadOnlyDictionary<string, string> texts, InterfaceDensity density)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        _body = new StackPanel { Spacing = _metrics.SpacingSmall };

        Content = new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Padding = new Thickness(0, _metrics.SpacingSmall, _metrics.SpacingSmall, 0),
        };
    }

    public void ShowEmpty(string message)
    {
        _body.Children.Clear();
        _body.Children.Add(Secondary(message));
    }

    public void ShowGuide()
    {
        _body.Children.Clear();

        _body.Children.Add(Heading(_texts["guide.title"]));

        foreach (var key in new[] { "guide.syntax", "guide.palette", "guide.preview", "guide.problems" })
            _body.Children.Add(Secondary(_texts[key]));
    }

    public void Show(FormulaEntry entry, string title, string? summary)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _body.Children.Clear();

        _body.Children.Add(Heading(title));

        var sample = new FormulaSample { Height = 54, GlyphSize = 24 };
        sample.Show(entry.Snippet);

        _body.Children.Add(Plate(sample));
        _body.Children.Add(Snippet(entry.Snippet));

        if (summary is { Length: > 0 }) _body.Children.Add(Secondary(summary));

        _body.Children.Add(new TextBlock
        {
            Text = _texts["insertHint"],
            FontSize = _metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
    }

    private Control Plate(Control child)
    {
        var plate = new Border
        {
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Child = child,
        };

        plate.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);
        plate.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        plate.BorderThickness = new Thickness(1);

        return plate;
    }

    private Control Snippet(string snippet)
    {
        var text = new TextBlock
        {
            FontFamily = Mono,
            FontSize = _metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
        };

        text.Inlines?.Add(new Run(snippet)
            .ThemedInline(Run.ForegroundProperty, "PulseAccentText"));

        return Plate(text);
    }

    private Control Heading(string text) => new TextBlock
    {
        Text = text,
        FontSize = _metrics.FontSize,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private Control Secondary(string text) => new TextBlock
    {
        Text = text,
        FontSize = _metrics.FontSizeSmall,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = _metrics.FontSizeSmall * 1.5,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
}
