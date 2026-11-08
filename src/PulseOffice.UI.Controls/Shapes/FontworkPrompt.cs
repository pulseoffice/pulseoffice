using PulseOffice.Drawing.Fontwork;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes.Fontwork;
using PulseOffice.UI.Icons;

namespace PulseOffice.UI.Controls.Shapes;

public interface IFontworkPrompt
{
    Task<FontworkObject?> CreateAsync(string titleKey, CancellationToken ct = default);

    Task<FontworkWarp?> ChooseWarpAsync(string titleKey, FontworkWarp current, CancellationToken ct = default);
}

public sealed class FontworkPrompt(
    IDialogService dialogs,
    Fonts.IFontPickerFactory fonts,
    IFontworkRenderer renderer,
    IFontworkLooks looks,
    IIconCatalog icons,
    ITextCatalog texts,
    Galleries.IGalleryNavigator navigator) : IFontworkPrompt
{
    private const string DefaultFont = "Inter";

    private FontworkGalleryServices Services => new(renderer, fonts, icons, texts, navigator);

    private FontworkDesigner Designer() => new(looks.All, texts["fontwork.sample"], DefaultFont);

    public async Task<FontworkObject?> CreateAsync(string titleKey, CancellationToken ct = default)
    {
        var designer = await dialogs.ShowAsync<FontworkDesigner>(
            new FontworkGalleryView(Designer(), Services),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 800, AccessibleDescription = texts["fontwork.gallery.explanation"] }, ct).ConfigureAwait(true);
        return designer is { CanConfirm: true } ? designer.Build(Guid.NewGuid().ToString("N")) : null;
    }

    public async Task<FontworkWarp?> ChooseWarpAsync(string titleKey, FontworkWarp current, CancellationToken ct = default)
    {
        var start = Designer();
        start.Warp = current;
        var designer = await dialogs.ShowAsync<FontworkDesigner>(
            new FontworkGalleryView(start, Services, FontworkGalleryMode.Warp),
            new DialogOptions { TitleKey = titleKey, PreferredWidth = 540 }, ct).ConfigureAwait(true);
        return designer?.Warp;
    }
}
