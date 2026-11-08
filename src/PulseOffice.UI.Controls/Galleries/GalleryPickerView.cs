using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Galleries;

public sealed class GalleryPickerView<T> : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ThumbnailGallery<T> _gallery;
    private readonly DialogFooter _footer;
    private T? _selected;

    public GalleryPickerView(
        string explanation, IReadOnlyList<ThumbnailGalleryItem<T>> items, IThumbnailPainter<T> painter, IGalleryNavigator navigator,
        ThumbnailGalleryOptions options, ITextCatalog texts, T? current = default, Control? extra = null,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);

        _gallery = new ThumbnailGallery<T>(items, painter, navigator, options, metrics);
        var index = current is null ? -1 : _gallery.IndexOf(current);
        if (index < 0 && items.Count > 0) index = 0;
        if (index >= 0)
        {
            _gallery.Select(index);
            _selected = items[index].Value;
        }

        _gallery.SelectionChanged += (_, item) => _selected = item;
        _gallery.Invoked += (_, item) =>
        {
            _selected = item;
            Confirm();
        };

        Loaded += (_, _) => Dispatcher.UIThread.Post(
            () => _gallery.TileOf(Math.Max(0, _gallery.SelectedIndex))?.Focus(Avalonia.Input.NavigationMethod.Directional),
            DispatcherPriority.Input);

        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], Confirm, primary: true);

        var layout = new StackPanel { Spacing = metrics.SpacingMedium };
        if (explanation.Length > 0)
            layout.Children.Add(new TextBlock { Text = explanation, FontSize = metrics.FontSize, TextWrapping = TextWrapping.Wrap }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        layout.Children.Add(_gallery);
        if (extra is not null) layout.Children.Add(extra);
        Content = layout;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public ThumbnailGallery<T> Gallery => _gallery;

    public T? Selected => _selected;

    public void Confirm()
    {
        if (_selected is null) return;
        Completed?.Invoke(this, _selected);
    }
}
