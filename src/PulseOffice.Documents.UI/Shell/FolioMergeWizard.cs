using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Documents.UI.Shell;

public sealed record FolioMergePlan(MergeTarget Target, IReadOnlyList<int> Rows, string? NameColumn);

public sealed record FolioMergeRecipient(int Row, string Summary);

public interface IFolioMergeLetters
{
    IReadOnlyList<string> Lines(TextDocument document, MergeData data, int row, int maximum);
}

public sealed class FolioMergeLetters(IBlockTree tree, IMergeValueFormat shown, ITextCultures cultures) : IFolioMergeLetters
{
    public IReadOnlyList<string> Lines(TextDocument document, MergeData data, int row, int maximum)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        var lines = new List<string>();
        foreach (var paragraph in tree.Paragraphs(document.Body))
        {
            if (lines.Count >= maximum) break;
            var text = new StringBuilder();
            foreach (var run in paragraph.Content.Runs)
                text.Append(run.Field == TextFieldKind.Database && run.FieldArgument is { } column
                    ? shown.Shown(data.Value(row, column), run.FieldDetail, cultures.For(run.Language, document.Language))
                    : run.Text);
            lines.Add(text.ToString().ReplaceLineEndings(" "));
        }
        return lines;
    }
}

public interface IFolioMergeWizard
{
    Task<FolioMergePlan?> AskAsync(TextDocument document, MergeData data, int startRecord, CancellationToken ct);
}

public sealed class FolioMergeWizard(IDialogService dialogs, IFolioMergeLetters letters, IMergeRecipients recipients, ITextCatalog texts, IIconCatalog icons) : IFolioMergeWizard
{
    public async Task<FolioMergePlan?> AskAsync(TextDocument document, MergeData data, int startRecord, CancellationToken ct) =>
        await dialogs.ShowAsync<FolioMergePlan>(new FolioMergeWizardView(document, data, letters, recipients, texts, icons) { StartRecord = startRecord },
            new DialogOptions { TitleKey = "command.folio.mailMerge.run.title", PreferredWidth = 720, AccessibleDescription = texts["folio.mailMerge.run.explanation"] }, ct).ConfigureAwait(true);
}

public sealed class FolioMergeWizardView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const int PreviewLines = 14;

    private const int SummaryColumns = 3;

    private readonly TextDocument _document;
    private readonly MergeData _data;
    private readonly IFolioMergeLetters _letters;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IReadOnlyList<MergeTarget> _targets = [MergeTarget.SingleDocument, MergeTarget.OdtFiles, MergeTarget.DocxFiles, MergeTarget.PdfFiles, MergeTarget.SinglePdf];
    private readonly ExclusiveChoices _targetChoices = new();
    private readonly Control[] _steps;
    private readonly IReadOnlyList<string> _stepNames;
    private readonly ContentControl _strip = new();
    private readonly StackPanel _letter = new() { Spacing = 3 };
    private readonly TextBlock _position = new();
    private readonly IMergeRecipients _recipients;
    private readonly HashSet<int> _excluded = [];
    private readonly ComboBox _filterColumn;
    private readonly ComboBox _filterOperator;
    private readonly TextBox _filterValue = new() { MinWidth = 200 };
    private readonly ListBox _list = new() { Height = 220 };
    private readonly TextBlock _count = new();
    private readonly IReadOnlyList<MergeFilterOperator> _operators = [MergeFilterOperator.Contains, MergeFilterOperator.Equals, MergeFilterOperator.NotEquals, MergeFilterOperator.Empty, MergeFilterOperator.NotEmpty];
    private readonly ComboBox _naming;
    private readonly Button _back;
    private readonly Button _next;
    private readonly Button _merge;
    private readonly DialogFooter _footer;
    private int _record;
    private int _step;

    public event EventHandler<object?>? Completed;

    public FolioMergeWizardView(TextDocument document, MergeData data, IFolioMergeLetters letters, IMergeRecipients recipients, ITextCatalog texts, IIconCatalog? icons, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(letters);
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(texts);
        _document = document;
        _data = data;
        _letters = letters;
        _texts = texts;
        _recipients = recipients;
        _metrics = DesignTokens.For(density);
        _filterColumn = new ComboBox { ItemsSource = new[] { texts["folio.mailMerge.filter.none"] }.Concat(data.Columns).ToList(), SelectedIndex = 0, MinWidth = 200 };
        _filterOperator = new ComboBox { ItemsSource = _operators.Select(item => texts["folio.mailMerge.filter." + char.ToLowerInvariant(item.ToString()[0]) + item.ToString()[1..]]).ToList(), SelectedIndex = 0, MinWidth = 200 };
        AutomationProperties.SetName(_filterColumn, texts["folio.mailMerge.filter.column"]);
        AutomationProperties.SetName(_filterOperator, texts["folio.mailMerge.filter.operator"]);
        AutomationProperties.SetName(_filterValue, texts["folio.mailMerge.filter.value"]);
        AutomationProperties.SetName(_list, texts["folio.mailMerge.recipients"]);
        _naming = new ComboBox { ItemsSource = new[] { texts["folio.mailMerge.naming.number"] }.Concat(data.Columns).ToList(), SelectedIndex = 0, MinWidth = 200 };
        AutomationProperties.SetName(_naming, texts["folio.mailMerge.naming"]);

        _stepNames = [texts["folio.mailMerge.step.letters"], texts["folio.mailMerge.step.records"], texts["folio.mailMerge.step.output"]];
        _steps = [RecordsStep(icons), RecipientsStep(), OutputStep(icons)];

        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["folio.mailMerge.back"], () => Go(_step - 1), out _back)
            .WithAction(texts["folio.mailMerge.nextStep"], () => Go(_step + 1), out _next, primary: true)
            .WithAction(texts["folio.mailMerge.merge"], Finish, out _merge, primary: true);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children = { _strip, new Panel { Children = { _steps[0], _steps[1], _steps[2] } } },
        };
        ShowRecord(0);
        Refilter();
        Go(0);
    }

    public Control? Footer => _footer;

    public double? PreferredWidth => 720;

    public int Step => _step;

    public int Record => _record;

    public int StartRecord
    {
        init => ShowRecord(value);
    }

    public IReadOnlyList<string> Letter => [.. _letter.Children.OfType<TextBlock>().Select(line => line.Text ?? string.Empty)];

    public IReadOnlyList<ToggleButton> TargetTiles => _targetChoices.Items;

    public void Go(int step)
    {
        _step = Math.Clamp(step, 0, _steps.Length - 1);
        for (var index = 0; index < _steps.Length; index++) _steps[index].IsVisible = index == _step;
        _strip.Content = new StepStrip(_stepNames, _step, _metrics, _texts["dialog.step.accessible"]);
        _back.IsVisible = _step > 0;
        _next.IsVisible = _step < _steps.Length - 1;
        _merge.IsVisible = _step == _steps.Length - 1;
        _next.IsDefault = _next.IsVisible;
        _merge.IsDefault = _merge.IsVisible;
    }

    public void ShowRecord(int record)
    {
        _record = Math.Clamp(record, 0, Math.Max(0, _data.Rows.Count - 1));
        _position.Text = _texts.Format("folio.mailMerge.record", _record + 1, _data.Rows.Count);
        _letter.Children.Clear();
        foreach (var line in _letters.Lines(_document, _data, _record, PreviewLines))
            _letter.Children.Add(new TextBlock { Text = line.Length == 0 ? " " : line, Foreground = Brushes.Black, FontSize = _metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap });
    }

    public void ChooseTarget(MergeTarget target) => _targetChoices.Select(_targets.ToList().IndexOf(target), notify: true);

    public FolioMergePlan Plan()
    {
        var naming = Math.Max(0, _naming.SelectedIndex);
        return new FolioMergePlan(_targets[Math.Max(0, _targetChoices.Selected)], Chosen, naming == 0 ? null : _data.Columns[naming - 1]);
    }

    public IReadOnlyList<int> Chosen => _recipients.Rows(_data, Filter, _excluded);

    public IReadOnlyList<int> Listed => [.. (_list.ItemsSource as IEnumerable<FolioMergeRecipient> ?? Enumerable.Empty<FolioMergeRecipient>()).Select(entry => entry.Row)];

    public void Exclude(int row, bool excluded)
    {
        if (excluded) _excluded.Add(row);
        else _excluded.Remove(row);
        Refilter();
    }

    public void ApplyFilter(MergeFilter? filter)
    {
        _filterColumn.SelectedIndex = filter is null ? 0 : Math.Max(0, _data.Columns.ToList().IndexOf(filter.Column) + 1);
        _filterOperator.SelectedIndex = filter is null ? 0 : Math.Max(0, _operators.ToList().IndexOf(filter.Operator));
        _filterValue.Text = filter?.Value ?? string.Empty;
        Refilter();
    }

    private MergeFilter? Filter =>
        _filterColumn.SelectedIndex is > 0 and var column && column - 1 < _data.Columns.Count
            ? new MergeFilter(_data.Columns[column - 1], _operators[Math.Clamp(_filterOperator.SelectedIndex, 0, _operators.Count - 1)], _filterValue.Text ?? string.Empty)
            : null;

    private void Refilter()
    {
        var filter = Filter;
        _filterOperator.IsEnabled = filter is not null;
        _filterValue.IsEnabled = filter is not null && filter.Operator is not (MergeFilterOperator.Empty or MergeFilterOperator.NotEmpty);
        _list.ItemsSource = Enumerable.Range(0, _data.Rows.Count).Where(row => _recipients.Matches(_data, row, filter))
            .Select(row => new FolioMergeRecipient(row, Summary(row))).ToList();
        Count();
    }

    private void Count()
    {
        var chosen = Chosen.Count;
        _count.Text = _texts.Format("folio.mailMerge.recipients.count", chosen, _data.Rows.Count);
        _merge.IsEnabled = chosen > 0;
    }

    private string Summary(int row) =>
        (row + 1).ToString(CultureInfo.CurrentCulture) + ". " + string.Join(" · ", _data.Columns.Take(SummaryColumns).Select(column => _data.Value(row, column) ?? string.Empty));

    private Control RecipientsStep()
    {
        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FolioMergeRecipient>((entry, _) => entry is null ? new Border() : Recipient(entry), supportsRecycling: false);
        _filterColumn.SelectionChanged += (_, _) => Refilter();
        _filterOperator.SelectionChanged += (_, _) => Refilter();
        _filterValue.TextChanged += (_, _) => Refilter();

        var filter = new DialogSection(_texts["folio.mailMerge.filter"], _metrics);
        filter.Add(_texts["folio.mailMerge.filter.column"], _filterColumn, editorWidth: 220);
        filter.Add(_texts["folio.mailMerge.filter.operator"], _filterOperator, editorWidth: 220);
        filter.Add(_texts["folio.mailMerge.filter.value"], _filterValue, editorWidth: 220);

        var all = new Button { Content = _texts["folio.mailMerge.selectAll"] };
        all.Click += (_, _) => Include(true);
        var none = new Button { Content = _texts["folio.mailMerge.selectNone"] };
        none.Click += (_, _) => Include(false);
        _count.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetLiveSetting(_count, AutomationLiveSetting.Polite);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall, Children = { all, none, _count } };

        var frame = new Border { CornerRadius = new CornerRadius(_metrics.RadiusTight), BorderThickness = new Thickness(1), Child = _list }
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        return new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = _texts["folio.mailMerge.recipients.explanation"], TextWrapping = TextWrapping.Wrap, FontSize = _metrics.FontSize }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                filter,
                frame,
                actions,
            },
        };
    }

    private Control Recipient(FolioMergeRecipient entry)
    {
        var check = new CheckBox { Content = entry.Summary, IsChecked = !_excluded.Contains(entry.Row) };
        check.IsCheckedChanged += (_, _) =>
        {
            if (check.IsChecked == true) _excluded.Remove(entry.Row);
            else _excluded.Add(entry.Row);
            Count();
        };
        return check;
    }

    private void Include(bool included)
    {
        foreach (var row in Listed)
        {
            if (included) _excluded.Remove(row);
            else _excluded.Add(row);
        }
        Refilter();
    }

    private void Finish() => Completed?.Invoke(this, Plan());

    private Control RecordsStep(IIconCatalog? icons)
    {
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        navigation.Children.Add(NavigationButton(icons, "folio.mailMerge.first", "command.folio.mailMerge.first.title", () => ShowRecord(0)));
        navigation.Children.Add(NavigationButton(icons, "folio.mailMerge.previous", "command.folio.mailMerge.previous.title", () => ShowRecord(_record - 1)));
        _position.VerticalAlignment = VerticalAlignment.Center;
        _position.Margin = new Thickness(_metrics.SpacingSmall, 0);
        _position.MinWidth = 110;
        _position.TextAlignment = TextAlignment.Center;
        AutomationProperties.SetLiveSetting(_position, AutomationLiveSetting.Polite);
        navigation.Children.Add(_position);
        navigation.Children.Add(NavigationButton(icons, "folio.mailMerge.next", "command.folio.mailMerge.next.title", () => ShowRecord(_record + 1)));
        navigation.Children.Add(NavigationButton(icons, "folio.mailMerge.last", "command.folio.mailMerge.last.title", () => ShowRecord(int.MaxValue)));

        var paper = new Border
        {
            Background = Brushes.White,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 16),
            Width = 300,
            Height = 300,
            ClipToBounds = true,
            Child = _letter,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1, Blur = 6, Color = Color.FromArgb(0x30, 0, 0, 0) }),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        AutomationProperties.SetName(paper, _texts["folio.mailMerge.letter"]);

        var left = new StackPanel { Spacing = _metrics.SpacingMedium, Width = 280 };
        left.Children.Add(new TextBlock { Text = _texts["folio.mailMerge.records.explanation"], TextWrapping = TextWrapping.Wrap, FontSize = _metrics.FontSize }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        left.Children.Add(navigation);

        var sunken = new Border { Padding = new Thickness(_metrics.SpacingLarge), CornerRadius = new CornerRadius(_metrics.RadiusTight), Child = paper }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = _metrics.SpacingLarge };
        Grid.SetColumn(sunken, 1);
        grid.Children.Add(left);
        grid.Children.Add(sunken);
        return grid;
    }

    private Control OutputStep(IIconCatalog? icons)
    {
        var list = new StackPanel { Spacing = 2 };
        foreach (var target in _targets)
        {
            var key = target.ToString().ToLowerInvariant();
            var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingMedium };
            if (icons?.Find(target is MergeTarget.SingleDocument or MergeTarget.SinglePdf ? "ui.file" : "ui.folder") is { } geometry)
                face.Children.Add(new IconView(geometry, _metrics.IconSize + 4).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary));
            face.Children.Add(new TextBlock { Text = _texts["folio.mailMerge.target." + key], FontSize = _metrics.FontSize, VerticalAlignment = VerticalAlignment.Center });
            var tile = new ToggleButton
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(_metrics.RadiusTight),
                Content = face,
            };
            tile.Classes.Add("iconChoice");
            _targetChoices.Add(tile, _texts["folio.mailMerge.target." + key]);
            list.Children.Add(tile);
        }
        _targetChoices.Select(0, notify: false);
        _targetChoices.Changed += (_, _) => _naming.IsEnabled = _targets[_targetChoices.Selected] is MergeTarget.OdtFiles or MergeTarget.DocxFiles or MergeTarget.PdfFiles;
        _naming.IsEnabled = false;

        var naming = new DialogSection(_texts["folio.mailMerge.files"], _metrics);
        naming.Add(_texts["folio.mailMerge.naming"], _naming, editorWidth: 220);

        var frame = new Border { Padding = new Thickness(4), CornerRadius = new CornerRadius(_metrics.RadiusTight + 2), BorderThickness = new Thickness(1), Child = list }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken)
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        AutomationProperties.SetName(list, _texts["folio.mailMerge.target"]);
        return new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = _texts["folio.mailMerge.run.explanation"], TextWrapping = TextWrapping.Wrap, FontSize = _metrics.FontSize }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                frame,
                naming,
            },
        };
    }

    private Button NavigationButton(IIconCatalog? icons, string icon, string labelKey, Action run)
    {
        var button = new Button
        {
            Width = _metrics.ControlHeight,
            Height = _metrics.ControlHeight,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = icons?.Find(icon) is { } geometry
                ? new IconView(geometry, _metrics.IconSize).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary)
                : new TextBlock { Text = _texts[labelKey] },
        };
        AutomationProperties.SetName(button, _texts[labelKey]);
        ToolTip.SetTip(button, _texts[labelKey]);
        button.Click += (_, _) => run();
        return button;
    }
}
