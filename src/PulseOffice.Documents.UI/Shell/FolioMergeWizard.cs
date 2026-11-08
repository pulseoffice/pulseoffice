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

public sealed record FolioMergePlan(MergeTarget Target, int From, int To, string? NameColumn);

public interface IFolioMergeLetters
{
    IReadOnlyList<string> Lines(TextDocument document, MergeData data, int row, int maximum);
}

public sealed class FolioMergeLetters(IBlockTree tree) : IFolioMergeLetters
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
                text.Append(run.Field == TextFieldKind.Database && run.FieldArgument is { } column ? data.Value(row, column) ?? string.Empty : run.Text);
            lines.Add(text.ToString().ReplaceLineEndings(" "));
        }
        return lines;
    }
}

public interface IFolioMergeWizard
{
    Task<FolioMergePlan?> AskAsync(TextDocument document, MergeData data, int startRecord, CancellationToken ct);
}

public sealed class FolioMergeWizard(IDialogService dialogs, IFolioMergeLetters letters, ITextCatalog texts, IIconCatalog icons) : IFolioMergeWizard
{
    public async Task<FolioMergePlan?> AskAsync(TextDocument document, MergeData data, int startRecord, CancellationToken ct) =>
        await dialogs.ShowAsync<FolioMergePlan>(new FolioMergeWizardView(document, data, letters, texts, icons) { StartRecord = startRecord },
            new DialogOptions { TitleKey = "command.folio.mailMerge.run.title", PreferredWidth = 720, AccessibleDescription = texts["folio.mailMerge.run.explanation"] }, ct).ConfigureAwait(true);
}

public sealed class FolioMergeWizardView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const int PreviewLines = 14;

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
    private readonly NumberBox _from;
    private readonly NumberBox _to;
    private readonly ComboBox _naming;
    private readonly Button _back;
    private readonly Button _next;
    private readonly Button _merge;
    private readonly DialogFooter _footer;
    private int _record;
    private int _step;

    public event EventHandler<object?>? Completed;

    public FolioMergeWizardView(TextDocument document, MergeData data, IFolioMergeLetters letters, ITextCatalog texts, IIconCatalog? icons, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(letters);
        ArgumentNullException.ThrowIfNull(texts);
        _document = document;
        _data = data;
        _letters = letters;
        _texts = texts;
        _metrics = DesignTokens.For(density);
        var count = Math.Max(1, data.Rows.Count);
        _from = new NumberBox(new NumberEntry(new NumericBounds(1, count, 1), true, CultureInfo.CurrentCulture), 1, _metrics, boxWidth: 72);
        _to = new NumberBox(new NumberEntry(new NumericBounds(1, count, 1), true, CultureInfo.CurrentCulture), count, _metrics, boxWidth: 72);
        _naming = new ComboBox { ItemsSource = new[] { texts["folio.mailMerge.naming.number"] }.Concat(data.Columns).ToList(), SelectedIndex = 0, MinWidth = 200 };
        AutomationProperties.SetName(_naming, texts["folio.mailMerge.naming"]);

        _stepNames = [texts["folio.mailMerge.step.records"], texts["folio.mailMerge.step.output"]];
        _steps = [RecordsStep(icons), OutputStep(icons)];

        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["folio.mailMerge.back"], () => Go(_step - 1), out _back)
            .WithAction(texts["folio.mailMerge.nextStep"], () => Go(_step + 1), out _next, primary: true)
            .WithAction(texts["folio.mailMerge.merge"], Finish, out _merge, primary: true);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children = { _strip, new Panel { Children = { _steps[0], _steps[1] } } },
        };
        ShowRecord(0);
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
        var from = (int)Math.Round(_from.Read()) - 1;
        var to = (int)Math.Round(_to.Read()) - 1;
        var naming = Math.Max(0, _naming.SelectedIndex);
        return new FolioMergePlan(_targets[Math.Max(0, _targetChoices.Selected)], Math.Min(from, to), Math.Max(from, to), naming == 0 ? null : _data.Columns[naming - 1]);
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

        var range = new DialogSection(_texts["folio.mailMerge.range"], _metrics);
        range.Add(_texts["folio.mailMerge.from"], _from, editorWidth: 120);
        range.Add(_texts["folio.mailMerge.to"], _to, editorWidth: 120);

        var left = new StackPanel { Spacing = _metrics.SpacingMedium, Width = 280 };
        left.Children.Add(new TextBlock { Text = _texts["folio.mailMerge.records.explanation"], TextWrapping = TextWrapping.Wrap, FontSize = _metrics.FontSize }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        left.Children.Add(navigation);
        left.Children.Add(range);

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
