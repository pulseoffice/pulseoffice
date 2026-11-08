using PulseOffice.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PulseOffice.Typography.Catalog;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fonts;

public sealed class FontBrowserView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const int MaximumRows = 3000;

    private readonly FontPickerServices _services;
    private readonly FontDescriptions _describe;
    private readonly DesignTokens.Metrics _metrics;
    private readonly TextBox _search = new();
    private readonly ListBox _list = new();
    private readonly TextBlock _count = new();
    private readonly ComboBox _script = new();
    private readonly CheckBox _freeOnly = new();
    private readonly TextBox _sample = new();
    private readonly Slider _size = new();
    private readonly TextBlock _preview = new();
    private readonly TextBlock _previewNote = new();
    private readonly StackPanel _details = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _download = new();
    private readonly Button _cancelDownload = new();
    private readonly TextBlock _downloadStatus = new();
    private readonly List<ToggleButton> _origins = [];
    private readonly List<ToggleButton> _categories = [];
    private readonly FontScript?[] _scripts = [null, .. Enum.GetValues<FontScript>()];
    private readonly DialogFooter _footer;
    private IReadOnlyList<FontFamilyInfo> _families = [];
    private FontFamilyInfo? _selected;
    private CancellationTokenSource? _transfer;
    private readonly string _initial;

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public FontBrowserView(string initial, FontPickerServices services, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _describe = services.Descriptions;
        _metrics = DesignTokens.For(density);
        _initial = initial ?? string.Empty;
        var texts = _describe.Texts;

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*,330"),
            Height = 560,
        };

        var filters = Filters();
        var list = ListColumn();
        var detail = DetailColumn();
        Grid.SetColumn(list, 1);
        Grid.SetColumn(detail, 2);
        layout.Children.Add(filters);
        layout.Children.Add(list);
        layout.Children.Add(detail);
        Content = layout;

        _footer = new DialogFooter(density)
            .WithCancel(texts["dialog.cancel"], Cancel)
            .WithAction(texts["font.browser.confirm"], () => _ = ConfirmAsync(), primary: true);

        AttachedToVisualTree += (_, _) => Loading = LoadAsync(refresh: false);
    }

    public Task Loading { get; private set; } = Task.CompletedTask;

    public int ShownCount => (_list.ItemsSource as IReadOnlyList<FontFamilyInfo>)?.Count ?? 0;

    private async Task LoadAsync(bool refresh)
    {
        try
        {
            if (refresh) await _services.Catalog.RefreshAsync().ConfigureAwait(true);
            _families = await _services.Catalog.ListAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or InvalidDataException)
        {
        }

        Fill();
    }

    private Control Section(string title) => new TextBlock
    {
        Text = title.ToUpperInvariant(),
        FontSize = _metrics.FontSizeSmall - 1,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 0.6,
        Margin = new Thickness(0, _metrics.SpacingMedium, 0, _metrics.SpacingSmall / 2),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private Control Filters()
    {
        var texts = _describe.Texts;
        var panel = new StackPanel { Spacing = 2 };

        panel.Children.Add(Section(texts["font.browser.source"]));
        var origins = new WrapPanel();
        AddChoice(origins, _origins, texts["font.source.all"], true);
        foreach (var origin in Enum.GetValues<FontOrigin>()) AddChoice(origins, _origins, _describe.Origin(origin), false);
        panel.Children.Add(origins);

        panel.Children.Add(Section(texts["font.browser.category"]));
        var categories = new WrapPanel();
        AddChoice(categories, _categories, texts["font.category.all"], true);
        foreach (var category in Enum.GetValues<FontCategory>().Where(category => category != FontCategory.Unknown))
            AddChoice(categories, _categories, _describe.Category(category), false);
        panel.Children.Add(categories);

        panel.Children.Add(Section(texts["font.browser.script"]));
        _script.ItemsSource = _scripts.Select(script => script is { } value ? _describe.Script(value) : texts["font.script.all"]).ToList();
        _script.SelectedIndex = 0;
        Avalonia.Automation.AutomationProperties.SetName(_script, texts["font.browser.script"]);
        _script.HorizontalAlignment = HorizontalAlignment.Stretch;
        _script.FontSize = _metrics.FontSize;
        _script.SelectionChanged += (_, _) => Fill();
        panel.Children.Add(_script);

        _freeOnly.Content = texts["font.browser.freeOnly"];
        _freeOnly.FontSize = _metrics.FontSize;
        _freeOnly.Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0);
        _freeOnly.IsCheckedChanged += (_, _) => Fill();
        panel.Children.Add(_freeOnly);

        var refresh = new Button
        {
            Content = texts["font.browser.refresh"],
            FontSize = _metrics.FontSizeSmall,
            Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };
        refresh.Click += async (_, _) =>
        {
            refresh.IsEnabled = false;
            try
            {
                await (Loading = LoadAsync(refresh: true)).ConfigureAwait(true);
            }
            finally
            {
                refresh.IsEnabled = true;
            }
        };
        panel.Children.Add(refresh);

        var surface = new Border
        {
            Padding = new Thickness(_metrics.SpacingMedium, 0, _metrics.SpacingMedium, _metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
            Child = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
        };
        surface.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);
        return surface;
    }

    private void AddChoice(Panel panel, List<ToggleButton> group, string label, bool selected)
    {
        var chip = new ToggleButton
        {
            Content = label,
            IsChecked = selected,
            FontSize = _metrics.FontSizeSmall + 1,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall - 1),
            Margin = new Thickness(0, 0, _metrics.SpacingSmall, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(999),
            MinHeight = 0,
        };
        chip.Click += (_, _) =>
        {
            foreach (var other in group) other.IsChecked = ReferenceEquals(other, chip);
            Fill();
        };
        group.Add(chip);
        panel.Children.Add(chip);
    }

    private Control ListColumn()
    {
        var texts = _describe.Texts;
        _search.PlaceholderText = texts["font.picker.search"];
        _search.FontSize = _metrics.FontSize;
        if (_services.Icons.Find("ui.search") is { } glass)
            _search.InnerLeftContent = new IconView(glass, _metrics.IconSize * 0.8) { Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _search.TextChanged += (_, _) => Fill();
        Avalonia.Automation.AutomationProperties.SetName(_search, texts["font.picker.search"]);
        Avalonia.Automation.AutomationProperties.SetName(_list, texts["font.browser.family"]);

        _count.FontSize = _metrics.FontSizeSmall;
        _count.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _list.ItemTemplate = new FuncDataTemplate<FontFamilyInfo>((family, _) => family is null ? new TextBlock() : Row(family));
        _list.SelectionMode = SelectionMode.Single;
        _list.Styles.Add(new Avalonia.Styling.Style(selector => selector.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(ListBoxItem.PaddingProperty, new Thickness(_metrics.SpacingSmall, 3)),
                new Avalonia.Styling.Setter(ListBoxItem.MinHeightProperty, 0d),
                new Avalonia.Styling.Setter(ListBoxItem.CornerRadiusProperty, new CornerRadius(_metrics.RadiusTight)),
            },
        });
        _list.Background = Brushes.Transparent;
        _list.SelectionChanged += (_, _) => Select(_list.SelectedItem as FontFamilyInfo);
        _list.DoubleTapped += (_, _) => _ = ConfirmAsync();

        var frame = new Border
        {
            Child = _list,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            ClipToBounds = true,
        };
        frame.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        frame.Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = _metrics.SpacingSmall };
        Grid.SetRow(_count, 1);
        Grid.SetRow(frame, 2);
        grid.Children.Add(_search);
        grid.Children.Add(_count);
        grid.Children.Add(frame);
        return grid;
    }

    private Control Row(FontFamilyInfo family)
    {
        var name = new TextBlock
        {
            Text = family.Family,
            FontFamily = family.Origin == FontOrigin.Online ? FontFamily.Default : _services.Display.Resolve(family.Family),
            FontSize = _metrics.FontSize + 6,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var meta = new TextBlock
        {
            Text = $"{_describe.Category(family.Category)} · {_describe.Origin(family.Origin)}",
            FontSize = _metrics.FontSizeSmall,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var text = new StackPanel { Spacing = 0, Children = { name, meta } };
        var badge = new LicenseBadge(_describe.Freedom(family.License), _describe.FreedomKey(family.License), _metrics, family.License.Name);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(_metrics.SpacingSmall, _metrics.SpacingSmall / 2),
        };
        Grid.SetColumn(badge, 1);
        grid.Children.Add(text);
        grid.Children.Add(badge);
        return grid;
    }

    private Control DetailColumn()
    {
        var texts = _describe.Texts;

        _sample.Text = texts["font.browser.sampleText"];
        _sample.AcceptsReturn = true;
        _sample.TextWrapping = TextWrapping.Wrap;
        _sample.FontSize = _metrics.FontSize;
        _sample.MaxHeight = 64;
        _sample.TextChanged += (_, _) => _preview.Text = _sample.Text;

        _size.Minimum = 10;
        _size.Maximum = 72;
        _size.Value = 28;
        _size.ValueChanged += (_, _) => _preview.FontSize = _size.Value;
        Avalonia.Automation.AutomationProperties.SetName(_size, texts["font.browser.size"]);

        _preview.Text = _sample.Text;
        _preview.FontSize = _size.Value;
        _preview.TextWrapping = TextWrapping.Wrap;

        _previewNote.FontSize = _metrics.FontSizeSmall;
        _previewNote.TextWrapping = TextWrapping.Wrap;
        _previewNote.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var stage = new Border
        {
            Padding = new Thickness(_metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            Height = 160,
            ClipToBounds = true,
            Child = new ScrollViewer { Content = _preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
        };
        stage.Themed(Border.BackgroundProperty, Palette.Keys.Surface);
        stage.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        stage.BorderThickness = new Thickness(1);

        _progress.Minimum = 0;
        _progress.Maximum = 1;
        _progress.IsVisible = false;
        _download.Content = texts["font.browser.download"];
        _download.Classes.Add("dialogPrimary");
        _download.CornerRadius = new CornerRadius(_metrics.RadiusTight);
        _download.Click += async (_, _) => await DownloadSelectedAsync().ConfigureAwait(true);
        _cancelDownload.Content = texts["font.browser.cancelDownload"];
        _cancelDownload.IsVisible = false;
        _cancelDownload.CornerRadius = new CornerRadius(_metrics.RadiusTight);
        _cancelDownload.Click += (_, _) => _transfer?.Cancel();
        _downloadStatus.FontSize = _metrics.FontSizeSmall;
        _downloadStatus.TextWrapping = TextWrapping.Wrap;
        _downloadStatus.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var size = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = _metrics.SpacingSmall };
        var sizeLabel = new TextBlock { Text = texts["font.browser.size"], FontSize = _metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(_size, 1);
        size.Children.Add(sizeLabel);
        size.Children.Add(_size);

        var panel = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                stage,
                _previewNote,
                _sample,
                size,
                _details,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall, Children = { _download, _cancelDownload } },
                _progress,
                _downloadStatus,
            },
        };

        return new ScrollViewer
        {
            Content = panel,
            Margin = new Thickness(_metrics.SpacingMedium, 0, 0, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private void Fill()
    {
        var query = (_search.Text ?? string.Empty).Trim();
        var origin = Picked(_origins) is var index and > 0 ? (FontOrigin?)Enum.GetValues<FontOrigin>()[index - 1] : null;
        var categories = Enum.GetValues<FontCategory>().Where(category => category != FontCategory.Unknown).ToArray();
        var category = Picked(_categories) is var chosen and > 0 ? (FontCategory?)categories[chosen - 1] : null;
        var script = _script.SelectedIndex > 0 ? _scripts[_script.SelectedIndex] : null;
        var freeOnly = _freeOnly.IsChecked == true;

        var found = _families.Where(family =>
                family.Matches(query)
                && (origin is null || family.Origin == origin || (origin == FontOrigin.Document && family.InDocument))
                && (category is null || family.Category == category)
                && (script is null || family.Scripts.Contains(script.Value))
                && (!freeOnly || family.License.IsFree))
            .ToList();

        _list.ItemsSource = found.Take(MaximumRows).ToList();
        _count.Text = _describe.Texts.Format("font.browser.count", found.Count);

        var keep = _selected is not null ? found.FirstOrDefault(family => family.Family == _selected.Family) : null;
        keep ??= found.FirstOrDefault(family => string.Equals(family.Family, _initial, StringComparison.OrdinalIgnoreCase));
        if (keep is not null)
        {
            _list.SelectedItem = keep;
            _list.ScrollIntoView(keep);
        }
        else if (_selected is null && found.Count > 0)
        {
            _list.SelectedIndex = 0;
        }
    }

    private int Picked(List<ToggleButton> group) => group.FindIndex(choice => choice.IsChecked == true);

    private void Select(FontFamilyInfo? family)
    {
        if (family is null) return;
        _selected = family;
        var texts = _describe.Texts;

        var available = family.Origin != FontOrigin.Online;
        _preview.FontFamily = available ? _services.Display.Resolve(family.Family) : FontFamily.Default;
        _previewNote.Text = available ? string.Empty : texts["font.browser.previewNeedsDownload"];
        _download.IsVisible = !available;
        _downloadStatus.Text = string.Empty;

        _details.Children.Clear();
        _details.Children.Add(Detail(texts["font.browser.family"], new TextBlock { Text = family.Family, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }));
        _details.Children.Add(Detail(texts["font.browser.styles"], Plain(_describe.Styles(family))));
        if (family.Scripts.Count > 0)
            _details.Children.Add(Detail(texts["font.browser.scripts"], Plain(string.Join(", ", family.Scripts.Select(_describe.Script)))));

        var license = new StackPanel { Spacing = 2 };
        license.Children.Add(new LicenseBadge(_describe.Freedom(family.License), _describe.FreedomKey(family.License), _metrics));
        license.Children.Add(Plain(family.License.Name));
        if (family.License.Url is { Length: > 0 } url) license.Children.Add(Plain(url, secondary: true));
        if (family.License.Text is { Length: > 0 } description) license.Children.Add(Plain(Shorten(description), secondary: true));
        _details.Children.Add(Detail(texts["font.browser.license"], license));

        _details.Children.Add(Detail(texts["font.browser.embedding"],
            new LicenseBadge(_describe.Embedding(family.Embedding), _describe.EmbeddingKey(family.Embedding), _metrics)));
        _details.Children.Add(Detail(texts["font.browser.origin"], Plain($"{_describe.Origin(family.Origin)} · {family.SourceId}")));
        if (family.Designer is { Length: > 0 } designer) _details.Children.Add(Detail(texts["font.browser.designer"], Plain(designer)));
        if (family.Homepage is { Length: > 0 } homepage) _details.Children.Add(Detail(texts["font.browser.homepage"], Plain(homepage, secondary: true)));
    }

    private string Shorten(string text) => text.Length > 220 ? text[..220].TrimEnd() + "…" : text;

    private TextBlock Plain(string text, bool secondary = false)
    {
        var block = new TextBlock { Text = text, FontSize = _metrics.FontSize, TextWrapping = TextWrapping.Wrap };
        return secondary ? block.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary) : block;
    }

    private Control Detail(string label, Control value) => new StackPanel
    {
        Spacing = 2,
        Margin = new Thickness(0, _metrics.SpacingSmall / 2, 0, 0),
        Children =
        {
            new TextBlock { Text = label, FontSize = _metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            value,
        },
    };

    private async Task<bool> DownloadSelectedAsync()
    {
        if (_selected is not { } family) return false;
        _transfer?.Cancel();
        _transfer = new CancellationTokenSource();
        var texts = _describe.Texts;

        _progress.IsVisible = true;
        _progress.Value = 0;
        _download.IsEnabled = false;
        _cancelDownload.IsVisible = true;
        _downloadStatus.Text = texts.Format("font.picker.downloading", family.Family, 0);

        try
        {
            var progress = new Progress<FontProgress>(step =>
            {
                _progress.Value = step.Fraction;
                _downloadStatus.Text = texts.Format("font.picker.downloading", family.Family, (int)Math.Round(step.Fraction * 100));
            });
            var result = await _services.Activation.ActivateAsync(family, progress, _transfer.Token).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                _downloadStatus.Text = texts.Format("font.picker.failed", family.Family, (result.Diagnostics.FirstOrDefault() is { } diagnostic ? _services.Diagnostics.Describe(diagnostic) : string.Empty));
                return false;
            }

            _families = await _services.Catalog.ListAsync().ConfigureAwait(true);
            _selected = _families.FirstOrDefault(entry => entry.Family == family.Family) ?? family;
            Fill();
            Select(_selected);
            return true;
        }
        catch (OperationCanceledException)
        {
            _downloadStatus.Text = texts["font.browser.cancelled"];
            return false;
        }
        finally
        {
            _progress.IsVisible = false;
            _download.IsEnabled = true;
            _cancelDownload.IsVisible = false;
        }
    }

    private async Task ConfirmAsync()
    {
        if (_selected is not { } family) return;
        if (family.Origin != FontOrigin.System && family.Faces.Count > 0 && !_services.Runtime.Contains(family.Family)
            && !await DownloadSelectedAsync().ConfigureAwait(true))
            return;

        _services.Recent.Remember(family.Family);
        Completed?.Invoke(this, family.Family);
    }

    private void Cancel()
    {
        _transfer?.Cancel();
        Completed?.Invoke(this, null);
    }
}
