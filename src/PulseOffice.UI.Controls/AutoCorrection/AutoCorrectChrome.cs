using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.AutoCorrection;

public sealed class AutoCorrectChrome(ITextCatalog texts, IIconCatalog icons, DesignTokens.Metrics metrics)
    : DialogChrome(icons, metrics)
{
    public ITextCatalog Texts { get; } = texts ?? throw new ArgumentNullException(nameof(texts));

    public TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = Metrics.FontSizeHeading,
        FontWeight = FontWeight.SemiBold,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public TextBox Field(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        Padding = new Thickness(Metrics.SpacingMedium, Metrics.SpacingSmall),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    public Button Quiet(string label, Action run)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(Metrics.SpacingMedium, Metrics.SpacingSmall),
            CornerRadius = new CornerRadius(Metrics.RadiusTight),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Click += (_, _) => run();
        return button;
    }

    public Button Primary(string label, Action run)
    {
        var button = Quiet(label, run);
        button.Classes.Add("dialogPrimary");
        return button;
    }

    public Button IconButton(string iconKey, string tip, Action run)
    {
        var button = new Button
        {
            Content = Icon(iconKey, 14),
            Padding = new Thickness(5),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Metrics.RadiusTight),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("ribbon");
        ToolTip.SetTip(button, tip);
        button.Click += (_, _) => run();
        return button;
    }

    public Border Frame(Control child) => new Border
    {
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(Metrics.RadiusSoft),
        ClipToBounds = true,
        Child = child,
    }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

    public Control OriginBadge(EntryOrigin origin)
    {
        var mine = origin is EntryOrigin.User or EntryOrigin.Modified;
        var label = new TextBlock
        {
            Text = Texts[OriginKey(origin)],
            FontSize = Math.Max(9, Metrics.FontSizeSmall - 1),
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, mine ? Palette.Keys.TextPrimary : Palette.Keys.TextSecondary);

        var dot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = mine,
        };
        dot.Bind(Border.BackgroundProperty, dot.GetResourceObservable("PulseAccent"));

        var pill = new Border
        {
            Padding = new Thickness(Metrics.SpacingSmall, 1),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { dot, label } },
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        ToolTip.SetTip(pill, Texts[OriginKey(origin) + ".tip"]);
        return pill;
    }

    public string OriginKey(EntryOrigin origin) => origin switch
    {
        EntryOrigin.Bundled => "autocorrect.origin.bundled",
        EntryOrigin.Modified => "autocorrect.origin.modified",
        EntryOrigin.User => "autocorrect.origin.user",
        _ => "autocorrect.origin.builtIn",
    };

    public string Problem(EntryProblem problem, string invalidKey) => problem switch
    {
        EntryProblem.Empty => Texts["autocorrect.problem.empty"],
        EntryProblem.Same => Texts["autocorrect.problem.same"],
        EntryProblem.Duplicate => Texts["autocorrect.problem.duplicate"],
        EntryProblem.Invalid => Texts[invalidKey],
        _ => string.Empty,
    };

    public TextBlock Message() => new TextBlock
    {
        FontSize = Metrics.FontSizeSmall,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        Margin = new Thickness(0, Metrics.SpacingSmall / 2, 0, 0),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

    public void Show(TextBlock message, string text, bool success = false)
    {
        message.Themed(TextBlock.ForegroundProperty, success ? Palette.Keys.Success : Palette.Keys.Danger);
        message.Text = text;
        message.IsVisible = text.Length > 0;
    }

    public string LanguageName(string language)
    {
        try
        {
            var name = CultureInfo.GetCultureInfo(language).NativeName;
            return name.Length == 0 ? language : $"{char.ToUpper(name[0], CultureInfo.CurrentCulture)}{name[1..]} · {language}";
        }
        catch (CultureNotFoundException)
        {
            return language;
        }
    }

    public string Count(int count) => Texts.Format("autocorrect.count", count.ToString("N0", CultureInfo.CurrentCulture));

    public string Count(int shown, int total) => shown == total
        ? Count(total)
        : Texts.Format("autocorrect.countFiltered", shown.ToString("N0", CultureInfo.CurrentCulture), total.ToString("N0", CultureInfo.CurrentCulture));
}
