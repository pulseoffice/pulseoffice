using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Prompts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class TextPromptView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly IReadOnlyList<ITextInputRule> _rules;
    private readonly TextBox _editor;
    private readonly TextBlock _message;
    private readonly Button _confirm;
    private readonly bool _multiline;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; private set; }

    public TextPromptView(
        string explanation, string initialText, ITextCatalog texts,
        bool multiline = false, InterfaceDensity density = InterfaceDensity.Compact,
        IReadOnlyList<ITextInputRule>? rules = null)
    {
        ArgumentNullException.ThrowIfNull(texts);

        _rules = rules ?? [];
        _multiline = multiline;
        var metrics = DesignTokens.For(density);
        var layout = new StackPanel { Spacing = metrics.SpacingMedium };

        if (explanation.Length > 0)
            layout.Children.Add(new Dialogs.HelpTip(explanation, texts, metrics));

        _editor = new TextBox
        {
            Text = initialText,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };

        if (multiline) _editor.MinHeight = 120;

        _message = new TextBlock
        {
            FontSize = metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
            Margin = new Thickness(2, -metrics.SpacingSmall, 0, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        _message.Classes.Add("validation");

        layout.Children.Add(_editor);
        layout.Children.Add(_message);

        Footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], Confirm, out _confirm, primary: true);

        Content = layout;

        _editor.TextChanged += (_, _) => Validate();
        _editor.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || _multiline) return;
            Confirm();
            e.Handled = true;
        };
        _editor.AttachedToVisualTree += (_, _) =>
        {
            _editor.Focus();
            if (!_multiline) _editor.SelectAll();
        };

        Validate();
    }

    public string Text
    {
        get => _editor.Text ?? string.Empty;
        set
        {
            _editor.Text = value;
            Validate();
        }
    }

    public string? Message => _message.IsVisible ? _message.Text : null;

    public bool CanConfirm => _confirm.IsEnabled;

    public void Confirm()
    {
        if (!Validate()) return;
        Completed?.Invoke(this, Text);
    }

    private bool Validate()
    {
        var problem = _rules.Select(rule => rule.Check(Text)).FirstOrDefault(message => message is not null);

        _message.Text = problem;
        _message.IsVisible = problem is not null;
        _confirm.IsEnabled = problem is null;
        AutomationProperties.SetHelpText(_editor, problem ?? string.Empty);
        _editor.Classes.Set("invalid", problem is not null);
        return problem is null;
    }
}
