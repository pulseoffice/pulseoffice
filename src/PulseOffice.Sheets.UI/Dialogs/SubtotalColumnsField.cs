using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Dialogs;

public sealed record SubtotalColumnChoice(int Offset, int Function);

public sealed record SubtotalColumnsField(
    string Key, string Label, IReadOnlyList<string> Columns, IReadOnlyList<string> Functions, IReadOnlyList<SubtotalColumnChoice> Initial)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new SubtotalColumnsEditor(this, metrics);
        return new FormFieldEditor(editor, () => editor.Chosen);
    }
}

public sealed class SubtotalColumnsEditor : UserControl
{
    private const double ListHeight = 200;

    private readonly List<(CheckBox Use, ComboBox Function)> _rows = [];

    public SubtotalColumnsEditor(SubtotalColumnsField field, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(field);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = metrics.SpacingMedium,
            RowSpacing = metrics.SpacingSmall,
        };

        for (var index = 0; index < field.Columns.Count; index++)
        {
            var initial = field.Initial.FirstOrDefault(choice => choice.Offset == index);
            var use = new CheckBox
            {
                Content = field.Columns[index],
                IsChecked = initial is not null,
                FontSize = metrics.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var function = new ComboBox
            {
                ItemsSource = field.Functions,
                SelectedIndex = Math.Clamp(initial?.Function ?? 0, 0, Math.Max(0, field.Functions.Count - 1)),
                IsEnabled = initial is not null,
                MinWidth = metrics.FontSize * 11,
                FontSize = metrics.FontSize,
            };
            use.IsCheckedChanged += (_, _) => function.IsEnabled = use.IsChecked == true;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(use, index);
            Grid.SetRow(function, index);
            Grid.SetColumn(function, 1);
            grid.Children.Add(use);
            grid.Children.Add(function);
            _rows.Add((use, function));
        }

        Content = new ScrollViewer
        {
            MaxHeight = ListHeight,
            Content = grid,
            Padding = new Thickness(0, 0, metrics.SpacingSmall, 0),
        };
    }

    public IReadOnlyList<SubtotalColumnChoice> Chosen =>
        [.. _rows
            .Select((row, index) => (row, index))
            .Where(pair => pair.row.Use.IsChecked == true)
            .Select(pair => new SubtotalColumnChoice(pair.index, Math.Max(0, pair.row.Function.SelectedIndex)))];
}
