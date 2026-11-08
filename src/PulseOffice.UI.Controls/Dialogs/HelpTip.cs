using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dialogs;

public sealed record HelpText(string Summary, string Details)
{
    public bool HasDetails => Details.Length > 0;
}

public interface IHelpTextSplitter
{
    HelpText Split(string text);
}

public sealed class HelpTextSplitter : IHelpTextSplitter
{
    private const int ShortEnough = 110;

    public HelpText Split(string text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length <= ShortEnough && !trimmed.Contains('\n')) return new HelpText(trimmed, string.Empty);

        var paragraph = trimmed.IndexOf('\n');
        var sentence = SentenceEnd(trimmed);
        var cut = paragraph >= 0 && (sentence < 0 || paragraph < sentence) ? paragraph : sentence;
        if (cut <= 0) return new HelpText(trimmed, string.Empty);

        var summary = trimmed[..cut].Trim();
        var details = trimmed[cut..].Trim();
        return details.Length == 0 ? new HelpText(summary, string.Empty) : new HelpText(summary, details);
    }

    private static int SentenceEnd(string text)
    {
        for (var index = 0; index < text.Length - 2; index++)
        {
            if (text[index] is not ('.' or '!' or '?' or '…' or ':')) continue;
            if (!char.IsWhiteSpace(text[index + 1])) continue;

            var next = index + 1;
            while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
            if (next < text.Length && (char.IsUpper(text[next]) || text[next] is '«' or '"' or '(' or '¿' or '¡')) return index + 1;
        }

        return -1;
    }
}

public sealed class HelpTip : UserControl
{
    private readonly Button? _more;

    public HelpTip(string text, ITextCatalog texts, DesignTokens.Metrics metrics, IHelpTextSplitter? splitter = null)
    {
        ArgumentNullException.ThrowIfNull(texts);

        Help = (splitter ?? new HelpTextSplitter()).Split(text);

        var summary = new TextBlock
        {
            Text = Help.Summary,
            FontSize = metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        if (!Help.HasDetails)
        {
            Content = summary;
            return;
        }

        var details = new TextBlock
        {
            Text = Help.Details,
            FontSize = metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
        };

        var name = texts["help.more"];
        _more = new Button
        {
            Width = 18,
            Height = 18,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new TextBlock
            {
                Text = "?",
                FontSize = metrics.FontSize - 1,
                FontWeight = FontWeight.SemiBold,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            Flyout = new Flyout
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                Content = new TextBlock
                {
                    Text = Help.Details,
                    FontSize = metrics.FontSize,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360,
                },
            },
        }.Themed(Button.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Button.BackgroundProperty, Palette.Keys.SurfaceSunken);
        _more.Classes.Add("helpTip");
        AutomationProperties.SetName(_more, name);
        AutomationProperties.SetHelpText(_more, Help.Details);
        ToolTip.SetTip(_more, details);
        ToolTip.SetShowDelay(_more, 150);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = metrics.SpacingSmall };
        Grid.SetColumn(_more, 1);
        summary.MaxWidth = 560;
        row.Children.Add(summary);
        row.Children.Add(_more);
        Content = row;
    }

    public HelpText Help { get; }

    public Button? MoreButton => _more;
}
