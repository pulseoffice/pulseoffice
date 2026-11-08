using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record ImageExportResult(int Width, bool CurrentOnly);

public sealed record ImageExportServices(IDeckSlidePictures Pictures, IMeasureUnits Units, IIconCatalog Icons, ITextCatalog Texts);

public sealed class ImageExportView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double PreviewWidth = 280;

    private readonly ImageExportServices _services;
    private readonly double _ratio;
    private readonly MeasureField _width;
    private readonly MeasureField _height;
    private readonly IconChoiceGroup _range;
    private bool _syncing;

    public ImageExportView(Presentation deck, Slide current, int initialWidth, ImageExportServices services)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(current);
        _services = services ?? throw new ArgumentNullException(nameof(services));
        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        _ratio = deck.Size.Width.Value > 0 ? (double)deck.Size.Height.Value / deck.Size.Width.Value : 9.0 / 16.0;

        _width = Pixels(initialWidth, Texts["deck.export.images.width"], metrics);
        _height = Pixels(Math.Round(initialWidth * _ratio), Texts["deck.export.images.height"], metrics);
        _width.ValueCommitted += (_, value) => Link(_height, value * _ratio);
        _height.ValueCommitted += (_, value) => Link(_width, value / _ratio);

        _range = new IconChoiceGroup(
            [
                new IconChoice("all", "deck.setup.rangeAll", Texts["deck.export.images.all"]),
                new IconChoice("current", "deck.slideSize.landscape", Texts["deck.export.images.current"]),
            ],
            services.Icons, metrics, Texts["deck.export.images.range"], 0, showLabels: true);

        var size = new DialogSection(Texts["deck.export.images.size"], metrics);
        var widthRow = size.Add(Texts["deck.export.images.width"], _width);
        if (widthRow.Label is { } scrubWidth) _width.AttachScrubber(scrubWidth);
        var heightRow = size.Add(Texts["deck.export.images.height"], _height);
        if (heightRow.Label is { } scrubHeight) _height.AttachScrubber(scrubHeight);
        size.Add(null, new TextBlock { Text = Texts["deck.export.images.locked"], FontSize = metrics.FontSizeSmall }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary), DialogRowKind.Indented);
        var range = new DialogSection(Texts["deck.export.images.range"], metrics);
        range.Add(null, _range, DialogRowKind.Wide, editorWidth: 320);
        range.Add(Texts["deck.export.images.format"], new TextBlock { Text = "PNG", VerticalAlignment = VerticalAlignment.Center });

        var layout = new TwoPaneDialogLayout(metrics)
        {
            Preview = new Border
            {
                Background = Brushes.White,
                Child = services.Pictures.Create(deck, current, PreviewWidth, PreviewWidth * _ratio),
            },
        };
        layout.AddSection(size);
        layout.AddSection(range);
        Content = layout;

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts["deck.export.images.export"], () => Completed?.Invoke(this, Result()), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 720;

    public MeasureField WidthField => _width;

    public MeasureField HeightField => _height;

    public IconChoiceGroup Range => _range;

    private ITextCatalog Texts => _services.Texts;

    public ImageExportResult Result() => new((int)Math.Round(_width.Value), _range.SelectedIndex == 1);

    private void Link(MeasureField other, double value)
    {
        if (_syncing) return;
        _syncing = true;
        other.Value = Math.Round(value);
        _syncing = false;
    }

    private MeasureField Pixels(double value, string name, DesignTokens.Metrics metrics) =>
        new(new MeasureEntry(new MeasureOptions("px", 16, 8192, 1) { Integer = true, LargeStep = 100, Presets = [640, 1280, 1920, 2560, 3840] },
            _services.Units, CultureInfo.CurrentCulture), value, Texts, metrics, _services.Icons, name, boxWidth: 72)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
}
