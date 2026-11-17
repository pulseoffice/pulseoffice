using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shapes;

public sealed class ShapePickerView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double RailWidth = 184;
    private const double ViewportHeight = 388;

    private readonly ShapePickerModel _model;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IThumbnailPainter<ShapeChoice> _painter;
    private readonly StackPanel _rail;
    private readonly TextBox _search;
    private readonly ContentControl _host = new();
    private readonly TextBlock _heading = new();
    private readonly TextBlock _note;
    private readonly Button _insert;
    private readonly DialogFooter _footer;
    private readonly List<(ShapeSection? Section, Button Entry)> _entries = [];
    private ThumbnailGallery<ShapeChoice>? _gallery;

    public ShapePickerView(ShapePickerModel model, ITextCatalog texts, IIconCatalog icons, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(icons);
        _model = model;
        _texts = texts;
        _metrics = DesignTokens.For(density);
        _painter = new ShapeChoicePainter(new ShapeGeometry(), icons);

        _rail = new StackPanel { Spacing = 2 };
        Avalonia.Automation.AutomationProperties.SetName(_rail, texts["shape.picker.sections"]);
        _rail.AddHandler(KeyDownEvent, RailKeys, RoutingStrategies.Tunnel);
        AddEntry(null, texts["shape.picker.section.all"], model.Items(null, string.Empty).Count);
        foreach (var section in model.Sections) AddEntry(section, model.TitleOf(section), model.CountOf(section));

        _search = new TextBox { PlaceholderText = texts["shape.picker.search"] };
        Avalonia.Automation.AutomationProperties.SetName(_search, texts["shape.picker.search"]);
        _search.TextChanged += (_, _) => SearchChanged();
        _search.AddHandler(KeyDownEvent, SearchKeys, RoutingStrategies.Tunnel);

        _heading.FontSize = _metrics.FontSize + 2;
        _heading.FontWeight = FontWeight.SemiBold;
        _heading.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithNote(string.Empty, out _note)
            .WithAction(texts["shape.picker.insert"], Confirm, out _insert, primary: true);

        Content = Layout();
        Section = model.Sections.Contains(ShapeSection.Recent) ? ShapeSection.Recent : ShapeSection.Basic;

        Loaded += (_, _) => Dispatcher.UIThread.Post(() => _search.Focus(), DispatcherPriority.Input);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public double? PreferredWidth => 760;

    public ShapePickerModel Model => _model;

    public ThumbnailGallery<ShapeChoice>? Gallery => _gallery;

    public ShapeChoice? Selected { get; private set; }

    public string SearchText
    {
        get => _search.Text ?? string.Empty;
        set
        {
            if (_search.Text == value) return;
            _search.Text = value;
            SearchChanged();
        }
    }

    private void SearchChanged()
    {
        if (_search.Text is { Length: > 0 } && Section is not null) Section = null;
        else Show();
    }

    public ShapeSection? Section
    {
        get;
        set
        {
            field = value;
            foreach (var (section, entry) in _entries) entry.Classes.Set("checked", section == value);
            Show();
        }
    }

    public void Confirm()
    {
        if (Selected is not { } choice) return;
        Completed?.Invoke(this, choice);
    }

    private Control Layout()
    {
        var railFrame = new Border
        {
            Width = RailWidth,
            Padding = new Thickness(_metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Child = new ScrollViewer { Content = _rail, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall) };
        _heading.VerticalAlignment = VerticalAlignment.Center;
        _search.Width = 220;
        Grid.SetColumn(_search, 1);
        top.Children.Add(_heading);
        top.Children.Add(_search);

        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(_metrics.SpacingMedium, 0, 0, 0) };
        Grid.SetRow(_host, 1);
        body.Children.Add(top);
        body.Children.Add(_host);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Height = ViewportHeight + 48 };
        Grid.SetColumn(body, 1);
        layout.Children.Add(railFrame);
        layout.Children.Add(body);
        return layout;
    }

    private void AddEntry(ShapeSection? section, string title, int count)
    {
        var label = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        label.Children.Add(new TextBlock { Text = title, FontSize = _metrics.FontSize, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        var number = new TextBlock
        {
            Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(number, 1);
        label.Children.Add(number);

        var entry = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };
        entry.Classes.Add("ribbon");
        entry.Click += (_, _) => Section = section;
        Avalonia.Automation.AutomationProperties.SetName(entry, title);
        ToolTip.SetTip(entry, title);
        _entries.Add((section, entry));
        _rail.Children.Add(entry);
    }

    private void Show()
    {
        var section = Section;
        var query = SearchText;
        _heading.Text = section is { } chosen ? _model.TitleOf(chosen) : _texts["shape.picker.section.all"];

        var items = _model.Items(section, query)
            .Select(choice => new ThumbnailGalleryItem<ShapeChoice>(choice, _model.TitleOf(choice), section is null ? _model.TitleOf(choice.Section) : null))
            .ToList();

        _gallery = new ThumbnailGallery<ShapeChoice>(items, _painter, new GalleryNavigator(), new ThumbnailGalleryOptions
        {
            TileWidth = 84,
            ThumbnailHeight = 52,
            Scrolls = true,
            FrameBorder = false,
            EmptyText = _texts["shape.picker.empty"],
            AccessibleName = _heading.Text,
            VirtualizeAbove = 500,
        }, _metrics) { Height = ViewportHeight };
        _gallery.SelectionChanged += (_, choice) => Choose(choice);
        _gallery.Invoked += (_, choice) =>
        {
            Choose(choice);
            Confirm();
        };
        _host.Content = _gallery;

        var keep = Selected is { } current ? items.FindIndex(item => item.Value.Key == current.Key) : -1;
        var index = keep >= 0 ? keep : items.Count > 0 ? 0 : -1;
        _gallery.Select(index);
        Choose(index >= 0 ? items[index].Value : null);
    }

    private void Choose(ShapeChoice? choice)
    {
        Selected = choice;
        _insert.IsEnabled = choice is not null;
        _note.Text = choice is null ? string.Empty : _model.TitleOf(choice);
    }

    private void SearchKeys(object? sender, KeyEventArgs e)
    {
        if (_gallery is null) return;
        if (e.Key == Key.Down && _gallery.VisibleIndices.Count > 0)
        {
            _gallery.TileOf(Math.Max(0, _gallery.SelectedIndex))?.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Confirm();
            e.Handled = true;
        }
    }

    private void RailKeys(object? sender, KeyEventArgs e)
    {
        var step = e.Key switch { Key.Down => 1, Key.Up => -1, _ => 0 };
        if (step == 0) return;
        var current = _entries.FindIndex(entry => entry.Section == Section);
        var next = Math.Clamp(current + step, 0, _entries.Count - 1);
        Section = _entries[next].Section;
        _entries[next].Entry.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private sealed class ShapeChoicePainter(IShapeGeometry geometry, IIconCatalog icons) : IThumbnailPainter<ShapeChoice>
    {
        public Control Create(ShapeChoice item, Size size)
        {
            if (item.Shape is { } kind) return new ShapeThumbnail(geometry, kind) { Width = size.Width, Height = size.Height };
            var icon = item.IconKey is { } key ? icons.Find(key) : null;
            var host = new Border { Width = size.Width, Height = size.Height };
            if (icon is not null)
                host.Child = new IconView(icon, Math.Min(size.Height, size.Width) * 0.72)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }.Themed(IconView.ForegroundProperty, "PulseAccent");
            return host;
        }

        public void Repaint(Control thumbnail, ShapeChoice item)
        {
        }
    }

    private sealed class ShapeThumbnail(IShapeGeometry geometry, ShapeKind kind) : Control
    {
        public override void Render(DrawingContext context)
        {
            var side = Math.Min(Bounds.Width, Bounds.Height * 1.35) * 0.78;
            var height = kind == ShapeKind.Line ? side * 0.55 : Math.Min(Bounds.Height * 0.8, side * 0.74);
            var box = new Rect((Bounds.Width - side) / 2, (Bounds.Height - height) / 2, side, height);
            if (box.Width <= 0 || box.Height <= 0) return;

            var outline = ShapeRenderer.Build(geometry.Outline(kind).Scaled(box.Width, box.Height));
            var accent = this.FindResource("PulseAccent") as ISolidColorBrush ?? Brushes.SteelBlue;
            var fill = kind == ShapeKind.Line ? null : new SolidColorBrush(accent.Color, 0.22);
            using (context.PushTransform(Matrix.CreateTranslation(box.X, box.Y)))
                context.DrawGeometry(fill, new Pen(accent, 1.4) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round }, outline);
        }
    }
}
