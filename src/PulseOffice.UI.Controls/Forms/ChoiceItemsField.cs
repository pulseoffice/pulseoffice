using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Rows;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Forms;

public sealed record ChoiceItem(string DisplayText, string Value);

public sealed record ChoiceItemsField(string Key, string Label, IReadOnlyList<ChoiceItem> Initial) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new ChoiceItemsEditor(texts, metrics, Initial);
        return new FormFieldEditor(editor, () => editor.Rows.Where(item => item.DisplayText.Length > 0 || item.Value.Length > 0).ToList());
    }
}

public sealed class ChoiceItemsEditor : ConditionRowsEditor<ChoiceItem>
{
    public ChoiceItemsEditor(ITextCatalog texts, DesignTokens.Metrics metrics, IReadOnlyList<ChoiceItem> initial)
        : base(texts, metrics, new ConditionRowsLayout(["choiceItems.display", "choiceItems.value"], "*,*", "choiceItems.add", "choiceItems.empty"))
    {
        Load(initial);
    }

    protected override ChoiceItem NewRow() => new(string.Empty, string.Empty);

    protected override IConditionRowEditor<ChoiceItem> CreateEditor(ChoiceItem row) => new Editor(row, Texts);

    private sealed class Editor : IConditionRowEditor<ChoiceItem>
    {
        private readonly TextBox _display;
        private readonly TextBox _value;

        public Editor(ChoiceItem row, ITextCatalog texts)
        {
            _display = new TextBox { Text = row.DisplayText, PlaceholderText = texts["choiceItems.display"] };
            _value = new TextBox { Text = row.Value, PlaceholderText = texts["choiceItems.sameAsDisplay"] };
            Avalonia.Automation.AutomationProperties.SetName(_display, texts["choiceItems.display"]);
            Avalonia.Automation.AutomationProperties.SetName(_value, texts["choiceItems.value"]);
        }

        public IReadOnlyList<Control> Cells => [_display, _value];

        public Control First => _display;

        public ChoiceItem Read()
        {
            var display = _display.Text ?? string.Empty;
            var value = _value.Text is { Length: > 0 } typed ? typed : display;
            return new ChoiceItem(display, value);
        }

        public void Arrange(int position)
        {
        }
    }
}
