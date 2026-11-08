using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Shapes;

public sealed class AssetPickerView : UserControl, IDialogContent
{
    private const int MaximumShown = 120;

    public event EventHandler<object?>? Completed;

    private readonly IAssetLibrary _library;
    private readonly IVectorOutlineReader _outlines;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly WrapPanel _results = new();
    private readonly TextBlock _summary = new();
    private readonly ComboBox _packs = new();
    private readonly IReadOnlyList<string> _packIds;
    private int _generation;

    public AssetPickerView(
        IAssetLibrary library, ITextCatalog texts,
        IVectorOutlineReader? outlines = null,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(texts);

        _library = library;
        _texts = texts;
        _outlines = outlines ?? new VectorOutlineReader();
        _metrics = DesignTokens.For(density);

        var search = new TextBox
        {
            PlaceholderText = texts["asset.search"],
            FontSize = _metrics.FontSize,
        };

        Avalonia.Automation.AutomationProperties.SetName(search, texts["asset.search"]);
        Avalonia.Automation.AutomationProperties.SetName(_packs, texts["asset.allPacks"]);
        search.TextChanged += (_, _) => _ = ShowAsync(search.Text ?? string.Empty);

        var packs = library.Packs.ToList();
        _packIds = [.. packs.Select(pack => pack.Id)];
        _packs.ItemsSource = new[] { texts["asset.allPacks"] }
            .Concat(packs.Select(pack => pack.DisplayName + " — " + pack.License.SpdxId))
            .ToList();

        _packs.SelectedIndex = 0;
        _packs.FontSize = _metrics.FontSize;
        _packs.SelectionChanged += (_, _) => _ = ShowAsync(search.Text ?? string.Empty);

        _summary.FontSize = _metrics.FontSizeSmall;
        _summary.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children = { search, _packs },
                },
                new ScrollViewer
                {
                    Content = _results,
                    Height = 320,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                },
                _summary,
            },
        };

        Grid.SetColumn(_packs, 1);
        _packs.Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0);

        Searched = ShowAsync(string.Empty);
    }

    public Task Searched { get; private set; }

    public int ShownCount => _results.Children.Count;

    public async Task ShowAsync(string query)
    {
        var generation = ++_generation;
        var packId = _packs.SelectedIndex > 0 && _packs.SelectedIndex <= _packIds.Count
            ? _packIds[_packs.SelectedIndex - 1]
            : null;

        var found = await _library.SearchAsync(query, packId: packId).ConfigureAwait(true);
        if (generation != _generation) return;

        _results.Children.Clear();

        foreach (var asset in found.Take(MaximumShown))
            _results.Children.Add(Tile(asset));

        _summary.Text = found.Count > MaximumShown
            ? _texts.Format("asset.tooMany", found.Count, MaximumShown)
            : _texts.Format("asset.found", found.Count);
    }

    private Control Tile(VectorAsset asset)
    {
        var button = new Button
        {
            Content = new AssetPreview(_outlines, asset) { Width = 32, Height = 32 },
            Margin = new Thickness(_metrics.SpacingSmall / 2),
            Padding = new Thickness(_metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            [ToolTip.TipProperty] = asset.Name,
        };

        Avalonia.Automation.AutomationProperties.SetName(button, asset.Name);
        button.Click += (_, _) => Completed?.Invoke(this, asset);
        return button;
    }

    private sealed class AssetPreview : Control
    {
        private readonly VectorDrawing _drawing;

        public AssetPreview(IVectorOutlineReader outlines, VectorAsset asset) =>
            _drawing = outlines.Read(asset.SvgContent ?? string.Empty);

        public override void Render(DrawingContext context)
        {
            if (_drawing.IsEmpty) return;

            var side = Math.Min(Bounds.Width, Bounds.Height);
            if (side <= 0) return;

            var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;

            using (context.PushTransform(Matrix.CreateTranslation(
                (Bounds.Width - side) / 2, (Bounds.Height - side) / 2)))
            {
                foreach (var path in _drawing.Scaled(side, side).Paths)
                    context.DrawGeometry(
                        path.Paint.FillHex is null ? null : accent,
                        path.Paint.StrokeHex is null
                            ? null
                            : new Pen(accent, Math.Max(0.8, path.Paint.StrokeWidth * side))
                            {
                                LineCap = PenLineCap.Round,
                                LineJoin = PenLineJoin.Round,
                            },
                        ShapeRenderer.Build(path.Outline));
            }
        }
    }
}
