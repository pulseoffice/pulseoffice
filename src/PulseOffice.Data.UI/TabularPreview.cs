using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Data.UI;

public sealed class DataPreviewControl(IDataTableRenderer renderer) : Control
{
    private DataTableObject _table = new()
    {
        Id = "preview",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
    };

    public void Show(DataSnapshot snapshot)
    {
        _table = _table with { Snapshot = snapshot };
        InvalidateVisual();
    }

    public override void Render(DrawingContext context) => renderer.Render(context, _table, new Rect(Bounds.Size), System.Globalization.CultureInfo.CurrentCulture);
}

public sealed record TabularPreviewChoice(string Key, string Label);

public interface ITabularSnapshots
{
    DataSnapshot From(IReadOnlyList<IReadOnlyList<string>> rows, int maximumRows);
}

public sealed class TabularSnapshots : ITabularSnapshots
{
    public DataSnapshot From(IReadOnlyList<IReadOnlyList<string>> rows, int maximumRows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var width = rows.Count == 0 ? 0 : Math.Min(rows.Max(row => row.Count), 64);
        var shown = rows.Take(Math.Max(maximumRows, 0)).ToList();

        return new DataSnapshot
        {
            Columns = [.. Enumerable.Range(0, width).Select(index => new DataColumn(Letters(index), DataValueKind.Text))],
            Rows = [.. shown.Select(row => (IReadOnlyList<object?>)[.. Enumerable.Range(0, width).Select(index => (object?)(index < row.Count ? row[index] : string.Empty))])],
            Truncated = rows.Count > shown.Count,
        };
    }

    private string Letters(int index)
    {
        var name = string.Empty;
        for (var value = index + 1; value > 0; value = (value - 1) / 26)
            name = (char)('A' + (value - 1) % 26) + name;
        return name;
    }
}

public sealed class TabularPreviewView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    public const int PreviewRows = 200;

    private readonly IReadOnlyList<TabularPreviewChoice> _choices;
    private readonly Func<string, IReadOnlyList<IReadOnlyList<string>>> _read;
    private readonly ITabularSnapshots _snapshots;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ListBox _list = new();
    private readonly DataPreviewControl _preview;
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 700;

    public TabularPreviewView(
        string explanation,
        IReadOnlyList<TabularPreviewChoice> choices,
        string initial,
        Func<string, IReadOnlyList<IReadOnlyList<string>>> read,
        ITabularSnapshots snapshots,
        IDataTableRenderer renderer,
        ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (choices.Count == 0) throw new ArgumentException("At least one table is required.", nameof(choices));
        _choices = choices;
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);
        _preview = new DataPreviewControl(renderer ?? throw new ArgumentNullException(nameof(renderer)));

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["externalData.preview.use"], Confirm, primary: true);

        Content = Build(explanation);

        var start = 0;
        for (var index = 0; index < choices.Count; index++)
            if (choices[index].Key == initial) start = index;
        _list.SelectedIndex = start;
        ShowSelected();
    }

    private Control Build(string explanation)
    {
        var intro = new TextBlock
        {
            Text = explanation,
            FontSize = _metrics.FontSize,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, _metrics.SpacingMedium),
        };

        _list.Classes.Add("compact");
        _list.ItemsSource = _choices.Select(choice => choice.Label).ToList();
        _list.SelectionChanged += (_, _) => ShowSelected();
        _list.DoubleTapped += (_, _) => Confirm();

        var sidebar = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var tablesCaption = Caption(_texts["externalData.preview.tables"]);
        Grid.SetRow(_list, 1);
        sidebar.Children.Add(tablesCaption);
        sidebar.Children.Add(_list);

        var plate = new Border
        {
            Child = _preview,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            ClipToBounds = true,
        };
        plate.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        plate.Background = Brushes.White;

        _summary.FontSize = _metrics.FontSizeSmall;
        _summary.Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0);
        _summary.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var previewCaption = Caption(_texts["externalData.preview.caption"]);
        Grid.SetRow(plate, 1);
        Grid.SetRow(_summary, 2);
        content.Children.Add(previewCaption);
        content.Children.Add(plate);
        content.Children.Add(_summary);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(_choices.Count > 1 ? "190,*" : "0,*"),
            ColumnSpacing = _choices.Count > 1 ? _metrics.SpacingMedium : 0,
            Height = 380,
        };
        sidebar.IsVisible = _choices.Count > 1;
        Grid.SetColumn(content, 1);
        body.Children.Add(sidebar);
        body.Children.Add(content);

        var layout = new DockPanel { MinWidth = 640 };
        DockPanel.SetDock(intro, Dock.Top);
        layout.Children.Add(intro);
        layout.Children.Add(body);
        return layout;
    }

    private void ShowSelected()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _choices.Count) return;

        var rows = _read(_choices[_list.SelectedIndex].Key);
        var snapshot = _snapshots.From(rows, PreviewRows);
        _preview.Show(snapshot);

        var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        _summary.Text = rows.Count == 0
            ? _texts["externalData.preview.empty"]
            : snapshot.Truncated
                ? _texts.Format("externalData.preview.partial", rows.Count, columns, snapshot.Rows.Count)
                : _texts.Format("externalData.preview.size", rows.Count, columns);
    }

    private void Confirm()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _choices.Count) return;
        Completed?.Invoke(this, _choices[_list.SelectedIndex].Key);
    }

    private TextBlock Caption(string text) =>
        new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall / 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
}
