using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;
using PulseOffice.Text.Spelling.Dictionaries;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dictionaries;

public sealed record DictionaryManagerServices(IDictionaryManager Manager, IIconCatalog Icons, ITextCatalog Texts, IDiagnosticTexts Diagnostics);

public sealed class DictionaryManagerView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const double ListWidth = 300;

    private readonly DictionaryManagerServices _services;
    private readonly DialogChrome _chrome;
    private readonly DictionaryDescriptions _describe;
    private readonly TextBox _search;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly StackPanel _detail = new();
    private readonly TextBlock _note;
    private readonly Button _updateAll;
    private readonly CancellationTokenSource _closing = new();
    private CancellationTokenSource? _operation;
    private DictionaryPart? _busyPart;
    private DictionaryProgress? _progress;
    private string? _message;
    private bool _messageIsError;
    private IReadOnlyList<DictionaryLanguage> _languages = [];
    private string? _selected;
    private bool _loading = true;
    private readonly Dictionary<(string Code, DictionaryPart Part), string> _chosenSources = [];

    public DictionaryManagerView(DictionaryManagerServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _chrome = new DialogChrome(services.Icons, DesignTokens.For(InterfaceDensity.Compact));
        _describe = new DictionaryDescriptions(services.Texts);

        _search = new TextBox
        {
            PlaceholderText = Texts["dictionaries.search"],
            Padding = new Thickness(Metrics.SpacingMedium, Metrics.SpacingSmall),
            VerticalContentAlignment = VerticalAlignment.Center,
            InnerLeftContent = new Border { Padding = new Thickness(Metrics.SpacingSmall, 0, 0, 0), Child = _chrome.Icon("ui.search", 14) },
        };
        _search.TextChanged += (_, _) => ShowList();
        Avalonia.Automation.AutomationProperties.SetName(_search, Texts["dictionaries.search"]);

        _note = _chrome.Description(string.Empty);
        _note.IsVisible = false;

        Content = Layout();

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.close"], () => Completed?.Invoke(this, null))
            .WithAction(Texts["dictionaries.updateAll"], () => _ = UpdateAllAsync(), out _updateAll, primary: true);
        _updateAll.IsEnabled = false;

        AttachedToVisualTree += (_, _) => _ = LoadAsync(false);
        DetachedFromVisualTree += (_, _) =>
        {
            _operation?.Cancel();
            _closing.Cancel();
        };

        ShowList();
        ShowDetail();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public IReadOnlyList<DictionaryLanguage> Languages => _languages;

    public string? Selected => _selected;

    public bool IsBusy => _operation is not null;

    private ITextCatalog Texts => _services.Texts;

    private DesignTokens.Metrics Metrics => _chrome.Metrics;

    public void Select(string code)
    {
        _selected = code;
        ShowList();
        ShowDetail();
    }

    public void ChooseSource(DictionaryPart part, string sourceId)
    {
        if (Current is not { } language) return;
        _chosenSources[(language.Code, part)] = sourceId;
        ShowDetail();
    }

    public DictionaryPackage? Chosen(DictionaryPartState state) =>
        Current is { } language
        && _chosenSources.TryGetValue((language.Code, state.Part), out var sourceId)
        && state.Alternatives.FirstOrDefault(candidate => candidate.SourceId == sourceId) is { } chosen
            ? chosen
            : state.Package;

    public Task InstallAsync(DictionaryPart part) =>
        Current?[part] is { } state && Chosen(state) is { } package ? RunAsync(part, ct => _services.Manager.InstallAsync(package, Reporter(), ct), "dictionaries.done.installed") : Task.CompletedTask;

    public Task RemoveAsync(DictionaryPart part) =>
        Current is { } language
            ? RunAsync(part, _ => Task.FromResult(_services.Manager.Remove(language[part].Installed?.Code ?? language.Code, part)), "dictionaries.done.removed")
            : Task.CompletedTask;

    public void Cancel() => _operation?.Cancel();

    private DictionaryLanguage? Current => _languages.FirstOrDefault(language => language.Code == _selected);

    private Control Layout()
    {
        var refresh = new Button
        {
            Content = _chrome.Icon("ui.refresh", 16),
            Padding = new Thickness(6),
            Background = Brushes.Transparent,
            BorderThickness = default,
            VerticalAlignment = VerticalAlignment.Center,
        };
        refresh.Classes.Add("ribbon");
        ToolTip.SetTip(refresh, Texts["dictionaries.refresh"]);
        Avalonia.Automation.AutomationProperties.SetName(refresh, Texts["dictionaries.refresh"]);
        refresh.Click += (_, _) => _ = LoadAsync(true);

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, Metrics.SpacingSmall) };
        top.Children.Add(_search);
        Grid.SetColumn(refresh, 1);
        refresh.Margin = new Thickness(Metrics.SpacingSmall, 0, 0, 0);
        top.Children.Add(refresh);

        var left = new DockPanel { Width = ListWidth };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(_note, Dock.Bottom);
        _note.Margin = new Thickness(0, Metrics.SpacingSmall, 0, 0);
        left.Children.Add(top);
        left.Children.Add(_note);
        left.Children.Add(_chrome.Card(new ScrollViewer { Content = _list, Padding = new Thickness(4) }));

        var divider = new Border { Width = 1, Margin = new Thickness(Metrics.SpacingLarge, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);

        var right = new ScrollViewer { Content = _detail, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), MinHeight = 500 };
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(right, 2);
        layout.Children.Add(left);
        layout.Children.Add(divider);
        layout.Children.Add(right);
        return layout;
    }

    private async Task LoadAsync(bool refresh)
    {
        _loading = true;
        ShowList();

        DictionaryCatalog catalog;
        try
        {
            catalog = await _services.Manager.CatalogAsync(refresh, _closing.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _languages = catalog.Languages;
        _loading = false;
        _selected ??= _languages.FirstOrDefault(language => language.HasInstalled)?.Code ?? _languages.FirstOrDefault()?.Code;
        _note.Text = catalog.Offline ? Texts["dictionaries.offline"] : string.Empty;
        _note.IsVisible = catalog.Offline;
        _updateAll.IsEnabled = _operation is null && _languages.Any(language => language.HasUpdate);
        ShowList();
        ShowDetail();
    }

    private void ShowList()
    {
        _list.Children.Clear();

        if (_loading && _languages.Count == 0)
        {
            _list.Children.Add(Padded(_chrome.Description(Texts["dictionaries.loading"])));
            return;
        }

        var filter = _search.Text?.Trim() ?? string.Empty;
        var shown = _languages
            .GroupBy(language => language.Base, StringComparer.OrdinalIgnoreCase)
            .Where(group => filter.Length == 0 || group.Any(language =>
                language.Code.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || _describe.LanguageName(language.Code).Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(group => group.Any(language => language.HasInstalled))
            .ThenBy(group => _describe.LanguageName(group.Key), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (shown.Count == 0)
        {
            _list.Children.Add(Padded(_chrome.Description(Texts["dictionaries.empty"])));
            return;
        }

        foreach (var group in shown) _list.Children.Add(Row(group.Key, [.. group]));
    }

    private Control Padded(Control child)
    {
        child.Margin = new Thickness(Metrics.SpacingMedium);
        return child;
    }

    private IReadOnlyList<DictionaryLanguage> Variants(string root) =>
        [.. _languages
            .Where(language => string.Equals(language.Base, root, StringComparison.OrdinalIgnoreCase))
            .OrderBy(language => language.Code.Length)
            .ThenBy(language => _describe.LanguageName(language.Code), StringComparer.CurrentCultureIgnoreCase)];

    private DictionaryStatus Best(IEnumerable<DictionaryPartState> states) =>
        states.Select(state => state.Status).OrderBy(Rank).FirstOrDefault();

    private int Rank(DictionaryStatus status) => status switch
    {
        DictionaryStatus.UpdateAvailable => 0,
        DictionaryStatus.Installed => 1,
        DictionaryStatus.OnSystem => 2,
        DictionaryStatus.Available => 3,
        _ => 4,
    };

    private Button Row(string root, IReadOnlyList<DictionaryLanguage> variants)
    {
        var name = new TextBlock { Text = _describe.LanguageName(root), FontWeight = FontWeight.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
        var marks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var part in variants[0].Parts.Select(state => state.Part))
        {
            var status = Best(variants.Select(language => language[part]));
            var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center }
                .Themed(Border.BackgroundProperty, _describe.StatusColour(status));
            ToolTip.SetTip(dot, $"{_describe.Part(part)}: {_describe.Status(status)}");
            marks.Children.Add(dot);
        }

        var code = new TextBlock
        {
            Text = variants.Count > 1 ? Texts.Format("dictionaries.variants.count", root, variants.Count) : variants[0].Code,
            FontSize = Metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = Metrics.SpacingSmall };
        grid.Children.Add(name);
        Grid.SetColumn(code, 1);
        Grid.SetColumn(marks, 2);
        grid.Children.Add(code);
        grid.Children.Add(marks);

        var button = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Metrics.SpacingSmall, 6),
            BorderThickness = default,
            Tag = root,
        };
        button.Classes.Add("ribbon");
        button.Classes.Set("checked", string.Equals(Current?.Base, root, StringComparison.OrdinalIgnoreCase));
        button.Click += (_, _) => Select((variants.FirstOrDefault(language => language.Code == root) ?? variants[0]).Code);
        return button;
    }

    private Control VariantPicker(DictionaryLanguage current)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var variant in Variants(current.Base))
        {
            var button = new Button
            {
                Content = _describe.LanguageName(variant.Code),
                Padding = new Thickness(Metrics.SpacingMedium, 4),
                Margin = new Thickness(0, 0, Metrics.SpacingSmall, Metrics.SpacingSmall),
                CornerRadius = new CornerRadius(Metrics.RadiusSoft),
                Tag = $"variant:{variant.Code}",
            };
            button.Classes.Add("ribbon");
            button.Classes.Set("checked", variant.Code == current.Code);
            ToolTip.SetTip(button, variant.Code);
            button.Click += (_, _) => Select(variant.Code);
            row.Children.Add(button);
        }

        return row;
    }

    private void ShowDetail()
    {
        _detail.Children.Clear();

        if (Current is not { } language)
        {
            _detail.Children.Add(_chrome.Description(_loading ? Texts["dictionaries.loading"] : Texts["dictionaries.pick"]));
            return;
        }

        _detail.Spacing = Metrics.SpacingMedium;
        _detail.Children.Add(new TextBlock
        {
            Text = _describe.LanguageName(language.Base),
            FontSize = Metrics.FontSizeHeading,
            FontWeight = FontWeight.SemiBold,
        });
        if (Variants(language.Base).Count > 1)
        {
            _detail.Children.Add(_chrome.Description(Texts["dictionaries.variants"]));
            _detail.Children.Add(VariantPicker(language));
        }
        _detail.Children.Add(_chrome.Description(Texts.Format("dictionaries.code", $"{_describe.LanguageName(language.Code)} ({language.Code})")));

        foreach (var state in language.Parts) _detail.Children.Add(PartCard(state));

        if (_message is { Length: > 0 })
            _detail.Children.Add(new TextBlock { Text = _message, TextWrapping = TextWrapping.Wrap, FontSize = Metrics.FontSizeSmall }
                .Themed(TextBlock.ForegroundProperty, _messageIsError ? Palette.Keys.Danger : Palette.Keys.Success));
    }

    private Control PartCard(DictionaryPartState state)
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Metrics.SpacingSmall };
        title.Children.Add(_chrome.Icon(_describe.PartIcon(state.Part), 18));
        title.Children.Add(new TextBlock { Text = _describe.Part(state.Part), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        title.Children.Add(new LicenseBadge(_describe.Status(state.Status), _describe.StatusColour(state.Status), Metrics));

        var chosen = Chosen(state);
        var switching = chosen is not null && state.Installed is not null && !SameOrigin(chosen, state.Installed);
        var license = switching ? chosen!.License : state.Installed?.License ?? chosen?.License;
        if (license is not null)
            title.Children.Add(new LicenseBadge(_describe.Freedom(license), _describe.FreedomColour(license), Metrics, license.Name));

        var body = new StackPanel { Spacing = Metrics.SpacingSmall };
        body.Children.Add(title);
        body.Children.Add(_chrome.Description(_describe.PartDescription(state.Part)));

        if (license is not null)
            body.Children.Add(_chrome.Description(Texts.Format("dictionaries.licenseLine", license.Name)));

        var origin = switching ? chosen!.Origin : state.Installed?.Origin ?? chosen?.Origin;
        if (origin is not null) body.Children.Add(_chrome.Description(Texts.Format("dictionaries.origin", origin)));

        if (state.Inherited && (switching ? chosen?.Code : state.Installed?.Code ?? chosen?.Code) is { } wider)
            body.Children.Add(_chrome.Description(Texts.Format("dictionaries.inherited", _describe.LanguageName(wider), wider)));

        if (state.Alternatives.Count > 1) body.Children.Add(SourcePicker(state, chosen));

        if (state.Status == DictionaryStatus.Unavailable || (state.Status == DictionaryStatus.OnSystem && state.Package is null))
            body.Children.Add(_chrome.Description(Texts[UnavailableKey(state)]));

        body.Children.Add(_busyPart == state.Part ? Progress() : Actions(state));

        return _chrome.Card(new Border { Padding = new Thickness(Metrics.SpacingLarge), Child = body });
    }

    private string UnavailableKey(DictionaryPartState state) =>
        state.Status == DictionaryStatus.OnSystem
            ? "dictionaries.onSystem.note"
            : _services.Manager.SupportedParts.Contains(state.Part)
                ? "dictionaries.unavailable.language"
                : $"dictionaries.unavailable.{state.Part.ToString().ToLowerInvariant()}";

    private bool SameOrigin(DictionaryPackage package, InstalledDictionary installed) =>
        package.SourceId == installed.SourceId && string.Equals(package.Code, installed.Code, StringComparison.OrdinalIgnoreCase);

    private Control SourcePicker(DictionaryPartState state, DictionaryPackage? chosen)
    {
        var picker = new ComboBox
        {
            ItemsSource = state.Alternatives,
            SelectedItem = chosen,
            MinWidth = 260,
            IsEnabled = _operation is null,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<DictionaryPackage>((package, _) => new TextBlock
            {
                Text = SourceLabel(state, package),
                TextTrimming = TextTrimming.CharacterEllipsis,
            }),
            Tag = "dictionaries.source",
        };
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is DictionaryPackage package && package.SourceId != Chosen(state)?.SourceId)
                ChooseSource(state.Part, package.SourceId);
        };

        var label = _chrome.Description(Texts.Format("dictionaries.sources", state.Alternatives.Count));
        Avalonia.Automation.AutomationProperties.SetName(picker, label.Text ?? string.Empty);
        label.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Metrics.SpacingSmall };
        row.Children.Add(label);
        row.Children.Add(picker);
        return row;
    }

    private string SourceLabel(DictionaryPartState state, DictionaryPackage? package) =>
        package is null
            ? string.Empty
            : Texts.Format(
                package == state.Alternatives[0] ? "dictionaries.source.recommended" : "dictionaries.source",
                package.Origin,
                package.License.Name);

    private Control Actions(DictionaryPartState state)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Metrics.SpacingSmall };
        var idle = _operation is null;
        var chosen = Chosen(state);

        if (state.Installed is not null && chosen is not null && !SameOrigin(chosen, state.Installed))
            row.Children.Add(Action("dictionaries.switch", primary: true, idle, () => _ = InstallAsync(state.Part)));

        if (state.Status is DictionaryStatus.Available or DictionaryStatus.OnSystem && chosen is not null)
            row.Children.Add(Action("dictionaries.install", primary: true, idle, () => _ = InstallAsync(state.Part)));
        if (state.Status == DictionaryStatus.UpdateAvailable && chosen is not null && state.Installed is not null && SameOrigin(chosen, state.Installed))
            row.Children.Add(Action("dictionaries.update", primary: true, idle, () => _ = InstallAsync(state.Part)));
        if (state.Status is DictionaryStatus.Installed or DictionaryStatus.UpdateAvailable)
            row.Children.Add(Action("dictionaries.remove", primary: false, idle, () => _ = RemoveAsync(state.Part)));

        return row;
    }

    private Button Action(string key, bool primary, bool enabled, Action run)
    {
        var button = new Button
        {
            Content = Texts[key],
            Padding = new Thickness(Metrics.SpacingMedium, Metrics.SpacingSmall),
            CornerRadius = new CornerRadius(Metrics.RadiusTight),
            IsEnabled = enabled,
            Tag = key,
        };
        if (primary) button.Classes.Add("dialogPrimary");
        button.Click += (_, _) => run();
        return button;
    }

    private Control Progress()
    {
        var bar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, MinWidth = 220, VerticalAlignment = VerticalAlignment.Center };
        if (_progress is { Total: > 0 } progress) bar.Value = (double)progress.Completed / progress.Total;
        else bar.IsIndeterminate = true;

        var label = _chrome.Description(_progress is { } current ? Texts.Format("dictionaries.progress", _describe.Size(current.Bytes)) : Texts["dictionaries.loading"]);
        var cancel = Action("dictionaries.cancel", primary: false, enabled: true, Cancel);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Metrics.SpacingMedium };
        row.Children.Add(bar);
        row.Children.Add(label);
        row.Children.Add(cancel);
        return row;
    }

    private IProgress<DictionaryProgress> Reporter() => new Progress<DictionaryProgress>(value =>
    {
        _progress = value;
        ShowDetail();
    });

    private async Task RunAsync(DictionaryPart part, Func<CancellationToken, Task<OperationResult<bool>>> work, string successKey)
    {
        if (_operation is not null) return;

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        _operation = operation;
        _busyPart = part;
        _progress = null;
        _message = null;
        _updateAll.IsEnabled = false;
        ShowDetail();

        try
        {
            var result = await work(operation.Token).ConfigureAwait(true);
            _messageIsError = !result.Succeeded;
            _message = result.Succeeded
                ? Texts[successKey]
                : result.Diagnostics.FirstOrDefault() is { } problem ? _services.Diagnostics.Describe(problem) : Texts["dictionaries.failed"];
        }
        catch (OperationCanceledException)
        {
            _messageIsError = false;
            _message = Texts["dictionaries.cancelled"];
        }
        finally
        {
            _operation = null;
            _busyPart = null;
            _progress = null;
        }

        if (_closing.IsCancellationRequested) return;
        await LoadAsync(false).ConfigureAwait(true);
    }

    private async Task UpdateAllAsync()
    {
        var pending = _languages
            .SelectMany(language => language.Parts.Where(state => state.Status == DictionaryStatus.UpdateAvailable).Select(state => (Code: state.Installed?.Code ?? language.Code, state.Part)))
            .Distinct()
            .ToList();

        foreach (var (code, part) in pending)
        {
            if (_closing.IsCancellationRequested) return;
            _chosenSources.Remove((code, part));
            Select(code);
            await InstallAsync(part).ConfigureAwait(true);
            if (_messageIsError || _message == Texts["dictionaries.cancelled"]) return;
        }
    }
}
