using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Analysis;

public sealed record PivotFormulaField(string Key, string Label, string Initial, IReadOnlyList<string> Headers) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new PivotFormulaEditor(this, texts, metrics);
        return new FormFieldEditor(editor, () => editor.Formula);
    }
}

public sealed class PivotFormulaEditor : UserControl
{
    private readonly TextBox _box;

    public PivotFormulaEditor(PivotFormulaField field, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(texts);
        _box = new TextBox { Text = field.Initial, FontFamily = new FontFamily("Menlo, Consolas, monospace") };
        AutomationProperties.SetName(_box, field.Label);

        var chips = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
        foreach (var header in field.Headers)
        {
            var name = header;
            var chip = new Button { Content = header, Padding = new Thickness(8, 2), FontSize = metrics.FontSizeSmall + 0.5 };
            chip.Classes.Add("dialogQuiet");
            AutomationProperties.SetName(chip, texts.Format("pivot.calculated.insertField", header));
            ToolTip.SetTip(chip, texts.Format("pivot.calculated.insertField", header));
            chip.Click += (_, _) => Insert(name);
            Chips.Add(chip);
            chips.Children.Add(chip);
        }

        var caption = new TextBlock { Text = texts["pivot.calculated.fields"], FontSize = metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Content = new StackPanel { Spacing = metrics.SpacingSmall, Children = { _box, caption, chips } };
    }

    public List<Button> Chips { get; } = [];

    public string Formula => _box.Text ?? string.Empty;

    public void Insert(string header)
    {
        var text = _box.Text ?? string.Empty;
        var caret = Math.Clamp(_box.CaretIndex, 0, text.Length);
        var token = header.Any(character => !char.IsLetterOrDigit(character) && character != '_') ? "'" + header + "'" : header;
        _box.Text = text.Insert(caret, token);
        _box.CaretIndex = caret + token.Length;
        _box.Focus();
    }
}
