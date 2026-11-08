using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fills;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record SlideBackgroundResult(SlideBackgroundChoice Choice, bool AllSlides);

public sealed record SlideBackgroundServices(
    IFillEditors Fills, ISlideBackgrounds Backgrounds, IDeckSlidePictures Pictures, ISlideFactory Slides, ITextCatalog Texts);

public sealed class SlideBackgroundView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double PreviewWidth = 320;

    private static readonly IReadOnlyList<FillPage> Pages =
        [FillPage.None, FillPage.Solid, FillPage.Gradient, FillPage.Pattern, FillPage.Hatch, FillPage.Image];

    private readonly SlideBackgroundServices _services;
    private readonly Presentation _deck;
    private readonly Slide _slide;
    private readonly FillEditor _editor;
    private readonly CheckBox _hideGraphics = new();
    private readonly ContentControl _preview = new();
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);

    public SlideBackgroundView(Presentation deck, Slide slide, SlideBackgroundServices services)
    {
        _deck = deck ?? throw new ArgumentNullException(nameof(deck));
        _slide = slide ?? throw new ArgumentNullException(nameof(slide));
        _services = services ?? throw new ArgumentNullException(nameof(services));

        _editor = services.Fills.Create(services.Backgrounds.Current(slide), new FillEditorOptions(Pages) { ShowPreview = false }, _metrics);
        _editor.FillChanged += (_, _) => Refresh();
        _hideGraphics.Content = Texts["deck.background.hideGraphics"];
        _hideGraphics.IsChecked = !slide.ShowMasterObjects;
        _hideGraphics.IsCheckedChanged += (_, _) => Refresh();

        var caption = new TextBlock
        {
            Text = Texts["deck.motion.preview"].ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var frame = new Border
        {
            Child = _preview,
            Padding = new Thickness(_metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken).Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var right = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            Width = PreviewWidth + 2 * _metrics.SpacingMedium + 2,
            Children = { caption, frame, _hideGraphics },
        };

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingLarge };
        Grid.SetColumn(right, 1);
        layout.Children.Add(_editor);
        layout.Children.Add(right);
        Content = layout;

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts["deck.background.reset"], () => Completed?.Invoke(this, new SlideBackgroundResult(
                services.Backgrounds.Choose(ShapeFill.Default with { Kind = ShapeFillKind.None }, false), false)))
            .WithAction(Texts["deck.motion.applyAll"], () => Completed?.Invoke(this, new SlideBackgroundResult(Choice(), true)))
            .WithAction(Texts["deck.motion.apply"], () => Completed?.Invoke(this, new SlideBackgroundResult(Choice(), false)), primary: true);

        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 860;

    public FillEditor Editor => _editor;

    public CheckBox HideGraphics => _hideGraphics;

    public SlidePicture? Preview => _preview.Content as SlidePicture;

    private ITextCatalog Texts => _services.Texts;

    public SlideBackgroundChoice Choice() => _services.Backgrounds.Choose(_editor.Fill, _hideGraphics.IsChecked == true);

    private void Refresh()
    {
        var sample = _services.Slides.Clone(_slide, _slide.Id);
        var choice = Choice();
        sample.BackgroundFill = choice.Image is null ? choice.Fill : null;
        sample.BackgroundImage = choice.Image;
        sample.BackgroundLayout = choice.Layout;
        sample.ShowMasterObjects = !choice.HideGraphics;
        _preview.Content = _services.Pictures.Create(_deck, sample, PreviewWidth, PreviewWidth * _deck.Size.Height.Value / Math.Max(1, _deck.Size.Width.Value),
            new SlidePictureOptions { Master = _deck.MasterOf(_slide), Theme = _deck.ThemeOf(_slide) });
    }
}
