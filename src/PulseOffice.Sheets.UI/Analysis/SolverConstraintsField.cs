using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Rows;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Analysis;

public sealed record SolverConstraintsField(string Key, string Label, string SuggestedCell) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new SolverConstraintsEditor(texts, metrics, SuggestedCell);
        return new FormFieldEditor(editor, () => editor.Rows);
    }
}

public sealed class SolverConstraintsEditor : ConditionRowsEditor<SolverConstraintRow>
{
    private static readonly ConstraintOperator[] Operators =
    [
        ConstraintOperator.LessOrEqual, ConstraintOperator.Equal, ConstraintOperator.GreaterOrEqual,
        ConstraintOperator.Integer, ConstraintOperator.Binary,
    ];

    private readonly string _suggestedCell;

    public SolverConstraintsEditor(ITextCatalog texts, DesignTokens.Metrics metrics, string suggestedCell)
        : base(texts, metrics, new ConditionRowsLayout(
            ["analysis.constraint.cell", "analysis.constraint.operator", "analysis.constraint.value"],
            "*,1.3*,*", "analysis.constraint.add", "analysis.constraint.empty"))
    {
        _suggestedCell = suggestedCell;
        Load([]);
    }

    protected override SolverConstraintRow NewRow() =>
        new(_suggestedCell, ConstraintOperator.LessOrEqual, string.Empty);

    protected override IConditionRowEditor<SolverConstraintRow> CreateEditor(SolverConstraintRow row) =>
        new Editor(row, [.. Operators.Select(Label)]);

    private string Label(ConstraintOperator op) => Texts["analysis.constraint." + op switch
    {
        ConstraintOperator.LessOrEqual => "lessOrEqual",
        ConstraintOperator.Equal => "equal",
        ConstraintOperator.GreaterOrEqual => "greaterOrEqual",
        ConstraintOperator.Integer => "integer",
        _ => "binary",
    }];

    private sealed class Editor : IConditionRowEditor<SolverConstraintRow>
    {
        private readonly TextBox _cell;
        private readonly ComboBox _operator;
        private readonly TextBox _value;

        public Editor(SolverConstraintRow row, IReadOnlyList<string> labels)
        {
            _cell = new TextBox { Text = row.Cell, PlaceholderText = "B1" };
            _operator = new ComboBox
            {
                ItemsSource = labels,
                SelectedIndex = Array.IndexOf(Operators, row.Operator),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _value = new TextBox { Text = row.Value, PlaceholderText = "0", IsVisible = row.NeedsValue };
            _operator.SelectionChanged += (_, _) => _value.IsVisible = Read().NeedsValue;
            Cells = [_cell, _operator, _value];
        }

        public IReadOnlyList<Control> Cells { get; }

        public Control First => _cell;

        public SolverConstraintRow Read() => new(
            _cell.Text ?? string.Empty,
            Operators[Math.Max(_operator.SelectedIndex, 0)],
            _value.Text ?? string.Empty);

        public void Arrange(int position)
        {
        }
    }
}
