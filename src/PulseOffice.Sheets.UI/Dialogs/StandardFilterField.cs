using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Rows;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Dialogs;

public sealed record StandardFilterField(string Key, string Label, IReadOnlyList<string> Fields)
    : CustomFormField(Key, Label)
{
    public IReadOnlyList<StandardFilterRow> Initial { get; init; } = [];

    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new StandardFilterEditor(texts, metrics, Fields, Initial);
        return new FormFieldEditor(editor, () => editor.Rows);
    }
}

public sealed class StandardFilterEditor : ConditionRowsEditor<StandardFilterRow>
{
    public const int MaximumConditions = 8;

    private static readonly FilterCondition[] Conditions = Enum.GetValues<FilterCondition>();

    private readonly IReadOnlyList<string> _fields;

    public StandardFilterEditor(ITextCatalog texts, DesignTokens.Metrics metrics, IReadOnlyList<string> fields)
        : this(texts, metrics, fields, [])
    {
    }

    public StandardFilterEditor(ITextCatalog texts, DesignTokens.Metrics metrics, IReadOnlyList<string> fields, IReadOnlyList<StandardFilterRow> initial)
        : base(texts, metrics, new ConditionRowsLayout(
            ["filter.join", "filter.column", "filter.operator", "filter.value"],
            "Auto,1.2*,1.2*,*", "filter.addCondition", "filter.noConditions", MaximumConditions))
    {
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        Load(initial is { Count: > 0 } ? initial : [NewRow()]);
    }

    protected override StandardFilterRow NewRow() => new(FilterJoin.And, 0, FilterCondition.Equal, string.Empty);

    protected override IConditionRowEditor<StandardFilterRow> CreateEditor(StandardFilterRow row) =>
        new Editor(row, _fields,
            [Texts["filter.and"], Texts["filter.or"]],
            [.. Conditions.Select(condition => Texts["filter.condition." + char.ToLowerInvariant(condition.ToString()[0]) + condition.ToString()[1..]])]);

    private sealed class Editor : IConditionRowEditor<StandardFilterRow>
    {
        private readonly ComboBox _join;
        private readonly ComboBox _field;
        private readonly ComboBox _condition;
        private readonly TextBox _value;
        private bool _first;

        public Editor(StandardFilterRow row, IReadOnlyList<string> fields, IReadOnlyList<string> joins, IReadOnlyList<string> conditions)
        {
            _join = new ComboBox { ItemsSource = joins, SelectedIndex = (int)row.Join, MinWidth = 76 };
            _field = new ComboBox
            {
                ItemsSource = fields,
                SelectedIndex = Math.Clamp(row.Field, 0, Math.Max(0, fields.Count - 1)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _condition = new ComboBox
            {
                ItemsSource = conditions,
                SelectedIndex = Array.IndexOf(Conditions, row.Condition),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _value = new TextBox { Text = row.Value };
            _condition.SelectionChanged += (_, _) => _value.IsEnabled = TakesValue();
            _value.IsEnabled = TakesValue();
            Cells = [_join, _field, _condition, _value];
        }

        public IReadOnlyList<Control> Cells { get; }

        public Control First => _field;

        public StandardFilterRow Read() => new(
            _first ? FilterJoin.And : (FilterJoin)Math.Max(_join.SelectedIndex, 0),
            Math.Max(_field.SelectedIndex, 0),
            Conditions[Math.Max(_condition.SelectedIndex, 0)],
            _value.Text ?? string.Empty);

        private bool TakesValue() =>
            Conditions[Math.Max(_condition.SelectedIndex, 0)] is not (FilterCondition.IsEmpty or FilterCondition.IsNotEmpty);

        public void Arrange(int position)
        {
            _first = position == 0;
            _join.IsVisible = !_first;
        }
    }
}
