using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record SeriesChoice(bool ByColumns, SeriesKind Kind, SeriesDateUnit Unit, double Step, double? Stop);

public sealed record SeriesPreview(IReadOnlyList<string> Values, bool Truncated);

public interface ISeriesPreviewer
{
    SeriesPreview Preview(SeriesChoice choice, int limit);
}

public sealed class SeriesView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const int PreviewLimit = 8;

    private readonly LedgerDialogKit _kit;
    private readonly ISeriesPreviewer _previewer;
    private readonly DialogRow _unitRow;
    private readonly TextBlock _stepError;
    private readonly StackPanel _cells = new() { Spacing = 0 };
    private readonly TextBlock _more;
    private readonly Button _confirm;

    public SeriesView(LedgerDialogKit kit, ISeriesPreviewer previewer, bool byColumns = true)
    {
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        _previewer = previewer ?? throw new ArgumentNullException(nameof(previewer));

        Direction = kit.Choices("series.direction",
        [
            ("columns", "series.direction.down", "series.direction.columns"),
            ("rows", "series.direction.right", "series.direction.rows"),
        ], byColumns ? 0 : 1, showLabels: true);
        Kind = kit.Choices("series.kind",
        [
            ("linear", "series.type.linear", "series.kind.linear"),
            ("growth", "series.type.growth", "series.kind.growth"),
            ("date", "series.type.date", "series.kind.date"),
            ("autoFill", "series.type.autoFill", "series.kind.autoFill"),
        ], 0, showLabels: true, columns: 2);
        Unit = kit.Choices("series.unit",
        [
            ("day", "validation.kind.date", "series.unit.day"),
            ("weekday", "validation.kind.date", "series.unit.weekday"),
            ("month", "validation.kind.date", "series.unit.month"),
            ("year", "validation.kind.date", "series.unit.year"),
        ], 0, showLabels: true, columns: 2);
        Step = new TextBox { Text = 1.ToString(CultureInfo.CurrentCulture), Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
        Stop = new TextBox { Width = 120, HorizontalAlignment = HorizontalAlignment.Left, PlaceholderText = kit["series.stop.none"] };
        _stepError = kit.Note();
        _stepError.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        _more = kit.Note();

        var layout = new TwoPaneDialogLayout(kit.Metrics) { Header = kit.Help("series.explanation"), Preview = BuildPreview() };
        var shape = layout.AddSection(kit["series.section.shape"]);
        shape.Add(kit["series.direction"], Direction);
        shape.Add(kit["series.kind"], Kind, alignTop: true);
        _unitRow = shape.Add(kit["series.unit"], Unit, alignTop: true);
        var numbers = layout.AddSection(kit["series.section.values"]);
        numbers.Add(kit["series.step"], Step);
        numbers.Add(kit["series.stop"], Stop);
        numbers.Add(null, _stepError, DialogRowKind.Indented);
        Layout = layout;
        Content = layout;

        Direction.SelectionChanged += (_, _) => Refresh();
        Kind.SelectionChanged += (_, _) => Refresh();
        Unit.SelectionChanged += (_, _) => Refresh();
        Step.TextChanged += (_, _) => Refresh();
        Stop.TextChanged += (_, _) => Refresh();

        Footer = new DialogFooter()
            .WithCancel(kit["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(kit["series.confirm"], () => { if (Choice is { } choice) Completed?.Invoke(this, choice); }, out _confirm, primary: true);

        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => Layout.SuggestedWidth;

    public TwoPaneDialogLayout Layout { get; }

    public IconChoiceGroup Direction { get; }

    public IconChoiceGroup Kind { get; }

    public IconChoiceGroup Unit { get; }

    public TextBox Step { get; }

    public TextBox Stop { get; }

    public bool UnitVisible => _unitRow.IsVisible;

    public IReadOnlyList<string> PreviewValues => [.. _cells.Children.OfType<Border>().Select(cell => (cell.Child as TextBlock)?.Text ?? string.Empty)];

    public bool CanConfirm => _confirm.IsEnabled;

    public SeriesChoice? Choice
    {
        get
        {
            if (!double.TryParse(Step.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var step) || !double.IsFinite(step)) return null;
            double? stop = null;
            if ((Stop.Text ?? string.Empty).Trim().Length > 0)
            {
                if (!double.TryParse(Stop.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var limit) || !double.IsFinite(limit)) return null;
                stop = limit;
            }

            return new SeriesChoice(Direction.SelectedIndex != 1, (SeriesKind)Math.Max(Kind.SelectedIndex, 0),
                (SeriesDateUnit)Math.Max(Unit.SelectedIndex, 0), step, stop);
        }
    }

    private Control BuildPreview() => new StackPanel
    {
        Width = 150,
        Spacing = _kit.Metrics.SpacingSmall,
        Children =
        {
            _kit.Caption("series.preview"),
            new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
                BorderThickness = new Thickness(1, 1, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = _cells,
            },
            _more,
        },
    };

    private void Refresh()
    {
        var kind = (SeriesKind)Math.Max(Kind.SelectedIndex, 0);
        _unitRow.IsVisible = kind == SeriesKind.Date;
        Step.IsEnabled = Stop.IsEnabled = kind != SeriesKind.AutoFill;

        var choice = Choice;
        _stepError.Text = choice is null ? _kit["series.invalidNumber"] : string.Empty;
        _stepError.IsVisible = choice is null;
        _confirm.IsEnabled = choice is not null;

        _cells.Children.Clear();
        if (choice is null) return;
        var preview = _previewer.Preview(choice, PreviewLimit);
        var line = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
        foreach (var (value, index) in preview.Values.Select((value, index) => (value, index)))
            _cells.Children.Add(new Border
            {
                Width = 66,
                Height = 20,
                Background = index == 0 ? new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)) : Brushes.White,
                BorderBrush = line,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = new TextBlock
                {
                    Text = value,
                    FontSize = _kit.Metrics.FontSizeSmall,
                    Foreground = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            });
        _more.Text = preview.Values.Count <= 1 ? _kit["series.preview.none"] : preview.Truncated ? _kit["series.preview.more"] : string.Empty;
        _more.IsVisible = _more.Text.Length > 0;
    }
}

public interface ISeriesPreviewers
{
    ISeriesPreviewer For(Worksheet sheet, Addressing.CellRange range);
}

public sealed class SeriesPreviewers(ISeriesFill series) : ISeriesPreviewers
{
    public ISeriesPreviewer For(Worksheet sheet, Addressing.CellRange range) => new SheetSeriesPreviewer(series, sheet, range);
}

public sealed class SheetSeriesPreviewer(ISeriesFill series, Worksheet sheet, Addressing.CellRange range) : ISeriesPreviewer
{
    public SeriesPreview Preview(SeriesChoice choice, int limit)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var length = choice.ByColumns ? range.RowCount : range.ColumnCount;
        var end = choice.ByColumns
            ? new Addressing.CellAddress(range.Start.Row + Math.Min(length, limit) - 1, range.Start.Column)
            : new Addressing.CellAddress(range.Start.Row, range.Start.Column + Math.Min(length, limit) - 1);
        var request = new SeriesRequest(new Addressing.CellRange(range.Start, end), choice.Kind)
        {
            ByColumns = choice.ByColumns,
            Step = choice.Step,
            Stop = choice.Stop,
            DateUnit = choice.Unit,
        };

        var start = sheet[range.Start].Value;
        if (!start.TryCoerceToNumber(out var first)) return new SeriesPreview([start.ToString()], false);

        var values = new List<string> { Shown(first, choice.Kind) };
        values.AddRange(series.Plan(sheet, request).Select(entry => entry.Value.TryCoerceToNumber(out var number) ? Shown(number, choice.Kind) : entry.Value.ToString()));
        return new SeriesPreview(values, length > limit && values.Count == limit);
    }

    private string Shown(double value, SeriesKind kind) =>
        kind == SeriesKind.Date && value is > -657435 and < 2958465
            ? DateTime.FromOADate(value).ToString("d", CultureInfo.CurrentCulture)
            : value.ToString("G10", CultureInfo.CurrentCulture);
}
