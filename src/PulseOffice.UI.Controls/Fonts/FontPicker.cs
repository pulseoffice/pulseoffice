using PulseOffice.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Typography.Catalog;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fonts;

public sealed record FontPickerServices(
    IFontCatalog Catalog,
    IRecentFonts Recent,
    IRuntimeFonts Runtime,
    IFontActivation Activation,
    IDialogService Dialogs,
    FontDescriptions Descriptions,
    IIconCatalog Icons,
    IDiagnosticTexts Diagnostics,
    IDisplayFonts Display,
    IFlyoutDismissal Dismissal);

public interface IFontPickerFactory
{
    FontPicker Create(string initial, InterfaceDensity density = InterfaceDensity.Compact);
}

public sealed class FontPickerFactory(FontPickerServices services) : IFontPickerFactory
{
    public FontPicker Create(string initial, InterfaceDensity density = InterfaceDensity.Compact) => new(initial, services, density);
}

public sealed class FontPicker : UserControl
{
    private const int MaximumOnlineRows = 200;

    private readonly FontPickerServices _services;
    private readonly DesignTokens.Metrics _metrics;
    private readonly TextBlock _name = new();
    private readonly Button _face;
    private readonly TextBox _search = new();
    private readonly ListBox _list = new();
    private readonly TextBlock _status = new();
    private readonly Flyout _flyout;
    private IReadOnlyList<FontFamilyInfo> _families = [];
    private CancellationTokenSource? _activation;

    public event EventHandler<string>? FamilyChanged;

    public string Family { get; private set; }

    public FontPicker(string initial, FontPickerServices services, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _metrics = DesignTokens.For(density);
        Family = initial ?? string.Empty;

        _name.FontSize = _metrics.FontSize + 1;
        _name.VerticalAlignment = VerticalAlignment.Center;
        _name.TextTrimming = TextTrimming.CharacterEllipsis;

        var faceLayout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        faceLayout.Children.Add(_name);
        if (services.Icons.Find("ui.chevronDown") is { } chevron)
        {
            var icon = new IconView(chevron, _metrics.IconSize * 0.75) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0) };
            Grid.SetColumn(icon, 1);
            faceLayout.Children.Add(icon);
        }

        _face = new Button
        {
            Content = faceLayout,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = _metrics.ControlHeight,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall / 2),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };
        ToolTip.SetTip(_face, services.Descriptions.Texts["font.picker.tip"]);

        _flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            FlyoutPresenterClasses = { "plain" },
            Content = BuildPopup(),
        };
        services.Dismissal.PassThrough(_flyout);
        _flyout.Opened += (_, _) =>
        {
            _search.Text = string.Empty;
            _ = LoadAsync();
            Dispatcher.UIThread.Post(() => _search.Focus(), DispatcherPriority.Input);
        };

        _face.Click += (_, _) => services.Dismissal.Toggle(_flyout, _face);
        Content = _face;
        ShowFamily();
    }

    private Control BuildPopup()
    {
        var texts = _services.Descriptions.Texts;

        _search.PlaceholderText = texts["font.picker.search"];
        _search.FontSize = _metrics.FontSize;
        if (_services.Icons.Find("ui.search") is { } glass)
            _search.InnerLeftContent = new IconView(glass, _metrics.IconSize * 0.8) { Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        _search.TextChanged += (_, _) => Fill();
        _search.KeyDown += OnSearchKey;

        _list.ItemTemplate = new FuncDataTemplate<PickerRow>((row, _) => row is null ? new TextBlock() : RowView(row));
        _list.SelectionMode = SelectionMode.Single;
        _list.Styles.Add(new Avalonia.Styling.Style(selector => selector.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(ListBoxItem.PaddingProperty, new Thickness(_metrics.SpacingSmall, 1)),
                new Avalonia.Styling.Setter(ListBoxItem.MinHeightProperty, 0d),
                new Avalonia.Styling.Setter(ListBoxItem.CornerRadiusProperty, new CornerRadius(_metrics.RadiusTight)),
            },
        });
        _list.Background = Brushes.Transparent;
        _list.Height = 300;
        _list.AddHandler(PointerReleasedEvent, (_, args) =>
        {
            if (args.Source is Visual visual && visual.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null) Commit();
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        _list.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            Commit();
            args.Handled = true;
        };

        _status.FontSize = _metrics.FontSizeSmall;
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var browse = new Button
        {
            Content = texts["font.picker.browse"],
            FontSize = _metrics.FontSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };
        browse.Click += async (_, _) => await BrowseAsync().ConfigureAwait(true);

        var surface = new Border
        {
            Width = 320,
            Margin = new Thickness(0, 0, 18, 18),
            Padding = new Thickness(_metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 10 28 -6 #40101828"),
            Child = new StackPanel
            {
                Spacing = _metrics.SpacingSmall,
                Children = { _search, _list, _status, browse },
            },
        };
        surface.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceOverlay);
        surface.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        return surface;
    }

    private void OnSearchKey(object? sender, KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Down:
                _list.SelectedIndex = FirstSelectable(0, 1);
                _list.Focus();
                args.Handled = true;
                break;
            case Key.Enter:
                var query = (_search.Text ?? string.Empty).Trim();
                var exact = _families.FirstOrDefault(family => string.Equals(family.Family, query, StringComparison.OrdinalIgnoreCase));
                var first = Rows().FirstOrDefault(row => row.Font is not null)?.Font;
                if (exact is not null) _ = ChooseAsync(exact);
                else if (first is not null) _ = ChooseAsync(first);
                else if (query.Length > 0) Apply(query);
                args.Handled = true;
                break;
        }
    }

    private int FirstSelectable(int start, int step)
    {
        var rows = _list.ItemsSource as IReadOnlyList<PickerRow> ?? [];
        for (var index = start; index >= 0 && index < rows.Count; index += step)
            if (rows[index].Font is not null) return index;
        return -1;
    }

    private async Task LoadAsync()
    {
        if (_families.Count == 0) _status.Text = _services.Descriptions.Texts["font.picker.loading"];
        try
        {
            _families = await _services.Catalog.ListAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or InvalidDataException)
        {
        }

        Fill();
    }

    private void Fill()
    {
        var rows = Rows().ToList();
        _list.ItemsSource = rows;
        _status.Text = rows.Any(row => row.Font is not null) ? string.Empty : _services.Descriptions.Texts["font.picker.none"];
    }

    private IEnumerable<PickerRow> Rows()
    {
        var texts = _services.Descriptions.Texts;
        var query = (_search.Text ?? string.Empty).Trim();
        var byName = _families.ToDictionary(family => family.Family, StringComparer.OrdinalIgnoreCase);

        if (query.Length == 0)
        {
            var recent = _services.Recent.All.Select(name => byName.GetValueOrDefault(name)).OfType<FontFamilyInfo>().ToList();
            if (recent.Count > 0)
            {
                yield return PickerRow.Header(texts["font.picker.recent"]);
                foreach (var family in recent) yield return PickerRow.Of(family);
            }

            var inDocument = _families.Where(family => family.InDocument).ToList();
            if (inDocument.Count > 0)
            {
                yield return PickerRow.Header(texts["font.picker.document"]);
                foreach (var family in inDocument) yield return PickerRow.Of(family);
            }

            yield return PickerRow.Header(texts["font.picker.all"]);
            foreach (var family in _families.Where(family => family.Origin is FontOrigin.System or FontOrigin.Downloaded or FontOrigin.Document))
                yield return PickerRow.Of(family);
            yield break;
        }

        var local = _families.Where(family => family.Origin != FontOrigin.Online && family.Matches(query)).ToList();
        foreach (var family in local) yield return PickerRow.Of(family);

        var online = _families.Where(family => family.Origin == FontOrigin.Online && family.Matches(query)).Take(MaximumOnlineRows).ToList();
        if (online.Count > 0)
        {
            yield return PickerRow.Header(texts["font.picker.online"]);
            foreach (var family in online) yield return PickerRow.Of(family);
        }
    }

    private Control RowView(PickerRow row)
    {
        if (row.Font is not { } family)
        {
            return new TextBlock
            {
                Text = row.Title.ToUpperInvariant(),
                FontSize = _metrics.FontSizeSmall - 1,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 0.6,
                Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
                IsHitTestVisible = false,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        }

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = _metrics.RowHeight + 6 };
        layout.Children.Add(new TextBlock
        {
            Text = family.Family,
            FontFamily = family.Origin == FontOrigin.Online ? FontFamily.Default : _services.Display.Resolve(family.Family),
            FontSize = _metrics.FontSize + 3,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        if (family.Origin == FontOrigin.Online && _services.Icons.Find("ui.cloud") is { } cloud)
        {
            var icon = new IconView(cloud, _metrics.IconSize * 0.8) { VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(icon, _services.Descriptions.Texts["font.picker.downloadTip"]);
            Grid.SetColumn(icon, 1);
            layout.Children.Add(icon);
        }
        else if (!family.License.IsFree && family.License.Freedom == PulseOffice.Typography.Licensing.LicenseFreedom.Restricted)
        {
            var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
            dot.Themed(Border.BackgroundProperty, Palette.Keys.Warning);
            ToolTip.SetTip(dot, _services.Descriptions.Freedom(family.License));
            Grid.SetColumn(dot, 1);
            layout.Children.Add(dot);
        }

        return layout;
    }

    private void Commit()
    {
        if (_list.SelectedItem is PickerRow { Font: { } family }) _ = ChooseAsync(family);
    }

    private async Task ChooseAsync(FontFamilyInfo family)
    {
        _activation?.Cancel();
        var activation = new CancellationTokenSource();
        _activation = activation;
        var texts = _services.Descriptions.Texts;

        if (family.Origin == FontOrigin.Online || family.Origin == FontOrigin.Downloaded || family.Origin == FontOrigin.Document)
        {
            var progress = new Progress<FontProgress>(step =>
            {
                if (!activation.IsCancellationRequested)
                    _status.Text = texts.Format("font.picker.downloading", family.Family, (int)Math.Round(step.Fraction * 100));
            });
            OperationResult<bool> result;
            try
            {
                result = await _services.Activation.ActivateAsync(family, progress, activation.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (activation.IsCancellationRequested) return;
            if (!result.Succeeded)
            {
                _status.Text = texts.Format("font.picker.failed", family.Family, (result.Diagnostics.FirstOrDefault() is { } diagnostic ? _services.Diagnostics.Describe(diagnostic) : string.Empty));
                return;
            }
        }

        _flyout.Hide();
        Apply(family.Family);
    }

    private async Task BrowseAsync()
    {
        _flyout.Hide();
        var view = new FontBrowserView(Family, _services);
        var chosen = await _services.Dialogs.ShowAsync<string>(view, new DialogOptions
        {
            TitleKey = "font.browser.title",
            PreferredWidth = 1040,
            PreferredHeight = 680,
        }).ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(chosen)) Apply(chosen);
    }

    private void Apply(string family)
    {
        Family = family;
        _services.Recent.Remember(family);
        ShowFamily();
        FamilyChanged?.Invoke(this, family);
    }

    public void Show(string family)
    {
        Family = family ?? string.Empty;
        ShowFamily();
    }

    private void ShowFamily()
    {
        _name.Text = Family.Length == 0 ? _services.Descriptions.Texts["font.picker.default"] : Family;
        _name.FontFamily = _services.Display.Resolve(Family);
    }

    private sealed record PickerRow(string Title, FontFamilyInfo? Font)
    {
        public static PickerRow Header(string title) => new(title, null);

        public static PickerRow Of(FontFamilyInfo family) => new(family.Family, family);
    }
}
