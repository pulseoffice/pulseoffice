using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Scripting.Editor;

public sealed class DocumentationPanel : UserControl
{
    private static readonly FontFamily Mono = new("Menlo, Consolas, monospace");

    private readonly DesignTokens.Metrics _metrics;
    private readonly IReadOnlyDictionary<string, string> _texts;
    private readonly StackPanel _body;

    public DocumentationPanel(IReadOnlyDictionary<string, string> texts, InterfaceDensity density)
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

        _body.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = _metrics.FontSizeSmall,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
    }

    public void ShowGuide()
    {
        _body.Children.Clear();

        _body.Children.Add(Heading(_texts["guide.title"]));

        foreach (var key in new[] { "guide.editor", "guide.docs", "guide.language", "guide.lists" })
        {
            var parts = _texts[key].Split('|', 2);

            _body.Children.Add(new TextBlock
            {
                Text = parts[0],
                FontSize = _metrics.FontSizeSmall,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

            _body.Children.Add(new TextBlock
            {
                Text = parts.Length > 1 ? parts[1] : string.Empty,
                FontSize = _metrics.FontSize,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = _metrics.FontSize * 1.45,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));
        }
    }

    private Control Heading(string text) => new TextBlock
    {
        Text = text,
        FontSize = _metrics.FontSize + 1,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

    public void Show(DocumentationTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);

        _body.Children.Clear();

        _body.Children.Add(new TextBlock
        {
            Text = topic.Title,
            FontSize = _metrics.FontSize + 1,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        _body.Children.Add(Signature(topic.Signature));

        if (topic.Summary is { Length: > 0 })
            _body.Children.Add(new TextBlock
            {
                Text = topic.Summary,
                FontSize = _metrics.FontSize,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = _metrics.FontSize * 1.45,
                Margin = new Thickness(0, _metrics.SpacingSmall / 2, 0, 0),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));

        if (topic.Parameters.Count == 0) return;

        _body.Children.Add(new TextBlock
        {
            Text = _texts["parameters"],
            FontSize = _metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        foreach (var parameter in topic.Parameters) _body.Children.Add(Parameter(parameter));
    }

    private Control Signature(string signature)
    {
        var text = new TextBlock
        {
            FontFamily = Mono,
            FontSize = _metrics.FontSizeSmall + 1,
            TextWrapping = TextWrapping.Wrap,
        };

        var open = signature.IndexOf('(', StringComparison.Ordinal);
        var colon = signature.LastIndexOf(':');

        if (open > 0 && colon > open)
        {
            text.Inlines =
            [
                Piece(signature[..open], Palette.Keys.TextPrimary, FontWeight.SemiBold),
                Piece(signature[open..colon], Palette.Keys.TextSecondary),
                Piece(signature[colon..], Palette.Keys.Success),
            ];
        }
        else if (colon > 0)
        {
            text.Inlines =
            [
                Piece(signature[..colon], Palette.Keys.TextPrimary, FontWeight.SemiBold),
                Piece(signature[colon..], Palette.Keys.Success),
            ];
        }
        else
        {
            text.Text = signature;
        }

        return new Border
        {
            Child = text,
            Padding = new Thickness(_metrics.SpacingSmall, _metrics.SpacingSmall / 2),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Margin = new Thickness(0, _metrics.SpacingSmall / 2, 0, 0),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
    }

    private Control Parameter(DocumentationParameter parameter)
    {
        var line = new TextBlock
        {
            FontSize = _metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = _metrics.FontSizeSmall * 1.5,
            Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0),
        };

        line.Inlines =
        [
            Piece(parameter.Name, Palette.Keys.Warning, FontWeight.SemiBold, Mono),
            Piece("  " + parameter.Description, Palette.Keys.TextSecondary),
        ];

        return line;
    }

    private static Run Piece(
        string text, string colourKey, FontWeight weight = FontWeight.Normal, FontFamily? family = null)
    {
        var run = new Run(text) { FontWeight = weight };

        if (family is not null) run.FontFamily = family;

        return run.ThemedInline(Avalonia.Controls.Documents.Run.ForegroundProperty, colourKey);
    }
}
