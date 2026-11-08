using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Templates;

public sealed record TemplateCard(
    string Key, string Name, string ModuleKey, string ModuleName, string AccentHex, DateTime Modified, Func<IPrintableDocument?> Pages);

public sealed class TemplateThumbnailPainter(IPageCanvasFactory canvases) : IThumbnailPainter<TemplateCard>
{
    public Control Create(TemplateCard item, Size size)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new PageThumbnail(item.Pages, Color.Parse(item.AccentHex), Initial(item), canvases) { Width = size.Width, Height = size.Height };
    }

    public void Repaint(Control thumbnail, TemplateCard item) => thumbnail.InvalidateVisual();

    private string Initial(TemplateCard item) =>
        item.ModuleName.Length > 0 ? item.ModuleName[..1].ToUpper(CultureInfo.CurrentCulture) : string.Empty;
}

public sealed class TemplateGalleryView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const string AllKey = "all";

    private readonly IReadOnlyList<TemplateCard> _cards;
    private readonly ITextCatalog _texts;
    private readonly IGalleryNavigator _navigator;
    private readonly IThumbnailPainter<TemplateCard> _painter;
    private readonly IPageCanvasFactory _canvases;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ContentControl _galleryHost = new();
    private readonly ContentControl _previewHost = new() { Width = 260, Height = 300 };
    private readonly TextBlock _name;
    private readonly TextBlock _details;
    private readonly TextBlock _pages;
    private readonly Border _module;
    private readonly TextBlock _moduleText;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _confirm;
    private PageThumbnail? _large;
    private TemplateCard? _selected;

    public TemplateGalleryView(
        IReadOnlyList<TemplateCard> cards, ITextCatalog texts, IGalleryNavigator navigator, IPageCanvasFactory canvases,
        InterfaceDensity density = InterfaceDensity.Compact, IThumbnailPainter<TemplateCard>? painter = null)
    {
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _canvases = canvases ?? throw new ArgumentNullException(nameof(canvases));
        _painter = painter ?? new TemplateThumbnailPainter(canvases);
        _metrics = DesignTokens.For(density);

        var modules = cards.GroupBy(card => card.ModuleKey).Select(group => group.First()).ToList();
        Filter = new IconChoiceGroup(
            [new IconChoice(AllKey, string.Empty, texts["template.filter.all"]),
             .. modules.Select(card => new IconChoice(card.ModuleKey, string.Empty, card.ModuleName))],
            null, _metrics, texts["template.filter.module"], 0, showLabels: true);
        Filter.SelectionChanged += (_, _) => ShowGallery();

        _name = new TextBlock { FontSize = _metrics.FontSize + 2, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        _moduleText = new TextBlock { FontSize = _metrics.FontSizeSmall, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White };
        _module = new Border
        {
            Padding = new Thickness(7, 1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _moduleText,
        };
        _details = new TextBlock { FontSize = _metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _pages = new TextBlock { FontSize = _metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center, MinWidth = 60, TextAlignment = Avalonia.Media.TextAlignment.Center }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _previous = Small("‹", texts["template.preview.previous"], -1);
        _next = Small("›", texts["template.preview.next"], 1);

        var paging = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = _metrics.SpacingSmall };
        paging.Children.Add(_previous);
        paging.Children.Add(_pages);
        paging.Children.Add(_next);

        var preview = new StackPanel { Spacing = _metrics.SpacingSmall, Width = 280 };
        preview.Children.Add(new Border
        {
            Padding = new Thickness(_metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            Child = _previewHost,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken));
        preview.Children.Add(paging);
        preview.Children.Add(_name);
        preview.Children.Add(_module);
        preview.Children.Add(_details);

        var left = new DockPanel { LastChildFill = true };
        var filterRow = new Border { Margin = new Thickness(0, 0, 0, _metrics.SpacingMedium), Child = Filter };
        DockPanel.SetDock(filterRow, Dock.Top);
        left.Children.Add(filterRow);
        left.Children.Add(_galleryHost);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,20,Auto"), MinHeight = 420 };
        Grid.SetColumn(preview, 2);
        grid.Children.Add(left);
        grid.Children.Add(preview);
        Content = grid;

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["template.create"], Confirm, out _confirm, primary: true);

        ShowGallery();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public IconChoiceGroup Filter { get; }

    public ThumbnailGallery<TemplateCard>? Gallery { get; private set; }

    public TemplateCard? Selected => _selected;

    public PageThumbnail? LargePreview => _large;

    public void Confirm()
    {
        if (_selected is { } chosen) Completed?.Invoke(this, chosen);
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (Gallery?.TileOf(Math.Max(0, Gallery.SelectedIndex)) is { } tile) tile.Focus();
    }

    private void ShowGallery()
    {
        var key = Filter.SelectedKey ?? AllKey;
        var shown = _cards.Where(card => key == AllKey || card.ModuleKey == key).ToList();

        Gallery = new ThumbnailGallery<TemplateCard>(
            [.. shown.Select(card => new ThumbnailGalleryItem<TemplateCard>(
                card, card.Name, key == AllKey ? card.ModuleName : null, card.ModuleName))],
            _painter, _navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 128,
                ThumbnailHeight = 150,
                Filterable = true,
                FilterPlaceholder = _texts["template.search"],
                EmptyText = _texts["template.search.none"],
                Scrolls = true,
                MaxViewportHeight = 400,
                Columns = 4,
                AccessibleName = _texts["template.gallery"],
            }, _metrics);
        Gallery.SelectionChanged += (_, card) => Show(card);
        Gallery.Invoked += (_, card) =>
        {
            Show(card);
            Confirm();
        };

        _galleryHost.Content = Gallery;

        var initial = shown.Contains(_selected!) ? _selected : shown.FirstOrDefault();
        if (initial is not null) Gallery.Select(initial);
        Show(initial);
    }

    private void Show(TemplateCard? card)
    {
        _selected = card;
        _confirm.IsEnabled = card is not null;

        if (card is null)
        {
            _large = null;
            _previewHost.Content = null;
            _name.Text = string.Empty;
            _module.IsVisible = false;
            _details.Text = string.Empty;
            _pages.Text = string.Empty;
            _previous.IsEnabled = _next.IsEnabled = false;
            return;
        }

        _large = new PageThumbnail(card.Pages, Color.Parse(card.AccentHex), card.ModuleName.Length > 0 ? card.ModuleName[..1] : string.Empty, _canvases);
        AutomationProperties.SetName(_large, card.Name);
        _previewHost.Content = _large;
        _name.Text = card.Name;
        _module.IsVisible = true;
        _module.Background = new SolidColorBrush(Color.Parse(card.AccentHex));
        _moduleText.Text = card.ModuleName;
        _details.Text = _texts.Format("template.modified", card.Modified.ToString("g", CultureInfo.CurrentCulture));
        Turn(0);
    }

    private void Turn(int direction)
    {
        if (_large is null) return;

        _large.Page += direction;
        var count = _large.PageCount;
        _pages.Text = count > 0 ? _texts.Format("template.preview.page", _large.Page + 1, count) : _texts["template.preview.none"];
        _previous.IsEnabled = _large.Page > 0;
        _next.IsEnabled = _large.Page < count - 1;
    }

    private Button Small(string caption, string name, int direction)
    {
        var button = new Button { Content = caption, Padding = new Thickness(10, 0), MinHeight = 22 };
        button.Classes.Add("ribbon");
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        button.Click += (_, _) => Turn(direction);
        return button;
    }
}
