using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shortcuts;

public sealed record ShortcutEditorServices(
    IShortcutCatalog Catalog,
    IShortcutCategories Categories,
    IShortcutSearch Search,
    IShortcutDraftFactory Drafts,
    IGestureNotation Notation,
    IShortcutCapture Capture,
    IIconCatalog Icons,
    ITextCatalog Texts);

public sealed class ShortcutEditorView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const double CategoryListWidth = 210;
    private const double RowIconSize = 16;

    private readonly ShortcutEditorServices _services;
    private DialogChrome? _chrome;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly ShortcutDraft _draft;
    private readonly IReadOnlyList<ShortcutCategory> _categories;
    private readonly List<Button> _categoryButtons = [];
    private readonly TextBox _search = new();
    private readonly ToggleButton _byKey = new();
    private readonly KeyCaptureBox _keySearch;
    private readonly ListBox _list = new();
    private readonly TextBlock _empty = new();
    private readonly TextBlock _heading = new();
    private readonly TextBlock _detailTitle = new();
    private readonly TextBlock _detailTrail = new();
    private readonly TextBlock _factory = new();
    private readonly KeyCaptureBox _capture;
    private readonly Button _remove = new();
    private readonly Button _restore = new();
    private readonly Border _message = new();
    private readonly TextBlock _messageText = new();
    private readonly Button _reassign = new();
    private readonly Button _keep = new();
    private readonly StackPanel _detail = new();
    private readonly TextBlock _detailHint = new();

    private int _category;
    private GestureChord? _keyQuery;
    private GestureChord? _pending;
    private string? _selectedId;

    public ShortcutEditorView(ModuleKind activeModule, ShortcutEditorServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _draft = services.Drafts.Create(services.Catalog.Entries());
        _categories = services.Categories.Build(_draft.Entries);
        _category = Math.Max(0, _categories.ToList().FindIndex(category =>
            category is ModuleShortcutCategory module && module.Module == activeModule));

        _keySearch = new KeyCaptureBox(services.Capture, services.Notation,
            Texts["shortcuts.editor.keySearch"], Texts["shortcuts.editor.capturing"], _metrics) { IsVisible = false };
        _capture = new KeyCaptureBox(services.Capture, services.Notation,
            Texts["shortcuts.editor.capture"], Texts["shortcuts.editor.capturing"], _metrics);

        _keySearch.Captured += (_, chord) =>
        {
            _keyQuery = chord;
            _keySearch.Show(chord.Canonical());
            Refill();
        };
        _capture.Captured += (_, chord) => Propose(chord);
        _draft.Changed += (_, _) =>
        {
            Refill();
            ShowDetail();
        };

        Content = Layout();

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts["shortcuts.editor.restoreAll"], () =>
            {
                ClearMessage();
                _draft.RestoreAll();
            })
            .WithAction(Texts["shortcuts.editor.apply"], () => Completed?.Invoke(this, _draft.Edits()), primary: true);

        SelectCategory(_category);
        AttachedToVisualTree += (_, _) => _search.Focus();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private ITextCatalog Texts => _services.Texts;

    private Control Layout()
    {
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions($"{CategoryListWidth},Auto,*") };
        var divider = new Border { Width = 1, Margin = new Thickness(_metrics.SpacingLarge, 0) }
            .Themed(Border.BackgroundProperty, Palette.Keys.Border);
        var main = Main();

        Grid.SetColumn(divider, 1);
        Grid.SetColumn(main, 2);
        layout.Children.Add(CategoryList());
        layout.Children.Add(divider);
        layout.Children.Add(main);

        return layout;
    }

    private Control CategoryList()
    {
        var stack = new StackPanel { Spacing = 2 };

        for (var i = 0; i < _categories.Count; i++)
        {
            var category = _categories[i];

            if (category is ModuleShortcutCategory && (i == 0 || _categories[i - 1] is not ModuleShortcutCategory))
                stack.Children.Add(Caption(Texts["shortcuts.editor.modules"]));
            if (category is MenuShortcutCategory && (i == 0 || _categories[i - 1] is not MenuShortcutCategory))
                stack.Children.Add(Caption(Texts["shortcuts.editor.menus"]));

            var index = i;
            var count = _draft.Entries.Count(category.Contains);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var title = new TextBlock
            {
                Text = category.Title,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var number = new TextBlock
            {
                Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture),
                FontSize = _metrics.FontSizeSmall,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

            Grid.SetColumn(number, 1);
            row.Children.Add(title);
            row.Children.Add(number);

            var button = new Button
            {
                Content = row,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(_metrics.SpacingSmall, 6),
                BorderThickness = default,
            };

            button.Classes.Add("ribbon");
            button.Click += (_, _) => SelectCategory(index);
            _categoryButtons.Add(button);
            stack.Children.Add(button);
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack,
        };
    }

    private Control Main()
    {
        _heading.FontSize = _metrics.FontSizeHeading;
        _heading.FontWeight = FontWeight.SemiBold;
        _heading.Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall);

        _search.PlaceholderText = Texts["shortcuts.editor.search"];
        _search.TextChanged += (_, _) => Refill();
        Avalonia.Automation.AutomationProperties.SetName(_search, Texts["shortcuts.editor.search"]);

        _byKey.Content = Icon("ui.keyboard");
        _byKey.Padding = new Thickness(_metrics.SpacingSmall, 0);
        _byKey.Margin = new Thickness(0, 2, 2, 2);
        _byKey.VerticalAlignment = VerticalAlignment.Stretch;
        _byKey.Background = Brushes.Transparent;
        _byKey.BorderThickness = new Thickness(0);
        _byKey.CornerRadius = new CornerRadius(_metrics.RadiusTight);
        _byKey.Classes.Add("keySearch");
        ToolTip.SetTip(_byKey, Texts["shortcuts.editor.byKey"]);
        Avalonia.Automation.AutomationProperties.SetName(_byKey, Texts["shortcuts.editor.byKey"]);
        _byKey.IsCheckedChanged += (_, _) => ToggleKeySearch();
        _byKey.HorizontalAlignment = HorizontalAlignment.Right;
        _search.Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall, 34, _metrics.SpacingSmall);

        var searchHost = new Panel { Children = { _search, _keySearch, _byKey } };
        var searchRow = new Grid();
        searchRow.Children.Add(searchHost);

        _list.Classes.Add("compact");
        _list.SelectionMode = SelectionMode.Single;
        _list.ItemTemplate = new FuncDataTemplate<ShortcutEntry>((entry, _) => entry is null ? new Border() : Row(entry), supportsRecycling: false);
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is ShortcutEntry entry && entry.Id != _selectedId)
            {
                _selectedId = entry.Id;
                ClearMessage();
                ShowDetail();
            }
        };

        _empty.Text = Texts["shortcuts.editor.none"];
        _empty.IsVisible = false;
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.Margin = new Thickness(0, _metrics.SpacingLarge * 2);
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var listFrame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            ClipToBounds = true,
            Margin = new Thickness(0, _metrics.SpacingMedium),
            Child = new Panel { Children = { _list, _empty } },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        Place(grid, _heading, 0);
        Place(grid, searchRow, 1);
        Place(grid, listFrame, 2);
        Place(grid, Detail(), 3);

        return grid;
    }

    private Control Detail()
    {
        _detailTitle.FontWeight = FontWeight.SemiBold;
        _detailTitle.FontSize = _metrics.FontSize + 1;
        _detailTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _detailTrail.FontSize = _metrics.FontSizeSmall;
        _detailTrail.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _factory.FontSize = _metrics.FontSizeSmall;
        _factory.VerticalAlignment = VerticalAlignment.Center;
        _factory.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Quiet(_remove, Texts["shortcuts.editor.remove"], () =>
        {
            ClearMessage();
            if (_selectedId is { } id) _draft.Remove(id);
        });
        Quiet(_restore, Texts["shortcuts.editor.restore"], () =>
        {
            ClearMessage();
            if (_selectedId is { } id) _draft.RestoreDefault(id);
        });

        var captureRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall, Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0) };
        actions.Children.Add(_remove);
        actions.Children.Add(_restore);
        Grid.SetColumn(actions, 1);
        captureRow.Children.Add(_capture);
        captureRow.Children.Add(actions);

        _messageText.TextWrapping = TextWrapping.Wrap;
        _messageText.VerticalAlignment = VerticalAlignment.Center;
        _messageText.FontSize = _metrics.FontSizeSmall + 1;

        Quiet(_keep, Texts["shortcuts.editor.keep"], ClearMessage);
        _reassign.Content = Texts["shortcuts.editor.reassign"];
        _reassign.Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall / 2);
        _reassign.CornerRadius = new CornerRadius(_metrics.RadiusTight);
        _reassign.Classes.Add("dialogPrimary");
        _reassign.Click += (_, _) => Reassign();

        var messageRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        var messageActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall, Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0) };
        messageActions.Children.Add(_keep);
        messageActions.Children.Add(_reassign);
        Grid.SetColumn(messageActions, 1);
        messageRow.Children.Add(_messageText);
        messageRow.Children.Add(messageActions);

        _message.Child = messageRow;
        _message.Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall);
        _message.CornerRadius = new CornerRadius(_metrics.RadiusTight);
        _message.BorderThickness = new Thickness(1);
        _message.IsVisible = false;

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var names = new StackPanel { Spacing = 1, Children = { _detailTitle, _detailTrail } };
        Grid.SetColumn(_factory, 1);
        header.Children.Add(names);
        header.Children.Add(_factory);

        _detail.Spacing = _metrics.SpacingSmall;
        _detail.Children.Add(header);
        _detail.Children.Add(Caption(Texts["shortcuts.editor.shortcut"]));
        _detail.Children.Add(captureRow);
        _detail.Children.Add(_message);

        _detail.IsVisible = false;
        _detailHint.Text = Texts["shortcuts.editor.selectHint"];
        _detailHint.VerticalAlignment = VerticalAlignment.Center;
        _detailHint.HorizontalAlignment = HorizontalAlignment.Center;
        _detailHint.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        return new Border
        {
            Padding = new Thickness(_metrics.SpacingLarge, _metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            MinHeight = 150,
            Child = new Panel { Children = { _detail, _detailHint } },
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }

    private Control Row(ShortcutEntry entry)
    {
        var gesture = _draft.GestureOf(entry.Id);
        var conflicts = _draft.ConflictsOf(entry.Id);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin = new Thickness(_metrics.SpacingSmall, 3),
        };

        var icon = Icon(entry.Id);
        icon.Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0);

        var names = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock
        {
            Text = entry.Title,
            FontSize = _metrics.FontSize,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));
        names.Children.Add(new TextBlock
        {
            Text = Trail(entry),
            FontSize = _metrics.FontSizeSmall,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var marker = new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(_metrics.SpacingSmall, 0),
            IsVisible = conflicts.Count > 0 || _draft.IsCustomized(entry.Id),
        }.Themed(Border.BackgroundProperty, conflicts.Count > 0 ? Palette.Keys.Warning : "PulseAccent");

        ToolTip.SetTip(marker, conflicts.Count > 0
            ? Texts.Format("shortcuts.editor.conflictBadge", conflicts[0].Title)
            : Texts["shortcuts.editor.customized"]);

        var cap = Keycap(gesture);

        Grid.SetColumn(names, 1);
        Grid.SetColumn(marker, 2);
        Grid.SetColumn(cap, 3);
        grid.Children.Add(icon);
        grid.Children.Add(names);
        grid.Children.Add(marker);
        grid.Children.Add(cap);

        return grid;
    }

    private Control Keycap(string? gesture)
    {
        if (gesture is not { Length: > 0 })
            return new TextBlock
            {
                Text = "—",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(_metrics.SpacingMedium, 0),
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextDisabled);

        return new Border
        {
            Padding = new Thickness(_metrics.SpacingSmall + 2, 2),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1, 1, 1, 2),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 28,
            Child = new TextBlock
            {
                Text = _services.Notation.Display(gesture),
                FontSize = _metrics.FontSizeSmall + 1,
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }

    private Control Icon(string id) =>
        _services.Icons.Find(id) is { } geometry
            ? new IconView(geometry, RowIconSize) { VerticalAlignment = VerticalAlignment.Center }
            : new Border { Width = RowIconSize, Height = RowIconSize };

    private string Trail(ShortcutEntry entry)
    {
        var module = _services.Categories.ModuleTitle(entry.Module);
        return entry.Trail is { Length: > 0 } trail ? $"{module} · {trail}" : module;
    }

    private void SelectCategory(int index)
    {
        _category = index;
        for (var i = 0; i < _categoryButtons.Count; i++) _categoryButtons[i].Classes.Set("checked", i == _category);
        _heading.Text = _categories[_category].Title;
        Refill();
    }

    private void ToggleKeySearch()
    {
        var byKey = _byKey.IsChecked == true;
        _search.IsVisible = !byKey;
        _keySearch.IsVisible = byKey;
        _keyQuery = null;
        _keySearch.Show(null);
        if (byKey) _keySearch.Focus();
        else _search.Focus();
        Refill();
    }

    private void Refill()
    {
        var category = _categories[_category];
        var query = _search.Text;
        var byKey = _byKey.IsChecked == true;

        var visible = _draft.Entries
            .Where(category.Contains)
            .Where(entry => byKey
                ? _keyQuery is null || _services.Search.Matches(_draft.GestureOf(entry.Id), _keyQuery)
                : _services.Search.Matches(entry, _draft.GestureOf(entry.Id), query))
            .ToList();

        _list.ItemsSource = visible;
        _list.SelectedItem = visible.FirstOrDefault(entry => entry.Id == _selectedId);
        _empty.IsVisible = visible.Count == 0;
    }

    private void ShowDetail()
    {
        var entry = _draft.Entries.FirstOrDefault(candidate => candidate.Id == _selectedId);

        _detail.IsVisible = entry is not null;
        _detailHint.IsVisible = entry is null;
        if (entry is null) return;

        var gesture = _draft.GestureOf(entry.Id);

        _detailTitle.Text = entry.Title;
        _detailTrail.Text = Trail(entry);
        _factory.Text = entry.FactoryGesture is { Length: > 0 } factory
            ? Texts.Format("shortcuts.editor.factory", _services.Notation.Display(factory))
            : Texts["shortcuts.editor.factoryNone"];
        _capture.Show(gesture);
        _remove.IsEnabled = gesture is not null;
        _restore.IsEnabled = _draft.IsCustomized(entry.Id);

        if (!_message.IsVisible && _draft.ConflictsOf(entry.Id) is { Count: > 0 } conflicts)
            Warn(Texts.Format("shortcuts.editor.conflictExisting", _services.Notation.Display(gesture), Describe(conflicts)), offerReassign: false);
    }

    private void Propose(GestureChord chord)
    {
        if (_selectedId is not { } id) return;

        var check = _draft.Check(id, chord);
        var shown = _services.Notation.Display(chord);

        switch (check.Problem)
        {
            case ChordProblem.NeedsModifier:
                Fail(Texts.Format("shortcuts.editor.needsModifier", shown));
                return;
            case ChordProblem.Reserved:
                Fail(Texts.Format("shortcuts.editor.reserved", shown));
                return;
        }

        if (check.Conflicts.Count > 0)
        {
            _pending = chord;
            Warn(Texts.Format("shortcuts.editor.conflict", shown, Describe(check.Conflicts)), offerReassign: true);
            return;
        }

        ClearMessage();
        _draft.Assign(id, chord, reassign: false);
        _capture.Focus();
    }

    private void Reassign()
    {
        if (_selectedId is { } id && _pending is { } chord)
        {
            ClearMessage();
            _draft.Assign(id, chord, reassign: true);
        }
    }

    private string Describe(IReadOnlyList<ShortcutEntry> entries) =>
        string.Join(", ", entries.Select(entry => $"«{entry.Title}» ({_services.Categories.ModuleTitle(entry.Module)})"));

    private void Fail(string text)
    {
        _pending = null;
        ShowMessage(text, Palette.Keys.Danger, offerReassign: false);
    }

    private void Warn(string text, bool offerReassign) => ShowMessage(text, Palette.Keys.Warning, offerReassign);

    private void ShowMessage(string text, string colour, bool offerReassign)
    {
        _messageText.Text = text;
        _messageText.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        _message.Themed(Border.BorderBrushProperty, colour);
        _message.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
        _reassign.IsVisible = offerReassign;
        _keep.IsVisible = offerReassign;
        _message.IsVisible = true;
    }

    private void ClearMessage()
    {
        _pending = null;
        _message.IsVisible = false;
    }

    private void Quiet(Button button, string label, Action run)
    {
        button.Content = label;
        button.Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall);
        button.CornerRadius = new CornerRadius(_metrics.RadiusTight);
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Click += (_, _) => run();
    }

    private void Place(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private DialogChrome Chrome => _chrome ??= new DialogChrome(_services.Icons, _metrics);

    private TextBlock Caption(string text) =>
        Chrome.Caption(text, new Thickness(0, _metrics.SpacingSmall, 0, 2));
}
