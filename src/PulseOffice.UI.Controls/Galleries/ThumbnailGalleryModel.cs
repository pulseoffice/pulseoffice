using Avalonia;
using Avalonia.Controls;

namespace PulseOffice.UI.Controls.Galleries;

public interface IThumbnailPainter<in T>
{
    Control Create(T item, Size size);

    void Repaint(Control thumbnail, T item);
}

public sealed record ThumbnailGalleryItem<T>(T Value, string Caption, string? Group = null, string? Keywords = null);

public enum GalleryMove
{
    Left,
    Right,
    Up,
    Down,
    First,
    Last,
}

public sealed record ThumbnailGalleryOptions
{
    public double TileWidth { get; init; } = 112;

    public double ThumbnailHeight { get; init; } = 66;

    public double ThumbnailWidth { get; init; } = double.NaN;

    public Thickness TilePadding { get; init; } = new(4);

    public double CaptionGap { get; init; } = 4;

    public double FrameRadius { get; init; } = 4;

    public bool FrameBorder { get; init; } = true;

    public double? Spacing { get; init; }

    public int? Columns { get; init; }

    public bool Scrolls { get; init; }

    public double MaxViewportHeight { get; init; } = 420;

    public int VirtualizeAbove { get; init; } = 150;

    public bool Filterable { get; init; }

    public string FilterPlaceholder { get; init; } = string.Empty;

    public string EmptyText { get; init; } = string.Empty;

    public string AccessibleName { get; init; } = string.Empty;
}

public sealed record GallerySection(string? Header, IReadOnlyList<int> Positions);

public sealed record GalleryRow(string? Header, IReadOnlyList<int> Positions);
