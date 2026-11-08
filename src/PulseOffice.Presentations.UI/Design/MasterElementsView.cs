using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record MasterElementsServices(IDeckSlidePictures Pictures, ISlideFactory Slides, ITextCatalog Texts);

public sealed class MasterElementsView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double PreviewWidth = 360;

    private readonly MasterElementsServices _services;
    private readonly Presentation _deck;
    private readonly SlideMaster _master;
    private readonly CheckBox _date = new();
    private readonly CheckBox _footer = new();
    private readonly CheckBox _number = new();
    private readonly Panel _preview = new();

    public MasterElementsView(Presentation deck, SlideMaster master, MasterElementsServices services)
    {
        _deck = deck ?? throw new ArgumentNullException(nameof(deck));
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        var metrics = DesignTokens.For(InterfaceDensity.Compact);

        Check(_date, "deck.master.elements.date", master.Elements.Date);
        Check(_footer, "deck.master.elements.footer", master.Elements.Footer);
        Check(_number, "deck.master.elements.number", master.Elements.SlideNumber);

        var frame = new Border
        {
            Child = _preview,
            Padding = new Thickness(metrics.SpacingMedium),
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            BorderThickness = new Thickness(1),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken).Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        var checks = new StackPanel { Spacing = metrics.SpacingSmall, VerticalAlignment = VerticalAlignment.Center, Children = { _date, _footer, _number } };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingLarge };
        Grid.SetColumn(checks, 1);
        layout.Children.Add(frame);
        layout.Children.Add(checks);
        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = Texts.Format("deck.master.elements.explanation", master.Name), TextWrapping = TextWrapping.Wrap }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                layout,
            },
        };

        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(Texts["deck.motion.apply"], () => Completed?.Invoke(this, Elements), primary: true);
        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 660;

    public MasterElements Elements => new(_date.IsChecked == true, _footer.IsChecked == true, _number.IsChecked == true);

    public IReadOnlyList<FooterPlaceholderBox> Placeholders { get; private set; } = [];

    private ITextCatalog Texts => _services.Texts;

    private void Check(CheckBox box, string key, bool value)
    {
        box.Content = Texts[key];
        box.IsChecked = value;
        box.IsCheckedChanged += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var elements = Elements;
        var ratio = _deck.Size.Width.Value > 0 ? (double)_deck.Size.Height.Value / _deck.Size.Width.Value : 9.0 / 16.0;
        var height = PreviewWidth * ratio;
        var sample = _services.Slides.Create("master-preview", _master, SlideLayout.TitleAndContent);
        var chrome = new SlideChrome(
            elements.Date ? Texts["deck.master.elements.date"] : null,
            elements.Footer ? Texts["deck.master.elements.footer"] : null,
            elements.SlideNumber ? "‹#›" : null);
        var picture = _services.Pictures.Create(_deck, sample, PreviewWidth, height,
            new SlidePictureOptions { Master = _master, Theme = _master.Theme, Chrome = chrome });
        var overlay = new FooterPlaceholderOverlay(PreviewWidth, height, elements.Date, elements.Footer, elements.SlideNumber);
        Placeholders = overlay.Boxes;
        _preview.Children.Clear();
        _preview.Width = PreviewWidth;
        _preview.Height = height;
        _preview.Children.Add(new Border { Background = Brushes.White, Child = picture });
        _preview.Children.Add(overlay);
    }
}
