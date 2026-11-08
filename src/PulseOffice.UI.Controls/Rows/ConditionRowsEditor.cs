using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Rows;

public interface IConditionRowEditor<out TRow>
{
    IReadOnlyList<Control> Cells { get; }

    Control First { get; }

    TRow Read();

    void Arrange(int position);
}

public sealed record ConditionRowsLayout(
    IReadOnlyList<string> HeaderKeys, string Columns, string AddKey, string EmptyKey, int? MaximumRows = null);

public abstract class ConditionRowsEditor<TRow> : UserControl
{
    private readonly StackPanel _rows = new();
    private readonly List<IConditionRowEditor<TRow>> _editors = [];
    private readonly Button _add;

    protected ConditionRowsEditor(ITextCatalog texts, DesignTokens.Metrics metrics, ConditionRowsLayout layout)
    {
        Texts = texts ?? throw new ArgumentNullException(nameof(texts));
        Metrics = metrics;
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));

        _add = new Button { Content = "+  " + texts[layout.AddKey], HorizontalAlignment = HorizontalAlignment.Left, FontSize = metrics.FontSize };
        _add.Classes.Add("dialogQuiet");
        _add.Click += (_, _) => Append(NewRow(), focus: true);

        var panel = new StackPanel { Spacing = metrics.SpacingSmall };
        panel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            Padding = new Thickness(metrics.SpacingSmall),
            Child = new ScrollViewer { MaxHeight = 260, Content = _rows },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface));
        panel.Children.Add(_add);

        Content = panel;
    }

    protected ITextCatalog Texts { get; }

    protected DesignTokens.Metrics Metrics { get; }

    protected ConditionRowsLayout Layout { get; }

    public IReadOnlyList<TRow> Rows => [.. _editors.Select(editor => editor.Read())];

    protected abstract TRow NewRow();

    protected abstract IConditionRowEditor<TRow> CreateEditor(TRow row);

    protected void Load(IEnumerable<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows) Append(row, focus: false);
        Refresh();
    }

    private void Append(TRow row, bool focus)
    {
        if (Layout.MaximumRows is { } maximum && _editors.Count >= maximum) return;

        var editor = CreateEditor(row);
        _editors.Add(editor);
        Refresh();
        if (focus) editor.First.Focus();
    }

    private void Refresh()
    {
        _rows.Children.Clear();
        _add.IsEnabled = Layout.MaximumRows is not { } maximum || _editors.Count < maximum;

        if (_editors.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = Texts[Layout.EmptyKey],
                FontSize = Metrics.FontSizeSmall + 0.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(Metrics.SpacingSmall),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
            return;
        }

        _rows.Children.Add(Header());

        for (var position = 0; position < _editors.Count; position++)
        {
            var editor = _editors[position];
            editor.Arrange(position);

            var grid = Columns();
            grid.Margin = new Thickness(0, 2);
            for (var column = 0; column < editor.Cells.Count; column++) Place(grid, editor.Cells[column], column);

            var remove = new Button
            {
                Content = "✕",
                FontSize = Metrics.FontSizeSmall,
                Width = 28,
                Height = 26,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            remove.Classes.Add("dialogQuiet");
            ToolTip.SetTip(remove, Texts["list.remove"]);
            Avalonia.Automation.AutomationProperties.SetName(remove, Texts["list.remove"]);
            remove.Click += (_, _) =>
            {
                _editors.Remove(editor);
                Refresh();
            };
            Place(grid, remove, editor.Cells.Count);
            _rows.Children.Add(grid);
        }
    }

    private Grid Header()
    {
        var grid = Columns();
        for (var column = 0; column < Layout.HeaderKeys.Count; column++)
        {
            if (Layout.HeaderKeys[column].Length == 0) continue;
            Place(grid, new TextBlock
            {
                Text = Texts[Layout.HeaderKeys[column]],
                FontSize = Metrics.FontSizeSmall,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary), column);
        }

        return grid;
    }

    private Grid Columns() => new()
    {
        ColumnDefinitions = new ColumnDefinitions(Layout.Columns + ",Auto"),
        ColumnSpacing = Metrics.SpacingSmall,
    };

    private void Place(Grid grid, Control control, int column)
    {
        if (control.Parent is Panel previous) previous.Children.Remove(control);
        Grid.SetColumn(control, column);
        control.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(control);
    }
}
