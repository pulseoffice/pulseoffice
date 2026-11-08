using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.TextFormat;

public sealed record FeedPreview<TState>(
    Func<IReadOnlyDictionary<string, object>, TState> Read,
    Func<TState, Control> Render,
    PreviewBackdrop Backdrop = PreviewBackdrop.Surface,
    double Height = 150);

public interface ITextFormatPreviews
{
    LivePreviewPane Create<TState>(IFormValuesFeed feed, FeedPreview<TState> preview);
}

public sealed class TextFormatPreviews(IDebounceTimerFactory timers, ITextCatalog texts) : ITextFormatPreviews
{
    public LivePreviewPane Create<TState>(IFormValuesFeed feed, FeedPreview<TState> preview)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(preview);
        var pane = new LivePreviewPane(
            new FeedPreviewSource<TState>(feed, preview),
            timers,
            new LivePreviewOptions
            {
                Backdrop = preview.Backdrop,
                Height = preview.Height,
                UnavailableText = texts["textFormat.preview.unavailable"],
                AccessibleName = texts["form.preview"],
            },
            DesignTokens.For(InterfaceDensity.Compact))
        {
            Width = 260,
        };
        feed.Changed += (_, _) => pane.Invalidate();
        return pane;
    }

    private sealed class FeedPreviewSource<TState>(IFormValuesFeed feed, FeedPreview<TState> preview) : IPreviewSource
    {
        public Control? Render() => preview.Render(preview.Read(feed.Current));
    }
}
