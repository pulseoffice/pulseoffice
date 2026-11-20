using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Conditional;

public sealed class ConditionalIconChoicePainter(ConditionalPainting painting) : IThumbnailPainter<ConditionalIconChoice>
{
    public Control Create(ConditionalIconChoice item, Size size) => new ChoiceThumbnail(painting, item) { Width = size.Height, Height = size.Height };

    public void Repaint(Control thumbnail, ConditionalIconChoice item) => thumbnail.InvalidateVisual();

    private sealed class ChoiceThumbnail(ConditionalPainting painting, ConditionalIconChoice choice) : Control
    {
        public override void Render(DrawingContext context)
        {
            if (!choice.IsNone) painting.Icon(context, new Rect(Bounds.Size).Deflate(1), choice);
        }
    }
}

public sealed class ConditionalIconPicker : UserControl
{
    private readonly ThumbnailGallery<ConditionalIconChoice> _gallery;
    private readonly ContentControl _face = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Func<ConditionalIconChoice, Control> _faceOf;
    private readonly Flyout _flyout;

    public ConditionalIconPicker(
        ConditionalPainting painting, IGalleryNavigator navigator, ITextCatalog texts, DesignTokens.Metrics metrics,
        ConditionalIconChoice current, string accessibleName, Func<ConditionalIconChoice, Control> faceOf)
    {
        ArgumentNullException.ThrowIfNull(painting);
        ArgumentNullException.ThrowIfNull(texts);
        _faceOf = faceOf ?? throw new ArgumentNullException(nameof(faceOf));
        _gallery = new ThumbnailGallery<ConditionalIconChoice>(
            Items(painting, texts),
            new ConditionalIconChoicePainter(painting),
            navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 30,
                ThumbnailHeight = 20,
                ThumbnailWidth = 20,
                TilePadding = new Thickness(2),
                ShowsCaptions = false,
                Columns = 5,
                Scrolls = true,
                MaxViewportHeight = 320,
                FrameRadius = metrics.RadiusTight,
                FrameBorder = false,
                AccessibleName = accessibleName,
            },
            metrics);
        _gallery.Select(current);
        if (_gallery.SelectedIndex < 0) _gallery.Select(0);
        _face.Content = faceOf(Selected);

        _flyout = new Flyout { Content = _gallery, Placement = PlacementMode.BottomEdgeAlignedLeft };
        _gallery.SelectionChanged += (_, picked) => Pick(picked);
        _gallery.Invoked += (_, picked) =>
        {
            Pick(picked);
            _flyout.Hide();
        };
        _gallery.AddHandler(Button.ClickEvent, (_, _) => _flyout.Hide());

        var button = new Button
        {
            Content = _face,
            MinWidth = 64,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Flyout = _flyout,
        };
        AutomationProperties.SetName(button, accessibleName);
        AutomationProperties.SetName(this, accessibleName);
        Content = button;
    }

    public event EventHandler<ConditionalIconChoice>? Picked;

    public ThumbnailGallery<ConditionalIconChoice> Gallery => _gallery;

    public ConditionalIconChoice Selected => _gallery.SelectedItem ?? new ConditionalIconChoice(ConditionalIconChoice.NoIcons, -1);

    private void Pick(ConditionalIconChoice picked)
    {
        _face.Content = _faceOf(picked);
        Picked?.Invoke(this, picked);
    }

    private List<ThumbnailGalleryItem<ConditionalIconChoice>> Items(ConditionalPainting painting, ITextCatalog texts)
    {
        var none = texts["conditional.icons.none"];
        var items = new List<ThumbnailGalleryItem<ConditionalIconChoice>>
        {
            new(new ConditionalIconChoice(ConditionalIconChoice.NoIcons, -1), none, none),
        };
        foreach (var name in painting.Icons.Names.Where(name => name != ConditionalIconChoice.NoIcons))
        {
            var group = texts.Contains($"conditional.iconSet.{name}") ? texts[$"conditional.iconSet.{name}"] : name;
            for (var index = 0; index < painting.Icons.Count(name); index++)
                items.Add(new(new ConditionalIconChoice(name, index), $"{group} {index + 1}", group));
        }
        return items;
    }
}
