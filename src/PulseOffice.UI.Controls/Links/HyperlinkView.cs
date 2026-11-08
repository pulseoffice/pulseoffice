using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Links;

public sealed record HyperlinkServices(ILinkAddresses Addresses, ITextCatalog Texts, IIconCatalog? Icons, Func<CancellationToken, Task<string?>>? BrowseFile);

public sealed class HyperlinkView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly LinkRequest _request;
    private readonly HyperlinkServices _services;
    private readonly ITextCatalog _texts;
    private readonly ExclusiveChoices _kinds = new();
    private readonly Dictionary<LinkKind, Control> _pages = [];
    private readonly TextBox? _text;
    private readonly TextBox _web;
    private readonly TextBox _file;
    private readonly TextBox _mailTo;
    private readonly TextBox _mailSubject;
    private readonly TextBox _search;
    private readonly TreeView _places;
    private readonly TextBlock _placesEmpty;
    private readonly TextBlock _target;
    private readonly DialogFooter _footer;
    private readonly Button _confirm;
    private bool _textTouched;
    private bool _seeding;

    public event EventHandler<object?>? Completed;

    public HyperlinkView(LinkRequest request, HyperlinkServices services, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        _request = request;
        _services = services;
        _texts = services.Texts;
        var metrics = DesignTokens.For(density);
        var addresses = services.Addresses;
        var initialKind = addresses.Classify(request.Address, request.Places);
        if (!request.Kinds.Contains(initialKind)) initialKind = request.Kinds[0];

        _web = new TextBox { PlaceholderText = "https://", Text = initialKind == LinkKind.Web ? request.Address : string.Empty };
        _file = new TextBox { PlaceholderText = _texts["link.file.placeholder"], Text = initialKind == LinkKind.File ? addresses.ReadFile(request.Address) : string.Empty };
        var mail = initialKind == LinkKind.Mail ? addresses.ReadMail(request.Address) : new MailAddress(string.Empty, string.Empty);
        _mailTo = new TextBox { PlaceholderText = _texts["link.mail.placeholder"], Text = mail.To };
        _mailSubject = new TextBox { Text = mail.Subject };
        _search = new TextBox { PlaceholderText = _texts["link.place.search"], Classes = { "search" } };
        _places = new TreeView
        {
            MinHeight = 200,
            MaxHeight = 260,
            ItemTemplate = new FuncTreeDataTemplate<LinkPlace>((place, _) => PlaceRow(place, metrics), place => place.Children),
        };
        _places.Styles.Add(new Style(selector => selector.OfType<TreeViewItem>())
        {
            Setters = { new Setter(TreeViewItem.IsExpandedProperty, true) },
        });
        _placesEmpty = new TextBlock { Text = _texts["link.place.none"], FontSize = metrics.FontSize, TextWrapping = TextWrapping.Wrap, IsVisible = false }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        AutomationProperties.SetName(_places, _texts["link.place.tree"]);
        AutomationProperties.SetName(_search, _texts["link.place.search"]);
        ShowPlaces(string.Empty);
        if (initialKind == LinkKind.Place && addresses.FindPlace(request.Address, request.Places) is { } current) _places.SelectedItem = current;

        _textTouched = request.Text is { Length: > 0 } || !request.TextEditable;
        if (request.ShowsText)
        {
            _text = new TextBox { Text = request.Text ?? string.Empty, IsReadOnly = !request.TextEditable };
            AutomationProperties.SetName(_text, _texts["link.text"]);
        }

        _target = new TextBlock { FontSize = metrics.FontSizeSmall, TextTrimming = TextTrimming.CharacterEllipsis }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var rail = new StackPanel { Spacing = 2, Width = 150 };
        foreach (var kind in request.Kinds)
        {
            var tile = KindTile(kind, metrics);
            _kinds.Add(tile, _texts["link.kind." + KeyOf(kind)]);
            rail.Children.Add(tile);
        }
        AutomationProperties.SetName(rail, _texts["link.kind"]);

        _pages[LinkKind.Web] = WebPage(metrics);
        _pages[LinkKind.Place] = PlacePage(metrics);
        _pages[LinkKind.File] = FilePage(metrics);
        _pages[LinkKind.Mail] = MailPage(metrics);

        var pageHost = new Panel();
        foreach (var kind in request.Kinds) pageHost.Children.Add(_pages[kind]);

        var right = new StackPanel { Spacing = metrics.SpacingMedium };
        if (_text is not null)
        {
            var textSection = new DialogSection(null, metrics);
            textSection.Add(_texts["link.text"], _text, editorWidth: 320);
            right.Children.Add(textSection);
        }
        right.Children.Add(pageHost);
        right.Children.Add(_target);

        var railFrame = new Border
        {
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(metrics.RadiusTight + 2),
            BorderThickness = new Thickness(1),
            Child = rail,
            VerticalAlignment = VerticalAlignment.Stretch,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingLarge };
        Grid.SetColumn(right, 1);
        body.Children.Add(railFrame);
        body.Children.Add(right);

        _footer = new DialogFooter(density);
        if (request.CanRemove)
            _footer.WithAction(_texts["link.remove"], () => Completed?.Invoke(this, new LinkChoice(LinkKind.Web, string.Empty, string.Empty) { Remove = true }));
        _footer.WithCancel(_texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(_texts["prompt.confirm"], Confirm, out _confirm, primary: true);

        _kinds.Changed += (_, _) => ShowKind();
        _kinds.Select(request.Kinds.ToList().IndexOf(initialKind), notify: false);
        ShowKind();

        foreach (var box in new[] { _web, _file, _mailTo, _mailSubject }) box.TextChanged += (_, _) => Refresh();
        _search.TextChanged += (_, _) => ShowPlaces(_search.Text ?? string.Empty);
        _places.SelectionChanged += (_, _) => Refresh();
        _places.DoubleTapped += (_, _) =>
        {
            if (_confirm.IsEnabled) Confirm();
        };
        _search.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Down) return;
            if (_places.ItemCount > 0)
            {
                _places.SelectedItem ??= FirstSelectable(_places.ItemsSource as IEnumerable<LinkPlace> ?? []);
                _places.Focus(NavigationMethod.Directional);
            }
            e.Handled = true;
        };
        if (_text is not null)
        {
            _text.TextChanged += (_, _) =>
            {
                if (!_seeding) _textTouched = true;
            };
        }
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && _confirm.IsEnabled)
            {
                Confirm();
                e.Handled = true;
            }
        };

        Loaded += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusPage(), Avalonia.Threading.DispatcherPriority.Input);

        Refresh();
        Content = body;
    }

    public Control? Footer => _footer;

    public double? PreferredWidth => 640;

    public LinkKind Kind => _request.Kinds[Math.Max(0, _kinds.Selected)];

    public IReadOnlyList<ToggleButton> KindTiles => _kinds.Items;

    public TextBox? TextBox => _text;

    public TextBox WebBox => _web;

    public TextBox FileBox => _file;

    public TextBox MailToBox => _mailTo;

    public TextBox MailSubjectBox => _mailSubject;

    public TextBox SearchBox => _search;

    public TreeView Places => _places;

    public Button ConfirmButton => _confirm;

    public string Address => Kind switch
    {
        LinkKind.Web => _services.Addresses.Web(_web.Text ?? string.Empty),
        LinkKind.File => _services.Addresses.File(_file.Text ?? string.Empty),
        LinkKind.Mail => _services.Addresses.Mail(new MailAddress(_mailTo.Text ?? string.Empty, _mailSubject.Text ?? string.Empty)),
        _ => _places.SelectedItem is LinkPlace { Selectable: true } place ? place.Address : string.Empty,
    };

    public void SelectKind(LinkKind kind)
    {
        var index = _request.Kinds.ToList().IndexOf(kind);
        if (index >= 0) _kinds.Select(index, notify: true);
    }

    public LinkChoice? Result()
    {
        var address = Address;
        if (!_services.Addresses.IsComplete(Kind, address)) return null;
        var shown = _text?.Text is { Length: > 0 } typed ? typed : Fallback(address);
        return new LinkChoice(Kind, address, shown);
    }

    private string Fallback(string address) => Kind switch
    {
        LinkKind.Place => (_places.SelectedItem as LinkPlace)?.Label.Trim() ?? address,
        LinkKind.Mail => _services.Addresses.ReadMail(address).To,
        LinkKind.File => Path.GetFileName(_services.Addresses.ReadFile(address)) is { Length: > 0 } name ? name : address,
        _ => address,
    };

    private void Confirm()
    {
        if (Result() is { } choice) Completed?.Invoke(this, choice);
    }

    private void ShowKind()
    {
        foreach (var (kind, page) in _pages) page.IsVisible = kind == Kind;
        Refresh();
    }

    private void FocusPage()
    {
        Control target = Kind switch
        {
            LinkKind.Web => _web,
            LinkKind.File => _file,
            LinkKind.Mail => _mailTo,
            _ => _search,
        };
        target.Focus(NavigationMethod.Tab);
    }

    private void Refresh()
    {
        var address = Address;
        var complete = _services.Addresses.IsComplete(Kind, address);
        _confirm.IsEnabled = complete;
        _target.Text = complete ? _texts.Format("link.target", Kind == LinkKind.Place ? Fallback(address) : address) : _texts["link.target.missing"];
        if (_text is null || _textTouched) return;
        _seeding = true;
        _text.Text = complete ? Fallback(address) : string.Empty;
        _seeding = false;
    }

    private void ShowPlaces(string filter)
    {
        var shown = Filter(_request.Places, filter.Trim());
        _places.ItemsSource = shown;
        _placesEmpty.IsVisible = shown.Count == 0;
        _places.IsVisible = shown.Count > 0;
    }

    private IReadOnlyList<LinkPlace> Filter(IReadOnlyList<LinkPlace> places, string filter)
    {
        if (filter.Length == 0) return places;
        var kept = new List<LinkPlace>();
        foreach (var place in places)
        {
            var children = Filter(place.Children, filter);
            if (place.Label.Contains(filter, StringComparison.CurrentCultureIgnoreCase)) kept.Add(place);
            else if (children.Count > 0) kept.Add(place with { Children = children });
        }
        return kept;
    }

    private LinkPlace? FirstSelectable(IEnumerable<LinkPlace> places)
    {
        foreach (var place in places)
        {
            if (place.Selectable) return place;
            if (FirstSelectable(place.Children) is { } nested) return nested;
        }
        return null;
    }

    private Control PlaceRow(LinkPlace place, DesignTokens.Metrics metrics)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        if (_services.Icons?.Find(place.Icon) is { } geometry)
            row.Children.Add(new IconView(geometry, metrics.IconSize - 2).Themed(IconView.ForegroundProperty, place.Selectable ? Palette.Keys.TextPrimary : Palette.Keys.TextSecondary));
        var label = new TextBlock { Text = place.Label.Trim(), FontSize = metrics.FontSize, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 360 };
        if (!place.Selectable) label.FontWeight = FontWeight.SemiBold;
        row.Children.Add(label);
        AutomationProperties.SetName(row, place.Label.Trim());
        return row;
    }

    private ToggleButton KindTile(LinkKind kind, DesignTokens.Metrics metrics)
    {
        var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingMedium };
        if (_services.Icons?.Find("link.kind." + KeyOf(kind)) is { } geometry)
            face.Children.Add(new IconView(geometry, metrics.IconSize + 4).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary));
        face.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = _texts["link.kind." + KeyOf(kind)], FontSize = metrics.FontSize, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = _texts["link.kind." + KeyOf(kind) + ".hint"], FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap, MaxWidth = 100, HorizontalAlignment = HorizontalAlignment.Left }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            },
        });
        var tile = new ToggleButton
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(metrics.SpacingMedium, metrics.SpacingSmall),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            BorderThickness = new Thickness(0),
            Content = face,
        };
        tile.Classes.Add("iconChoice");
        return tile;
    }

    private Control WebPage(DesignTokens.Metrics metrics)
    {
        var section = new DialogSection(_texts["link.kind.web"], metrics);
        section.Add(_texts["link.web.address"], _web, editorWidth: 320);
        section.Add(null, Hint(_texts["link.web.hint"], metrics), DialogRowKind.Indented, editorWidth: 320);
        return section;
    }

    private Control PlacePage(DesignTokens.Metrics metrics)
    {
        var stack = new StackPanel { Spacing = metrics.SpacingSmall };
        stack.Children.Add(new TextBlock { Text = _texts["link.place.explanation"], FontSize = metrics.FontSize, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        stack.Children.Add(_search);
        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            Child = new Panel { Children = { _places, _placesEmpty } },
            Padding = new Thickness(2),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.FieldBorder)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface);
        stack.Children.Add(frame);
        return stack;
    }

    private Control FilePage(DesignTokens.Metrics metrics)
    {
        var section = new DialogSection(_texts["link.kind.file"], metrics);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingSmall };
        row.Children.Add(_file);
        if (_services.BrowseFile is { } browse)
        {
            var button = new Button { Content = _texts["form.browse"], VerticalAlignment = VerticalAlignment.Stretch };
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try
                {
                    if (await browse(CancellationToken.None).ConfigureAwait(true) is { Length: > 0 } chosen) _file.Text = chosen;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or OperationCanceledException)
                {
                }
                finally
                {
                    button.IsEnabled = true;
                }
            };
            Grid.SetColumn(button, 1);
            row.Children.Add(button);
        }
        section.Add(_texts["link.file.path"], row, editorWidth: 340);
        section.Add(null, Hint(_texts["link.file.hint"], metrics), DialogRowKind.Indented, editorWidth: 320);
        return section;
    }

    private Control MailPage(DesignTokens.Metrics metrics)
    {
        var section = new DialogSection(_texts["link.kind.mail"], metrics);
        section.Add(_texts["link.mail.to"], _mailTo, editorWidth: 320);
        section.Add(_texts["link.mail.subject"], _mailSubject, editorWidth: 320);
        return section;
    }

    private static TextBlock Hint(string text, DesignTokens.Metrics metrics) =>
        new TextBlock { Text = text, FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private static string KeyOf(LinkKind kind) => kind switch
    {
        LinkKind.Web => "web",
        LinkKind.Place => "place",
        LinkKind.File => "file",
        _ => "mail",
    };
}
