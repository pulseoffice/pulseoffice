using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed record PromptChoice(string Label, object Result, bool IsDefault = false, bool IsCancel = false)
{
    public string? Description { get; init; }

    public string? Icon { get; init; }
}

public sealed class ChoicePromptView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly List<ChoiceOptionCard> _cards = [];

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; private set; }

    public IReadOnlyList<ChoiceOptionCard> Cards => _cards;

    public ChoicePromptView(
        string question, string explanation, IReadOnlyList<PromptChoice> choices, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact, IIconCatalog? icons = null)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(texts);

        var metrics = DesignTokens.For(density);
        var options = choices.Where(choice => !choice.IsCancel).ToList();
        var asCards = options.Count >= 3 || options.Any(choice => !string.IsNullOrEmpty(choice.Description) || !string.IsNullOrEmpty(choice.Icon));

        var footer = new DialogFooter(density);
        var footerUsed = false;

        foreach (var choice in choices)
        {
            var result = choice.Result;
            if (choice.IsCancel)
            {
                footer.WithCancel(choice.Label, () => Completed?.Invoke(this, result));
                footerUsed = true;
            }
            else if (!asCards)
            {
                footer.WithAction(choice.Label, () => Completed?.Invoke(this, result), choice.IsDefault);
                footerUsed = true;
            }
        }

        Footer = footerUsed ? footer : null;

        var layout = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock
                {
                    Text = question,
                    FontSize = metrics.FontSize,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        if (!string.IsNullOrEmpty(explanation))
            layout.Children.Add(new TextBlock
            {
                Text = explanation,
                FontSize = metrics.FontSizeSmall,
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        if (asCards)
        {
            var list = new StackPanel { Spacing = metrics.SpacingSmall };
            foreach (var choice in options)
            {
                var result = choice.Result;
                var card = new ChoiceOptionCard(choice, metrics, icons);
                card.Click += (_, _) => Completed?.Invoke(this, result);
                card.KeyDown += (_, args) => MoveFocus(card, args);
                _cards.Add(card);
                list.Children.Add(card);
            }

            layout.Children.Add(list);
            Loaded += (_, _) => DefaultCard?.Focus(NavigationMethod.Tab);
        }

        Content = layout;
    }

    public ChoiceOptionCard? DefaultCard => _cards.FirstOrDefault(card => card.Choice.IsDefault) ?? _cards.FirstOrDefault();

    private void MoveFocus(ChoiceOptionCard card, KeyEventArgs args)
    {
        var step = args.Key switch
        {
            Key.Down or Key.Right => 1,
            Key.Up or Key.Left => -1,
            _ => 0,
        };
        if (step == 0) return;

        var index = (_cards.IndexOf(card) + step + _cards.Count) % _cards.Count;
        _cards[index].Focus(NavigationMethod.Directional);
        args.Handled = true;
    }
}
