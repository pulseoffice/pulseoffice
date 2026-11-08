using PulseOffice.Foundation.Colors;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Objects;

public sealed class ColorReplacerView : UserControl, IDialogContent, IDialogFooterOwner
{
    private const int RuleCount = 4;

    private readonly Bitmap? _original;
    private readonly IBitmapPixels _pixels;
    private readonly Image _preview = new() { Stretch = Stretch.Uniform };
    private readonly List<RuleRow> _rows = [];
    private readonly DesignTokens.Metrics _metrics;
    private readonly ITextCatalog _texts;
    private int _active;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public ColorReplacerView(byte[] image, IBitmapPixels pixels, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);
        _original = pixels.Decode(image);

        for (var i = 0; i < RuleCount; i++) _rows.Add(new RuleRow(i, this));

        var frame = new Border
        {
            Child = _preview,
            Height = 340,
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Cursor = new Cursor(StandardCursorType.Cross),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
        frame.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(frame).Properties.IsLeftButtonPressed) Pick(e.GetPosition(_preview));
        };

        var left = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                frame,
                new TextBlock { Text = texts["colorReplacer.hint"], FontSize = _metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            },
        };

        var right = new StackPanel { Spacing = _metrics.SpacingMedium };
        foreach (var row in _rows) right.Children.Add(row.View);
        Grid.SetColumn(right, 1);

        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,330"),
            ColumnSpacing = _metrics.SpacingLarge,
            Children = { left, right },
        };

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Rules()), primary: true);

        Activate(0);
        Refresh();
    }

    public IReadOnlyList<ColorReplacementRule> Rules() =>
        [.. _rows.Select(row => row.Rule()).OfType<ColorReplacementRule>()];

    private void Activate(int index)
    {
        _active = index;
        foreach (var row in _rows) row.Highlight(row.Index == index);
    }

    private void Refresh()
    {
        if (_original is null) return;
        var rules = Rules();
        _preview.Source = rules.Count == 0
            ? _original
            : _pixels.Edit(_original, image => new ColorReplacementFilter(rules).Apply(image));
    }

    private void Pick(Point point)
    {
        if (_original is null || _preview.Bounds.Width <= 0) return;

        var size = _original.Size;
        var scale = Math.Min(_preview.Bounds.Width / size.Width, _preview.Bounds.Height / size.Height);
        var offsetX = (_preview.Bounds.Width - size.Width * scale) / 2;
        var offsetY = (_preview.Bounds.Height - size.Height * scale) / 2;
        var x = (int)((point.X - offsetX) / scale * _original.PixelSize.Width / size.Width);
        var y = (int)((point.Y - offsetY) / scale * _original.PixelSize.Height / size.Height);
        if (x < 0 || y < 0 || x >= _original.PixelSize.Width || y >= _original.PixelSize.Height) return;

        uint colour = 0;
        _pixels.Edit(_original, image =>
        {
            var offset = (y * image.Width + x) * 4;
            colour = (uint)(image.Pixels[offset + 2] << 16 | image.Pixels[offset + 1] << 8 | image.Pixels[offset]);
        });

        _rows[_active].SetSource(colour);
        Activate(Math.Min(_active + 1, RuleCount - 1));
    }

    private sealed class RuleRow
    {
        private readonly ColorReplacerView _owner;
        private readonly CheckBox _enabled = new();
        private readonly ColorFieldControl _source;
        private readonly NumberBox _tolerance;
        private readonly ColorFieldControl _target;
        private readonly CheckBox _transparent;
        private readonly Border _card;

        public RuleRow(int index, ColorReplacerView owner)
        {
            Index = index;
            _owner = owner;
            var metrics = owner._metrics;

            _tolerance = new NumberBox(
                new NumberEntry(new NumericBounds(0, 100, 5), true, CultureInfo.CurrentCulture), 10, metrics, unit: "%", boxWidth: 44);
            Avalonia.Automation.AutomationProperties.SetName(_tolerance, owner._texts["colorReplacer.tolerance"]);
            ToolTip.SetTip(_tolerance, owner._texts["colorReplacer.tolerance"]);
            _source = new ColorFieldControl(string.Empty, allowNone: true, owner._texts, metrics) { Width = 120 };
            _target = new ColorFieldControl("#FFFFFF", allowNone: false, owner._texts, metrics) { Width = 120 };
            _transparent = new CheckBox { Content = owner._texts["colorReplacer.transparent"], FontSize = metrics.FontSizeSmall };

            _source.Changed += (_, _) => Changed(enable: true);
            _target.Changed += (_, _) => Changed(enable: false);
            _tolerance.ValueCommitted += (_, _) => Changed(enable: false);
            _enabled.IsCheckedChanged += (_, _) => owner.Refresh();
            _transparent.IsCheckedChanged += (_, _) =>
            {
                _target.IsEnabled = _transparent.IsChecked != true;
                Changed(enable: false);
            };

            var sourceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { _enabled, _source, _tolerance } };
            var targetRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = metrics.SpacingSmall,
                Margin = new Thickness(28, 0, 0, 0),
                Children =
                {
                    new TextBlock { Text = "→", VerticalAlignment = VerticalAlignment.Center },
                    _target,
                    _transparent,
                },
            };

            _card = new Border
            {
                Padding = new Thickness(metrics.SpacingSmall),
                CornerRadius = new CornerRadius(metrics.RadiusTight),
                BorderThickness = new Thickness(1),
                Child = new StackPanel { Spacing = metrics.SpacingSmall, Children = { sourceRow, targetRow } },
            };
            _card.PointerPressed += (_, _) => owner.Activate(Index);
            View = _card;
        }

        public int Index { get; }

        public Control View { get; }

        public void Highlight(bool active)
        {
            if (active) _card.Bind(Border.BorderBrushProperty, _card.GetResourceObservable("PulseAccent"));
            else _card.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        }

        public void SetSource(uint colour) => _source.Select(new ArgbColor(colour).ToHashedHex());

        public ColorReplacementRule? Rule() =>
            _enabled.IsChecked == true && Parse(_source.Value) is { } source
                ? new ColorReplacementRule(source, Math.Clamp(_tolerance.Value, 0, 100) / 100,
                    _transparent.IsChecked == true ? null : Parse(_target.Value) ?? 0xFFFFFF)
                : null;

        private void Changed(bool enable)
        {
            if (enable && Parse(_source.Value) is not null) _enabled.IsChecked = true;
            _owner.Refresh();
        }




        private uint? Parse(string? text)
            => ArgbColor.FromRgbHex(text?.Trim())?.Rgb;
    }
}
