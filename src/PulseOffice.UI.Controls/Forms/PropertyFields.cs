using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Rows;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Forms;

public sealed record InfoField(string Key, string Label, string Value) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var text = new SelectableTextBlock
        {
            Text = Value,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = metrics.FontSize,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        return new FormFieldEditor(text, () => Value);
    }
}

public sealed record CustomPropertyItem(string Name, string Value, int Kind);

public sealed record CustomPropertiesField(string Key, string Label, IReadOnlyList<CustomPropertyItem> Initial, IReadOnlyList<string> Kinds)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new CustomPropertiesEditor(texts, metrics, Initial, Kinds);
        return new FormFieldEditor(editor, () => editor.Rows.Where(item => item.Name.Trim().Length > 0).ToList(), Labelled: false);
    }
}

public sealed class CustomPropertiesEditor : ConditionRowsEditor<CustomPropertyItem>
{
    private readonly IReadOnlyList<string> _kinds;

    public CustomPropertiesEditor(ITextCatalog texts, DesignTokens.Metrics metrics, IReadOnlyList<CustomPropertyItem> initial, IReadOnlyList<string> kinds)
        : base(texts, metrics, new ConditionRowsLayout(
            ["properties.custom.name", "properties.custom.value", "properties.custom.kind"], "*,*,Auto", "properties.custom.add", "properties.custom.empty"))
    {
        _kinds = kinds;
        Load(initial);
    }

    protected override CustomPropertyItem NewRow() => new(string.Empty, string.Empty, 0);

    protected override IConditionRowEditor<CustomPropertyItem> CreateEditor(CustomPropertyItem row) => new Editor(row, Texts, _kinds);

    private sealed class Editor : IConditionRowEditor<CustomPropertyItem>
    {
        private readonly TextBox _name;
        private readonly TextBox _value;
        private readonly ComboBox _kind;

        public Editor(CustomPropertyItem row, ITextCatalog texts, IReadOnlyList<string> kinds)
        {
            _name = new TextBox { Text = row.Name, PlaceholderText = texts["properties.custom.name"] };
            _value = new TextBox { Text = row.Value, PlaceholderText = texts["properties.custom.value"] };
            _kind = new ComboBox { ItemsSource = kinds, SelectedIndex = Math.Clamp(row.Kind, 0, kinds.Count - 1), MinWidth = 96 };
            Avalonia.Automation.AutomationProperties.SetName(_kind, texts["properties.custom.kind"]);
            Avalonia.Automation.AutomationProperties.SetName(_name, texts["properties.custom.name"]);
            Avalonia.Automation.AutomationProperties.SetName(_value, texts["properties.custom.value"]);
        }

        public IReadOnlyList<Control> Cells => [_name, _value, _kind];

        public Control First => _name;

        public CustomPropertyItem Read() => new(_name.Text?.Trim() ?? string.Empty, _value.Text ?? string.Empty, Math.Max(0, _kind.SelectedIndex));

        public void Arrange(int position)
        {
        }
    }
}
