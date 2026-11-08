using Avalonia;
using Avalonia.Controls;
using PulseOffice.Document.Pages;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Pages;

public enum PageNumberAlignment
{
    Left,
    Center,
    Right,
}

public sealed record PageNumberPosition(bool Top, PageNumberAlignment Alignment)
{
    public string Key => $"{(Top ? "top" : "bottom")}.{Alignment.ToString().ToLowerInvariant()}";
}

public sealed class PageNumberPositionPainter(IPageSketchLayout layout) : IThumbnailPainter<PageNumberPosition>
{
    public Control Create(PageNumberPosition item, Size size)
    {
        var preview = new PagePreview(layout) { Width = size.Width, Height = size.Height };
        Repaint(preview, item);
        return preview;
    }

    public void Repaint(Control thumbnail, PageNumberPosition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (thumbnail is not PagePreview preview) return;
        var band = new PageSketchBand(2.4, 0.4,
            item.Alignment == PageNumberAlignment.Left ? "1" : null,
            item.Alignment == PageNumberAlignment.Center ? "1" : null,
            item.Alignment == PageNumberAlignment.Right ? "1" : null);
        preview.Sketch = new PageSketch(21, 29.7)
        {
            Margins = new PageSketchMargins(2, 1.5, 2, 1.5),
            Header = item.Top ? band : null,
            Footer = item.Top ? null : band,
        };
    }
}

public interface IPageNumberPositionGalleries
{
    ThumbnailGallery<PageNumberPosition> Create(PageNumberPosition initial, DesignTokens.Metrics metrics);

    IReadOnlyList<PageNumberPosition> Positions { get; }
}

public sealed class PageNumberPositionGalleries(IPageSketchLayout layout, IGalleryNavigator navigator, ITextCatalog texts) : IPageNumberPositionGalleries
{
    public IReadOnlyList<PageNumberPosition> Positions { get; } =
    [
        .. new[] { true, false }.SelectMany(top => Enum.GetValues<PageNumberAlignment>().Select(alignment => new PageNumberPosition(top, alignment))),
    ];

    public ThumbnailGallery<PageNumberPosition> Create(PageNumberPosition initial, DesignTokens.Metrics metrics)
    {
        var gallery = new ThumbnailGallery<PageNumberPosition>(
            [.. Positions.Select(position => new ThumbnailGalleryItem<PageNumberPosition>(position, texts[$"page.number.{position.Key}"],
                texts[position.Top ? "page.number.group.top" : "page.number.group.bottom"]))],
            new PageNumberPositionPainter(layout), navigator,
            new ThumbnailGalleryOptions { TileWidth = 84, ThumbnailHeight = 92, Columns = 3, AccessibleName = texts["page.number.gallery"] },
            metrics);
        gallery.Select(initial);
        return gallery;
    }
}

public sealed record PageNumberPositionField(string Key, string Label, PageNumberPosition Initial, IPageNumberPositionGalleries Galleries) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var gallery = Galleries.Create(Initial, metrics);
        return new FormFieldEditor(gallery, () => gallery.SelectedItem ?? Initial, Labelled: false) { Observe = changed => gallery.SelectionChanged += (_, _) => changed() };
    }
}
