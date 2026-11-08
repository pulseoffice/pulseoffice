using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Themes;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record MasterGalleryItem(SlideMaster Master, string Caption, string? Group = null);

public sealed record MasterGalleryAction(string Key, string Label, bool Primary = false);

public sealed record MasterGalleryRequest(
    IReadOnlyList<MasterGalleryItem> Items, int Selected, string Explanation, IReadOnlyList<MasterGalleryAction> Actions, SlideSize Size)
{
    public string? NamePrompt { get; init; }

    public Func<MasterGalleryItem, string>? SuggestedName { get; init; }
}

public sealed record MasterGalleryResult(int Index, string Action, string Name);

public sealed record MasterGalleryServices(IThemePreviews Previews, IGalleryNavigator Navigator, ITextCatalog Texts);

public sealed class MasterThumbnailPainter(IThemePreviews previews, IReadOnlyList<MasterGalleryItem> items, SlideSize size) : IThumbnailPainter<int>
{
    public Control Create(int item, Size area) => previews.Create(items[item].Master.Theme, items[item].Master, size, area.Width, area.Height);

    public void Repaint(Control thumbnail, int item)
    {
    }
}

public sealed class MasterGalleryView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double TileWidth = 156;

    private readonly MasterGalleryRequest _request;
    private readonly ThumbnailGallery<int> _gallery;
    private readonly TextBox? _name;
    private string? _suggested;

    public MasterGalleryView(MasterGalleryRequest request, MasterGalleryServices services)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        ArgumentNullException.ThrowIfNull(services);
        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        var thumbnailWidth = TileWidth - 10;
        var ratio = request.Size.Width.Value > 0 ? (double)request.Size.Height.Value / request.Size.Width.Value : 9.0 / 16.0;
        _gallery = new ThumbnailGallery<int>(
            [.. request.Items.Select((item, index) => new ThumbnailGalleryItem<int>(index, item.Caption, item.Group))],
            new MasterThumbnailPainter(services.Previews, request.Items, request.Size),
            services.Navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = TileWidth,
                ThumbnailWidth = thumbnailWidth,
                ThumbnailHeight = Math.Round(thumbnailWidth * ratio),
                FrameRadius = metrics.RadiusTight,
                Columns = 4,
                Scrolls = true,
                MaxViewportHeight = 440,
                AccessibleName = request.Explanation,
            },
            metrics);
        _gallery.Select(Math.Clamp(request.Selected, 0, Math.Max(0, request.Items.Count - 1)));
        _gallery.SelectionChanged += (_, index) => Suggest(index);
        var primary = request.Actions.FirstOrDefault(action => action.Primary) ?? request.Actions[^1];
        _gallery.Invoked += (_, _) => Complete(primary.Key);

        var content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = request.Explanation, TextWrapping = TextWrapping.Wrap }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _gallery,
            },
        };

        if (request.NamePrompt is { } prompt)
        {
            _name = new TextBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(_name, prompt);
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = metrics.SpacingMedium,
                Children = { new TextBlock { Text = prompt, VerticalAlignment = VerticalAlignment.Center }, _name },
            };
            content.Children.Add(row);
            Suggest(_gallery.SelectedIndex);
        }

        Content = content;
        var footer = new DialogFooter().WithCancel(services.Texts["prompt.cancel"], () => Completed?.Invoke(this, null));
        foreach (var action in request.Actions)
            footer.WithAction(action.Label, () => Complete(action.Key), primary: action.Primary);
        Footer = footer;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 4 * (TileWidth + 8) + 60;

    public ThumbnailGallery<int> Gallery => _gallery;

    public TextBox? NameBox => _name;

    public void Complete(string action)
    {
        if (_gallery.SelectedIndex < 0) return;
        Completed?.Invoke(this, new MasterGalleryResult(_gallery.SelectedIndex, action, (_name?.Text ?? string.Empty).Trim()));
    }

    private void Suggest(int index)
    {
        if (_name is null || index < 0 || _request.SuggestedName is not { } suggest) return;
        var name = suggest(_request.Items[index]);
        if (string.IsNullOrWhiteSpace(_name.Text) || _name.Text == _suggested) _name.Text = name;
        _suggested = name;
    }
}
