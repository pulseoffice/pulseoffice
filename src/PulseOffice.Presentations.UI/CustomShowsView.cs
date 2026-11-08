using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed record CustomShowsResult(IReadOnlyList<CustomShow> Shows, CustomShow? Present);

public sealed class CustomShowsView : UserControl, IDialogContent, IDialogFooterOwner
{
    private sealed class WorkingShow
    {
        public required string Name { get; set; }

        public required IReadOnlyList<string> Ids { get; set; }

        public CustomShow ToShow() => new(Name, [.. Ids]);
    }

    private sealed class SlideRow : Button
    {
        private readonly Border _highlight;

        public SlideRow(string slideId, Control line, Border highlight)
        {
            SlideId = slideId;
            _highlight = highlight;
            Content = new Panel { Children = { highlight, line } };
        }

        protected override Type StyleKeyOverride => typeof(Button);

        public string SlideId { get; }

        public void Mark(bool selected) => _highlight.IsVisible = selected;
    }

    public event EventHandler<object?>? Completed;

    private readonly ICustomShowNaming _naming;

    public Control? Footer { get; private set; }

    private readonly Presentation _deck;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;

    private readonly List<WorkingShow> _shows;
    private readonly ListBox _showList = new();
    private readonly TextBox _nameBox;
    private readonly StackPanel _slidePanel = new() { Spacing = 2 };
    private readonly StackPanel _deckPanel = new() { Spacing = 2 };
    private readonly Canvas _dropLayer = new() { IsHitTestVisible = false };
    private readonly Border _dropMarker = new() { Height = 2, IsVisible = false };
    private readonly Motion.IDeckSlidePictures _pictures;
    private readonly Dictionary<string, Slide> _slidesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);
    private readonly ICustomShowEditing _editing;
    private (bool FromDeck, int Index)? _drag;
    private Point _dragOrigin;
    private bool _dragging;
    private int _deckSelected = -1;
    private int _showSelected = -1;
    private readonly Panel _editorHost = new();
    private readonly TextBlock _empty;

    private WorkingShow? _current;

    public CustomShowsView(
        Presentation deck, ITextCatalog texts, Motion.IDeckSlidePictures pictures, ICustomShowEditing editing,
        InterfaceDensity density = InterfaceDensity.Compact, ICustomShowNaming? naming = null)
    {
        _deck = deck ?? throw new ArgumentNullException(nameof(deck));
        for (var index = 0; index < deck.Slides.Count; index++)
            if (_slidesById.TryAdd(deck.Slides[index].Id, deck.Slides[index])) _numbers[deck.Slides[index].Id] = index + 1;
        _pictures = pictures ?? throw new ArgumentNullException(nameof(pictures));
        _editing = editing ?? throw new ArgumentNullException(nameof(editing));
        _dropMarker.Themed(Border.BackgroundProperty, "PulseAccent");
        _dropLayer.Children.Add(_dropMarker);
        _texts = texts;
        _naming = naming ?? new CustomShowNaming();
        _metrics = DesignTokens.For(density);

        _shows = deck.CustomShows.Select(FromShow).ToList();

        _nameBox = new TextBox { PlaceholderText = texts["deck.customShows.name"], FontSize = _metrics.FontSize };
        _nameBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && _current is not null)
            {
                _current.Name = _nameBox.Text ?? string.Empty;
                RefreshList();
            }
        };

        _empty = new TextBlock
        {
            Text = texts["deck.customShows.empty"],
            FontSize = _metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _showList.SelectionChanged += (_, _) =>
        {
            if (!_refreshing) SelectShow(_showList.SelectedIndex);
        };

        var footer = new DialogFooter(density);
        footer.WithCancel(texts["dialog.cancel"], () => Completed?.Invoke(this, null));
        footer.WithAction(texts["dialog.confirm"], Confirm, true);
        Footer = footer;

        Content = BuildBody();
        BuildDeckRows();
        RefreshList();
        SelectShow(_shows.Count > 0 ? 0 : -1);
    }

    private Control BuildBody()
    {
        var newButton = TextButton(_texts["deck.customShows.new"], AddShow);
        var deleteButton = TextButton(_texts["deck.customShows.delete"], DeleteShow);
        var presentButton = TextButton(_texts["deck.customShows.present"], PresentShow, accent: true);

        var leftButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
            Children = { newButton, deleteButton },
        };

        var left = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), Width = 220 };
        var title = SectionLabel(_texts["deck.customShows.list"]);
        Grid.SetRow(title, 0);
        Grid.SetRow(_showList, 1);
        Grid.SetRow(leftButtons, 2);
        Grid.SetRow(presentButton, 3);
        presentButton.Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0);
        presentButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        left.Children.Add(title);
        left.Children.Add(_showList);
        left.Children.Add(leftButtons);
        left.Children.Add(presentButton);

        _editorHost.Children.Add(_empty);
        _editorHost.Children.Add(BuildEditor());

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), MinHeight = 360 };
        Grid.SetColumn(left, 0);
        left.Margin = new Thickness(0, 0, _metrics.SpacingLarge, 0);
        Grid.SetColumn(_editorHost, 1);
        grid.Children.Add(left);
        grid.Children.Add(_editorHost);
        return grid;
    }

    public IReadOnlyList<string> CurrentIds => _current?.Ids ?? [];

    public void AddToShow(int deckIndex, int at)
    {
        if (_current is null || deckIndex < 0 || deckIndex >= _deck.Slides.Count) return;
        _current.Ids = _editing.Insert(_current.Ids, [_deck.Slides[deckIndex].Id], at);
        _showSelected = Math.Clamp(at, 0, _current.Ids.Count - 1);
        RebuildShowRows();
    }

    public void MoveInShow(int from, int to)
    {
        if (_current is null) return;
        _current.Ids = _editing.Move(_current.Ids, from, to);
        _showSelected = Math.Clamp(to > from ? to - 1 : to, 0, Math.Max(0, _current.Ids.Count - 1));
        RebuildShowRows();
    }

    public void RemoveFromShow(int index)
    {
        if (_current is null) return;
        _current.Ids = _editing.RemoveAt(_current.Ids, index);
        _showSelected = Math.Min(index, _current.Ids.Count - 1);
        RebuildShowRows();
    }

    private Control BuildEditor()
    {
        var editor = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var nameLabel = SectionLabel(_texts["deck.customShows.name"]);
        Grid.SetRow(nameLabel, 0);
        Grid.SetRow(_nameBox, 1);
        _nameBox.Margin = new Thickness(0, 0, 0, _metrics.SpacingMedium);

        var add = TextButton(_texts["deck.customShows.add"], () => AddToShow(_deckSelected, _current?.Ids.Count ?? 0));
        var remove = TextButton(_texts["deck.customShows.remove"], () => RemoveFromShow(_showSelected));
        var middle = new StackPanel { Spacing = _metrics.SpacingSmall, VerticalAlignment = VerticalAlignment.Center, Children = { add, remove } };

        var showHost = new Panel { Children = { new ScrollViewer { Content = _slidePanel }, _dropLayer } };
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), ColumnSpacing = _metrics.SpacingMedium };
        var left = Column(_texts["deck.customShows.inDeck"], new ScrollViewer { Content = _deckPanel });
        var right = Column(_texts["deck.customShows.slides"], showHost);
        Grid.SetColumn(middle, 1);
        Grid.SetColumn(right, 2);
        columns.Children.Add(left);
        columns.Children.Add(middle);
        columns.Children.Add(right);
        columns.AddHandler(PointerPressedEvent, OnPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        columns.AddHandler(PointerMovedEvent, OnMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        columns.AddHandler(PointerReleasedEvent, OnReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        Grid.SetRow(columns, 2);
        editor.Children.Add(nameLabel);
        editor.Children.Add(_nameBox);
        editor.Children.Add(columns);
        return editor;
    }

    private Control Column(string title, Control body)
    {
        var frame = new Border
        {
            Child = body,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Padding = new Thickness(4),
            MinHeight = 260,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border).Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(frame, 1);
        grid.Children.Add(SectionLabel(title));
        grid.Children.Add(frame);
        return grid;
    }

    private void SelectShow(int index)
    {
        _current = index >= 0 && index < _shows.Count ? _shows[index] : null;
        _editorHost.Children[0].IsVisible = _current is null;
        _editorHost.Children[1].IsVisible = _current is not null;

        if (_current is null) return;

        _nameBox.Text = _current.Name;
        _showSelected = -1;
        RebuildShowRows();
        if (_showList.SelectedIndex != index) _showList.SelectedIndex = index;
    }

    private void BuildDeckRows()
    {
        _deckPanel.Children.Clear();
        for (var i = 0; i < _deck.Slides.Count; i++)
        {
            var index = i;
            var row = Row(_deck.Slides[i]);
            row.Click += (_, _) =>
            {
                if (_dragging) return;
                _deckSelected = index;
                Mark(_deckPanel, index);
            };
            row.DoubleTapped += (_, _) => AddToShow(index, _current?.Ids.Count ?? 0);
            _deckPanel.Children.Add(row);
        }
        Mark(_deckPanel, _deckSelected);
    }

    private void RebuildShowRows()
    {
        var spare = _slidePanel.Children.OfType<SlideRow>()
            .GroupBy(row => row.SlideId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new Queue<SlideRow>(group), StringComparer.Ordinal);
        _slidePanel.Children.Clear();
        if (_current is null) return;
        foreach (var id in _current.Ids)
        {
            if (!_slidesById.TryGetValue(id, out var slide)) continue;
            _slidePanel.Children.Add(spare.TryGetValue(id, out var reusable) && reusable.Count > 0 ? reusable.Dequeue() : ShowRow(slide));
        }
        Mark(_slidePanel, _showSelected);
    }

    private SlideRow ShowRow(Slide slide)
    {
        var row = Row(slide);
        row.Click += (_, _) =>
        {
            if (_dragging) return;
            _showSelected = _slidePanel.Children.IndexOf(row);
            Mark(_slidePanel, _showSelected);
        };
        row.DoubleTapped += (_, _) => RemoveFromShow(_slidePanel.Children.IndexOf(row));
        return row;
    }

    private void Mark(Panel panel, int selected)
    {
        for (var index = 0; index < panel.Children.Count; index++)
            if (panel.Children[index] is SlideRow row) row.Mark(index == selected);
    }

    private SlideRow Row(Slide slide)
    {
        var ratio = _deck.Size.Width.Value > 0 ? (double)_deck.Size.Height.Value / _deck.Size.Width.Value : 9.0 / 16.0;
        var picture = new Border { Background = Brushes.White, Child = _pictures.Create(_deck, slide, 64, 64 * ratio) };
        var label = new TextBlock { Text = LabelFor(slide), FontSize = _metrics.FontSize, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = _metrics.SpacingSmall, Margin = new Thickness(4, 2) };
        Grid.SetColumn(label, 1);
        line.Children.Add(picture);
        line.Children.Add(label);
        var highlight = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            IsVisible = false,
        }.Themed(Border.BackgroundProperty, "PulseAccentSubtle").Themed(Border.BorderBrushProperty, "PulseAccent");
        var row = new SlideRow(slide.Id, line, highlight)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        Avalonia.Automation.AutomationProperties.SetName(row, label.Text);
        return row;
    }

    private int SlotAt(Point point)
    {
        var rows = _slidePanel.Children;
        for (var i = 0; i < rows.Count; i++)
            if (point.Y < rows[i].Bounds.Y + rows[i].Bounds.Height / 2) return i;
        return rows.Count;
    }

    private void OnPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _dragging = false;
        _drag = null;
        var inShow = e.GetPosition(_slidePanel);
        var showIndex = _slidePanel.Children.ToList().FindIndex(row => row.Bounds.Contains(inShow));
        if (showIndex >= 0) { _drag = (false, showIndex); _dragOrigin = inShow; return; }
        var inDeck = e.GetPosition(_deckPanel);
        var deckIndex = _deckPanel.Children.ToList().FindIndex(row => row.Bounds.Contains(inDeck));
        if (deckIndex >= 0) { _drag = (true, deckIndex); _dragOrigin = e.GetPosition(_slidePanel); }
    }

    private void OnMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_drag is null) return;
        var point = e.GetPosition(_slidePanel);
        if (!_dragging && Math.Abs(point.Y - _dragOrigin.Y) + Math.Abs(point.X - _dragOrigin.X) < 4) return;
        _dragging = true;
        var over = point.X >= 0 && point.X <= _slidePanel.Bounds.Width;
        _dropMarker.IsVisible = over;
        if (!over) return;
        var slot = SlotAt(point);
        var rows = _slidePanel.Children;
        _dropMarker.Width = _slidePanel.Bounds.Width;
        Canvas.SetTop(_dropMarker, slot < rows.Count ? rows[slot].Bounds.Y - 1 : rows.Count > 0 ? rows[^1].Bounds.Bottom : 0);
    }

    private void OnReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        var drag = _drag;
        var dragging = _dragging;
        _drag = null;
        _dropMarker.IsVisible = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _dragging = false);
        if (drag is not { } source || !dragging) return;
        var point = e.GetPosition(_slidePanel);
        if (point.X < 0 || point.X > _slidePanel.Bounds.Width) return;
        var slot = SlotAt(point);
        if (source.FromDeck) AddToShow(source.Index, slot);
        else MoveInShow(source.Index, slot);
    }

    public void AddShow()
    {
        var name = UniqueName(_texts["deck.customShows.defaultName"]);
        var show = new WorkingShow { Name = name, Ids = [.. _deck.Slides.Select(slide => slide.Id)] };
        _shows.Add(show);
        RefreshList();
        SelectShow(_shows.Count - 1);
    }

    private void DeleteShow()
    {
        if (_current is null) return;
        var index = _shows.IndexOf(_current);
        _shows.Remove(_current);
        RefreshList();
        SelectShow(Math.Min(index, _shows.Count - 1));
    }

    private void PresentShow()
    {
        if (_current is null) return;
        var index = _shows.IndexOf(_current);
        var shows = Shows();
        if (index < 0 || shows[index].SlideIds.Count == 0) return;
        Completed?.Invoke(this, new CustomShowsResult(shows, shows[index]));
    }

    private void Confirm() => Completed?.Invoke(this, new CustomShowsResult(Shows(), null));

    private IReadOnlyList<CustomShow> Shows() =>
        _naming.Distinct([.. _shows.Select(s => s.ToShow())], _texts["deck.customShows.unnamed"]);

    private bool _refreshing;

    private void RefreshList()
    {
        var selected = _current is null ? _showList.SelectedIndex : _shows.IndexOf(_current);
        _refreshing = true;
        try
        {
            _showList.ItemsSource = _shows
                .Select(s => string.IsNullOrWhiteSpace(s.Name) ? _texts["deck.customShows.unnamed"] : s.Name)
                .ToList();
            if (selected >= 0 && selected < _shows.Count) _showList.SelectedIndex = selected;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private WorkingShow FromShow(CustomShow show) =>
        new() { Name = show.Name, Ids = [.. show.SlideIds.Where(_slidesById.ContainsKey)] };

    private string LabelFor(Slide slide)
    {
        var number = _numbers.GetValueOrDefault(slide.Id);
        var title = slide.Placeholder(PlaceholderRole.Title)?.Text.PlainText;
        return string.IsNullOrWhiteSpace(title) ? $"{number}. {_texts["deck.slide"]}" : $"{number}. {title}";
    }

    private string UniqueName(string baseName)
    {
        var name = baseName;
        var counter = 1;
        while (_shows.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{baseName} {++counter}";
        return name;
    }

    private Button TextButton(string text, Action action, bool accent = false)
    {
        var button = new Button
        {
            Content = text,
            FontSize = _metrics.FontSizeSmall,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        if (accent) button.Classes.Add("dialogPrimary");
        button.Click += (_, _) => action();
        return button;
    }

    private TextBlock SectionLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = _metrics.FontSizeSmall,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall),
    };
}
