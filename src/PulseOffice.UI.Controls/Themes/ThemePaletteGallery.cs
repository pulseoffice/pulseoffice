using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Document.Themes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Themes;

public interface IThemePaletteGallery
{
    Task<ThemePalette?> AskAsync(string titleKey, string explanationKey, string? currentKey, CancellationToken ct);

    GalleryPickerView<ThemePalette> Create(string explanation, string? currentKey);
}

public sealed class ThemePaletteGallery(IThemePalettes palettes, IDialogService dialogs, IGalleryNavigator navigator, ITextCatalog texts) : IThemePaletteGallery
{
    public async Task<ThemePalette?> AskAsync(string titleKey, string explanationKey, string? currentKey, CancellationToken ct) =>
        await dialogs.ShowAsync<ThemePalette>(
            Create(texts[explanationKey], currentKey),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 640 }, ct).ConfigureAwait(true);

    public GalleryPickerView<ThemePalette> Create(string explanation, string? currentKey)
    {
        var all = palettes.All;
        return new GalleryPickerView<ThemePalette>(
            explanation,
            [.. all.Select(theme => new ThumbnailGalleryItem<ThemePalette>(theme, texts["theme." + theme.Key], null, theme.FontFamily))],
            new ThemePalettePainter(),
            navigator,
            new ThumbnailGalleryOptions { TileWidth = 136, ThumbnailHeight = 84, Columns = 4, AccessibleName = texts["theme.choose"] },
            texts,
            all.FirstOrDefault(theme => string.Equals(theme.Key, currentKey, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class ThemePalettePainter : IThumbnailPainter<ThemePalette>
{
    public Control Create(ThemePalette item, Size size) => new ThemePaletteThumbnail(item) { Height = size.Height };

    public void Repaint(Control thumbnail, ThemePalette item) => thumbnail.InvalidateVisual();
}

public sealed class ThemePaletteThumbnail(ThemePalette palette) : Control
{
    private static readonly IBrush Paper = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IPen Edge = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)), 1);

    public ThemePalette Palette => palette;

    public override void Render(DrawingContext context)
    {
        var box = new Rect(Bounds.Size).Deflate(1);
        if (box.Width <= 8 || box.Height <= 8) return;
        context.DrawRectangle(Paper, Edge, box, 3, 3);

        var heading = new FormattedText("Aa", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily(palette.FontFamily), FontStyle.Normal, FontWeight.Bold),
            Math.Min(26, box.Height * 0.36), Brush(palette.HeadingColor));
        context.DrawText(heading, new Point(box.X + 8, box.Y + 6));

        var note = new Rect(box.Right - box.Width * 0.38 - 6, box.Y + 8, box.Width * 0.38, heading.Height - 4);
        context.DrawRectangle(Brush(palette.NoteBackground), null, note, 2, 2);

        var lines = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0x55, 0x4B, 0x55, 0x63)), 1.5);
        var top = box.Y + 10 + heading.Height;
        context.DrawLine(lines, new Point(box.X + 8, top), new Point(box.Right - 8, top));
        context.DrawLine(lines, new Point(box.X + 8, top + 5), new Point(box.X + box.Width * 0.62, top + 5));

        var accents = new[] { palette.HeadingColor, palette.Accent1, palette.Accent2, palette.Accent3 };
        var stripe = Math.Max(4, box.Height * 0.16);
        var width = (box.Width - 16) / accents.Length;
        for (var index = 0; index < accents.Length; index++)
            context.DrawRectangle(Brush(accents[index]), null, new Rect(box.X + 8 + index * width, box.Bottom - stripe - 6, width - 2, stripe));
    }

    private static IBrush Brush(string hex) =>
        Color.TryParse(hex, out var colour) ? new ImmutableSolidColorBrush(colour) : Brushes.Gray;
}
