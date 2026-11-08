using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Documents.UI.Fields;

public sealed class FolioFieldsView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly FolioFieldsModel _model;
    private readonly IIconCatalog? _icons;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IconChoiceGroup _pages;
    private readonly Control _fieldsPage;
    private readonly Control _referencesPage;
    private readonly ListBox _categories = new();
    private readonly TextBox _fieldSearch = new();
    private readonly ListBox _fields = new();
    private readonly Border _optionsHost = new();
    private readonly TextBlock _fieldPreview = new();
    private readonly ListBox _kinds = new();
    private readonly TextBox _targetSearch = new();
    private readonly ListBox _targets = new();
    private readonly ListBox _formats = new();
    private readonly TextBlock _referencePreview = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _targetsEmpty = new();
    private readonly DialogFooter _footer;
    private readonly Button _apply;
    private IReadOnlyList<FolioFieldChoice> _shownFields = [];
    private IReadOnlyList<FolioReferenceKind> _shownKinds = [];
    private IReadOnlyList<ReferenceTarget> _shownTargets = [];
    private IReadOnlyList<FolioReferenceFormat> _shownFormats = [];
    private FormPromptView? _options;
    private bool _syncing;

    public event EventHandler<object?>? Completed;

    public FolioFieldsView(FolioFieldsModel model, IIconCatalog? icons, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _icons = icons;
        _metrics = DesignTokens.For(density);
        var texts = model.Texts;

        _pages = new IconChoiceGroup(
            [new IconChoice("fields", "folio.insert.fieldDialog", texts["folio.fields.page.fields"]), new IconChoice("references", "folio.insert.crossReference", texts["folio.fields.page.references"])],
            icons, _metrics, texts["folio.fields.page"], model.Start.Page == FolioFieldsPage.Fields ? 0 : 1, showLabels: true);
        _pages.IsVisible = !model.IsEditing;

        _fieldsPage = BuildFieldsPage();
        _referencesPage = BuildReferencesPage();

        _status.FontSize = _metrics.FontSizeSmall;
        _status.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _status.VerticalAlignment = VerticalAlignment.Center;

        _footer = new DialogFooter(density)
            .WithCancel(texts[model.IsEditing ? "prompt.cancel" : "folio.fields.close"], () => Completed?.Invoke(this, null))
            .WithAction(texts[model.IsEditing ? "folio.fields.apply" : "folio.fields.insert"], Apply, out _apply, primary: true);

        _pages.SelectionChanged += (_, _) => ShowPage();
        KeyDown += OnKey;

        FillCategories();
        FillKinds();
        ShowPage();

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                _pages,
                new Panel { Children = { _fieldsPage, _referencesPage } },
                _status,
            },
        };

        Loaded += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => (Page == FolioFieldsPage.Fields ? _fields : (Control)_targets).Focus(NavigationMethod.Tab), Avalonia.Threading.DispatcherPriority.Input);
    }

    public Control? Footer => _footer;

    public double? PreferredWidth => 860;

    public FolioFieldsPage Page => _pages.SelectedIndex == 1 ? FolioFieldsPage.References : FolioFieldsPage.Fields;

    public ListBox Categories => _categories;

    public ListBox FieldList => _fields;

    public TextBox FieldSearch => _fieldSearch;

    public FormPromptView? Options => _options;

    public string FieldPreview => _fieldPreview.Text ?? string.Empty;

    public ListBox Kinds => _kinds;

    public ListBox TargetList => _targets;

    public TextBox TargetSearch => _targetSearch;

    public ListBox FormatList => _formats;

    public string ReferencePreview => _referencePreview.Text ?? string.Empty;

    public string Status => _status.Text ?? string.Empty;

    public Button ApplyButton => _apply;

    public FolioFieldChoice? SelectedField => _fields.SelectedIndex >= 0 && _fields.SelectedIndex < _shownFields.Count ? _shownFields[_fields.SelectedIndex] : null;

    public ReferenceTarget? SelectedTarget => _targets.SelectedIndex >= 0 && _targets.SelectedIndex < _shownTargets.Count ? _shownTargets[_targets.SelectedIndex] : null;

    public FolioReferenceFormat? SelectedFormat => _formats.SelectedIndex >= 0 && _formats.SelectedIndex < _shownFormats.Count ? _shownFormats[_formats.SelectedIndex] : null;

    public void ShowPage(FolioFieldsPage page) => _pages.SelectedIndex = page == FolioFieldsPage.Fields ? 0 : 1;

    public void Apply()
    {
        var applied = Page == FolioFieldsPage.Fields ? ApplyField() : ApplyReference();
        if (applied is null) return;
        if (_model.IsEditing)
        {
            Completed?.Invoke(this, true);
            return;
        }
        _status.Text = _model.Texts.Format("folio.fields.inserted", applied);
        if (Page == FolioFieldsPage.References) FillTargets();
    }

    private string? ApplyField()
    {
        if (SelectedField is not { } choice || _options is null) return null;
        return _model.Apply(choice, _options.Values) ? _model.Title(choice) : null;
    }

    private string? ApplyReference()
    {
        if (SelectedTarget is not { } target || SelectedFormat is not { } format) return null;
        return _model.ApplyReference(target, format.Format) ? _model.Flat(target.Title) : null;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None) return;
        if (e.Source is TextBox { AcceptsReturn: true }) return;
        if (!_apply.IsEnabled) return;
        Apply();
        e.Handled = true;
    }

    private void ShowPage()
    {
        _fieldsPage.IsVisible = Page == FolioFieldsPage.Fields;
        _referencesPage.IsVisible = Page == FolioFieldsPage.References;
        _status.Text = string.Empty;
        RefreshApply();
    }

    private Control BuildFieldsPage()
    {
        var texts = _model.Texts;
        _fieldSearch.PlaceholderText = texts["folio.fields.search"];
        AutomationProperties.SetName(_fieldSearch, texts["folio.fields.search"]);
        AutomationProperties.SetName(_categories, texts["folio.fields.categories"]);
        AutomationProperties.SetName(_fields, texts["folio.fields.list"]);
        _categories.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            FillFields(null);
        };
        _fieldSearch.TextChanged += (_, _) => FillFields(SelectedField);
        _fieldSearch.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Down || _fields.ItemCount == 0) return;
            _fields.SelectedIndex = Math.Max(0, _fields.SelectedIndex);
            _fields.Focus(NavigationMethod.Directional);
            e.Handled = true;
        };
        _fields.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            ShowOptions();
        };
        _fields.DoubleTapped += (_, _) =>
        {
            if (_apply.IsEnabled) Apply();
        };

        var fieldsColumn = Column(texts["folio.fields.list"], new DockPanel
        {
            LastChildFill = true,
            Children = { Docked(_fieldSearch, Dock.Top), Framed(_fields) },
        });
        _fieldSearch.Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall);

        var right = new DockPanel { LastChildFill = true };
        right.Children.Add(Docked(PreviewBox(_fieldPreview, texts["folio.fields.preview"]), Dock.Bottom));
        right.Children.Add(new ScrollViewer { Content = _optionsHost, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });

        return ThreeColumns(Column(texts["folio.fields.categories"], Framed(_categories)), fieldsColumn, Column(texts["folio.fields.options"], right));
    }

    private Control BuildReferencesPage()
    {
        var texts = _model.Texts;
        _targetSearch.PlaceholderText = texts["folio.fields.search"];
        _targetSearch.Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall);
        AutomationProperties.SetName(_targetSearch, texts["folio.fields.search"]);
        AutomationProperties.SetName(_kinds, texts["folio.reference.kind"]);
        AutomationProperties.SetName(_targets, texts["folio.reference.target"]);
        AutomationProperties.SetName(_formats, texts["folio.reference.format"]);
        _targetsEmpty.Text = texts["folio.reference.none"];
        _targetsEmpty.TextWrapping = TextWrapping.Wrap;
        _targetsEmpty.Margin = new Thickness(_metrics.SpacingMedium);
        _targetsEmpty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _kinds.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            FillTargets();
            FillFormats(null);
        };
        _targetSearch.TextChanged += (_, _) => FillTargets();
        _targetSearch.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Down || _targets.ItemCount == 0) return;
            _targets.SelectedIndex = Math.Max(0, _targets.SelectedIndex);
            _targets.Focus(NavigationMethod.Directional);
            e.Handled = true;
        };
        _targets.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            FillFormats(SelectedFormat?.Format);
        };
        _formats.SelectionChanged += (_, _) => RefreshReferencePreview();
        _targets.DoubleTapped += (_, _) =>
        {
            if (_apply.IsEnabled) Apply();
        };
        _formats.DoubleTapped += (_, _) =>
        {
            if (_apply.IsEnabled) Apply();
        };

        var targetsColumn = Column(texts["folio.reference.target"], new DockPanel
        {
            LastChildFill = true,
            Children = { Docked(_targetSearch, Dock.Top), new Panel { Children = { Framed(_targets), _targetsEmpty } } },
        });
        var formatsColumn = new DockPanel { LastChildFill = true };
        formatsColumn.Children.Add(Docked(PreviewBox(_referencePreview, texts["folio.fields.preview"]), Dock.Bottom));
        formatsColumn.Children.Add(Framed(_formats));

        return ThreeColumns(Column(texts["folio.reference.kind"], Framed(_kinds)), targetsColumn, Column(texts["folio.reference.format"], formatsColumn));
    }

    private void FillCategories()
    {
        _syncing = true;
        _categories.Items.Clear();
        foreach (var category in _model.Categories) _categories.Items.Add(Item(category.Icon, _model.CategoryTitle(category), null));
        var start = _model.Start.Choice;
        var index = start is null ? 0 : _model.Categories.ToList().FindIndex(category => category.Key == start.Category);
        _categories.SelectedIndex = Math.Max(0, index);
        _syncing = false;
        FillFields(start);
    }

    private void FillFields(FolioFieldChoice? keep)
    {
        var category = _model.Categories[Math.Max(0, _categories.SelectedIndex)];
        _shownFields = _model.Fields(category, _fieldSearch.Text ?? string.Empty);
        _syncing = true;
        _fields.Items.Clear();
        foreach (var choice in _shownFields) _fields.Items.Add(Item(null, _model.Title(choice), null));
        var index = keep is null ? -1 : _shownFields.ToList().IndexOf(keep);
        _fields.SelectedIndex = _shownFields.Count == 0 ? -1 : Math.Max(0, index);
        _syncing = false;
        ShowOptions();
    }

    private void ShowOptions()
    {
        if (_options is not null) _options.ValuesChanged -= OnOptionsChanged;
        _options = null;
        _optionsHost.Child = null;
        if (SelectedField is { } choice)
        {
            var strategy = _model.OptionsFor(choice);
            _options = new FormPromptView(_model.Texts[strategy.ExplanationKey(choice)], strategy.Fields(choice, _model.Seed(choice), _model.Context), _model.Texts)
            {
                ShowButtons = false,
            };
            _options.ValuesChanged += OnOptionsChanged;
            _optionsHost.Child = _options;
        }
        RefreshFieldPreview();
    }

    private void OnOptionsChanged(object? sender, EventArgs e) => RefreshFieldPreview();

    private void RefreshFieldPreview()
    {
        _fieldPreview.Text = SelectedField is { } choice && _options is not null ? _model.Preview(choice, _options.Values) : string.Empty;
        RefreshApply();
    }

    private void FillKinds()
    {
        _shownKinds = _model.ReferenceKinds();
        _syncing = true;
        _kinds.Items.Clear();
        foreach (var kind in _shownKinds) _kinds.Items.Add(Item(kind.Icon, kind.Title, null));
        var start = _model.Start.ReferenceKind;
        var index = start is null ? 0 : _shownKinds.ToList().FindIndex(kind => kind.Kind == start.Kind && kind.Sequence == start.Sequence);
        _kinds.SelectedIndex = Math.Max(0, index);
        _syncing = false;
        FillTargets();
        FillFormats(_model.Start.Format);
    }

    private void FillTargets()
    {
        if (_kinds.SelectedIndex < 0 || _kinds.SelectedIndex >= _shownKinds.Count) return;
        var keep = SelectedTarget ?? _model.Start.Target;
        _shownTargets = _model.Targets(_shownKinds[_kinds.SelectedIndex], _targetSearch.Text ?? string.Empty);
        _syncing = true;
        _targets.Items.Clear();
        foreach (var target in _shownTargets) _targets.Items.Add(Item(null, _model.Flat(target.Title), null));
        var index = keep is null ? -1 : _shownTargets.ToList().FindIndex(target => target.Kind == keep.Kind && target.Key == keep.Key);
        _targets.SelectedIndex = _shownTargets.Count == 0 ? -1 : Math.Max(0, index);
        _syncing = false;
        _targetsEmpty.IsVisible = _shownTargets.Count == 0;
        _targets.IsVisible = _shownTargets.Count > 0;
        FillFormats(SelectedFormat?.Format);
    }

    private void FillFormats(string? keep)
    {
        if (_kinds.SelectedIndex < 0 || _kinds.SelectedIndex >= _shownKinds.Count) return;
        var kind = _shownKinds[_kinds.SelectedIndex];
        _shownFormats = _model.Formats(kind);
        var wanted = keep ?? _model.DefaultFormat(kind);
        _syncing = true;
        _formats.Items.Clear();
        foreach (var format in _shownFormats)
            _formats.Items.Add(Item(null, format.Title, SelectedTarget is { } target ? _model.ReferencePreview(target, format.Format) : null));
        var index = _shownFormats.ToList().FindIndex(format => format.Format == wanted);
        _formats.SelectedIndex = _shownFormats.Count == 0 ? -1 : Math.Max(0, index);
        _syncing = false;
        RefreshReferencePreview();
    }

    private void RefreshReferencePreview()
    {
        _referencePreview.Text = SelectedTarget is { } target && SelectedFormat is { } format ? _model.ReferencePreview(target, format.Format) : string.Empty;
        RefreshApply();
    }

    private void RefreshApply()
    {
        if (_apply is null) return;
        _apply.IsEnabled = Page == FolioFieldsPage.Fields
            ? SelectedField is { } choice && _options is not null && _model.Compose(choice, _options.Values) is not null
            : SelectedTarget is not null && SelectedFormat is not null;
    }

    private ListBoxItem Item(string? icon, string title, string? example)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        if (icon is not null && _icons?.Find(icon) is { } geometry)
            row.Children.Add(new IconView(geometry, _metrics.IconSize).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary));
        var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, FontSize = _metrics.FontSize, TextTrimming = TextTrimming.CharacterEllipsis });
        if (example is { Length: > 0 })
        {
            label.Children.Add(new TextBlock { Text = example, FontSize = _metrics.FontSizeSmall, TextTrimming = TextTrimming.CharacterEllipsis }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        }
        row.Children.Add(label);
        var item = new ListBoxItem { Content = row, Padding = new Thickness(_metrics.SpacingSmall, 3) };
        AutomationProperties.SetName(item, example is { Length: > 0 } ? title + ", " + example : title);
        return item;
    }

    private Control PreviewBox(TextBlock text, string caption)
    {
        text.FontSize = _metrics.FontSize + 1;
        text.Foreground = Brushes.Black;
        text.TextWrapping = TextWrapping.Wrap;
        text.MaxHeight = 60;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        AutomationProperties.SetName(text, caption);
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
        var paper = new Border
        {
            Background = Brushes.White,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            MinHeight = 40,
            Child = text,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        return new StackPanel
        {
            Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0),
            Spacing = 4,
            Children = { Caption(caption), paper },
        };
    }

    private Control Column(string title, Control body)
    {
        var dock = new DockPanel { LastChildFill = true };
        dock.Children.Add(Docked(Caption(title), Dock.Top));
        dock.Children.Add(body);
        return dock;
    }

    private TextBlock Caption(string text)
    {
        var caption = new TextBlock
        {
            Text = text.ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.4,
            Margin = new Thickness(0, 0, 0, 6),
        };
        caption.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        return caption;
    }

    private Border Framed(Control content) => new Border
    {
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(_metrics.RadiusTight),
        Padding = new Thickness(2),
        Child = content,
    }.Themed(Border.BorderBrushProperty, Palette.Keys.FieldBorder)
     .Themed(Border.BackgroundProperty, Palette.Keys.Surface);

    private Grid ThreeColumns(Control first, Control second, Control third)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180,230,*"),
            ColumnSpacing = _metrics.SpacingLarge,
            Height = 340,
        };
        Grid.SetColumn(second, 1);
        Grid.SetColumn(third, 2);
        grid.Children.Add(first);
        grid.Children.Add(second);
        grid.Children.Add(third);
        return grid;
    }

    private Control Docked(Control control, Dock dock)
    {
        DockPanel.SetDock(control, dock);
        return control;
    }
}
