using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.References;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public interface IReferenceSource
{
    bool IsValid(string text);

    PulseOffice.UI.Controls.References.IReferencePicker? Picker { get; }
}

public sealed record RangeField(string Key, string Label, string Initial, IReferenceSource Source)
    : CustomFormField(Key, Label)
{
    public bool Optional { get; init; }

    public string? Placeholder { get; init; }

    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new RangeFieldControl(this, texts, metrics);
        return new FormFieldEditor(editor, () => editor.Text);
    }
}

public sealed class RangeFieldControl : UserControl
{
    private readonly RangeField _field;
    private readonly TextBox _box;
    private readonly TextBlock _error;

    public RangeFieldControl(RangeField field, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        _field = field ?? throw new ArgumentNullException(nameof(field));
        ArgumentNullException.ThrowIfNull(texts);

        _box = new TextBox
        {
            Text = field.Initial,
            PlaceholderText = field.Placeholder ?? string.Empty,
            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
        };

        _error = new TextBlock
        {
            Text = texts["range.invalid"],
            FontSize = metrics.FontSizeSmall,
            IsVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        _box.TextChanged += (_, _) =>
        {
            Check();
            TextChanged?.Invoke(this, EventArgs.Empty);
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingSmall };
        row.Children.Add(_box);

        if (field.Source.Picker is { } picker)
        {
            var pick = new ReferencePickButton(picker, texts, metrics);
            pick.Click += async (_, _) => await PickAsync(picker).ConfigureAwait(true);
            Grid.SetColumn(pick, 1);
            row.Children.Add(pick);
        }

        Content = new StackPanel { Spacing = 2, Children = { row, _error } };
        Check();
    }

    private async Task PickAsync(IReferencePicker picker)
    {
        var chosen = await picker.PickAsync(new ReferencePickRequest(_field.Label, Text, _field.Source.IsValid))
            .ConfigureAwait(true);

        if (chosen is not null) _box.Text = chosen;

        _box.Focus();
        _box.CaretIndex = _box.Text?.Length ?? 0;
    }

    public event EventHandler? TextChanged;

    public string Text
    {
        get => (_box.Text ?? string.Empty).Trim();
        set => _box.Text = value;
    }

    public bool IsValid => Text.Length == 0 ? _field.Optional : _field.Source.IsValid(Text);

    private void Check() => _error.IsVisible = Text.Length > 0 && !_field.Source.IsValid(Text);
}
