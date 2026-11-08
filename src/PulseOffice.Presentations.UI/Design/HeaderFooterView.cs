using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Pages;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Design;

public sealed record HeaderFooterResult(HeaderFooterSettings Slides, NotesMaster Notes, HandoutMaster Handout);

public sealed record HeaderFooterServices(
    IDeckSlidePictures Pictures, IPageSketchLayout Pages, IIconCatalog Icons, ITextCatalog Texts,
    IGalleryNavigator Navigator, IHeaderFooterDateFormats DateFormats, PulseOffice.Text.Rich.ITextCultures Cultures);

public sealed class DateFormatPainter(IHeaderFooterDateFormats formats, DateTime moment, CultureInfo culture) : IThumbnailPainter<HeaderFooterDateFormat>
{
    public Control Create(HeaderFooterDateFormat item, Size size) => new Border
    {
        Width = size.Width,
        Height = size.Height,
        Padding = new Thickness(4),
        Child = new TextBlock
        {
            Text = formats.Render(item, moment, culture),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary),
    }.Themed(Border.BackgroundProperty, Palette.Keys.Surface);

    public void Repaint(Control thumbnail, HeaderFooterDateFormat item)
    {
    }
}

public sealed class HeaderFooterView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private const double PreviewWidth = 320;

    private readonly HeaderFooterServices _services;
    private readonly Presentation _deck;
    private readonly Slide _slide;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly CheckBox _date = new();
    private readonly IconChoiceGroup _dateMode;
    private readonly TextBox _fixedDate = new() { Width = 200 };
    private readonly ThumbnailGallery<HeaderFooterDateFormat> _dateFormat;
    private readonly CheckBox _number = new();
    private readonly CheckBox _footer = new();
    private readonly TextBox _footerText = new() { Width = 240 };
    private readonly CheckBox _skipTitle = new();
    private readonly CheckBox _notesHeader = new();
    private readonly TextBox _notesHeaderText = new() { Width = 240 };
    private readonly CheckBox _notesFooter = new();
    private readonly TextBox _notesFooterText = new() { Width = 240 };
    private readonly CheckBox _notesNumber = new();
    private readonly Panel _slidePreview = new();
    private readonly PagePreview _notesPreview;
    private readonly ContentControl _page = new();
    private readonly Control _slidesPage;
    private readonly Control _notesPage;

    public HeaderFooterView(Presentation deck, Slide slide, HeaderFooterServices services)
    {
        _deck = deck ?? throw new ArgumentNullException(nameof(deck));
        _slide = slide ?? throw new ArgumentNullException(nameof(slide));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        var current = deck.HeaderFooter;

        Check(_date, "deck.headerFooter.showDate", current.ShowDate);
        _dateMode = new IconChoiceGroup(
            [
                new IconChoice("auto", "deck.motion.advance.afterTime", Texts["deck.headerFooter.dateAuto"]),
                new IconChoice("fixed", "deck.headerFooter.fixed", Texts["deck.headerFooter.dateFixedShort"]),
            ],
            services.Icons, _metrics, Texts["deck.headerFooter.showDate"], current.DateIsFixed ? 1 : 0, showLabels: true);
        _dateMode.SelectionChanged += (_, _) => Refresh();
        _dateFormat = new ThumbnailGallery<HeaderFooterDateFormat>(
            [.. services.DateFormats.All.Select(info => new ThumbnailGalleryItem<HeaderFooterDateFormat>(info.Format, Texts[$"deck.headerFooter.format.{info.Key}"]))],
            new DateFormatPainter(services.DateFormats, DateTime.Now, Culture),
            services.Navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 104,
                ThumbnailHeight = 40,
                Columns = 3,
                Scrolls = true,
                MaxViewportHeight = 168,
                FrameRadius = _metrics.RadiusTight,
                AccessibleName = Texts["deck.headerFooter.dateFormat"],
            },
            _metrics);
        _dateFormat.Select(current.DateFormat);
        _dateFormat.SelectionChanged += (_, _) => Refresh();
        _fixedDate.Text = current.DateIsFixed && current.FixedDate.Length > 0 ? current.FixedDate : DateTime.Now.ToString("d", Culture);
        Edit(_fixedDate, "deck.headerFooter.fixedDate");
        Check(_number, "deck.headerFooter.showNumber", current.ShowSlideNumber);
        Check(_footer, "deck.headerFooter.showFooter", current.ShowFooter);
        _footerText.Text = current.FooterText;
        Edit(_footerText, "deck.headerFooter.footerText");
        Check(_skipTitle, "deck.headerFooter.skipTitle", !current.ShowOnTitleSlide);

        Check(_notesHeader, "deck.headerFooter.notesHeader", deck.NotesMaster.Header.Length > 0);
        _notesHeaderText.Text = deck.NotesMaster.Header;
        Edit(_notesHeaderText, "deck.headerFooter.notesHeader");
        Check(_notesFooter, "deck.headerFooter.notesFooter", deck.NotesMaster.Footer.Length > 0);
        _notesFooterText.Text = deck.NotesMaster.Footer;
        Edit(_notesFooterText, "deck.headerFooter.notesFooter");
        Check(_notesNumber, "deck.headerFooter.pageNumber", deck.NotesMaster.PageNumber);
        _notesPreview = new PagePreview(services.Pages) { Width = 200, Height = 260 };

        _slidesPage = SlidesPage();
        _notesPage = NotesPage();
        var tabs = new PageTabStrip(_metrics, [Texts["deck.headerFooter.slidesTab"], Texts["deck.headerFooter.notesTab"]]);
        tabs.SelectionChanged += (_, index) => _page.Content = index == 1 ? _notesPage : _slidesPage;
        Tabs = tabs;
        _page.Content = _slidesPage;

        Content = new StackPanel { Spacing = _metrics.SpacingMedium, Children = { tabs, _page } };
        Footer = new DialogFooter()
            .WithCancel(Texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithNote(Texts["deck.headerFooter.explanation"])
            .WithAction(Texts["deck.motion.applyAll"], () => Completed?.Invoke(this, Result()), primary: true);
        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 780;

    public PageTabStrip Tabs { get; }

    public CheckBox Date => _date;

    public IconChoiceGroup DateMode => _dateMode;

    public ThumbnailGallery<HeaderFooterDateFormat> DateFormat => _dateFormat;

    public CheckBox Number => _number;

    public CheckBox FooterBox => _footer;

    public TextBox FooterText => _footerText;

    public PagePreview NotesPreview => _notesPreview;

    public IReadOnlyList<FooterPlaceholderBox> Placeholders { get; private set; } = [];

    private ITextCatalog Texts => _services.Texts;

    public HeaderFooterSettings Settings() => new()
    {
        ShowDate = _date.IsChecked == true,
        DateIsFixed = _dateMode.SelectedIndex == 1,
        FixedDate = _dateMode.SelectedIndex == 1 ? _fixedDate.Text ?? string.Empty : string.Empty,
        DateFormat = _dateFormat.SelectedIndex >= 0 ? _dateFormat.SelectedItem : HeaderFooterDateFormat.ShortDate,
        ShowFooter = _footer.IsChecked == true,
        FooterText = _footerText.Text ?? string.Empty,
        ShowSlideNumber = _number.IsChecked == true,
        ShowOnTitleSlide = _skipTitle.IsChecked != true,
    };

    public HeaderFooterResult Result()
    {
        var header = _notesHeader.IsChecked == true ? _notesHeaderText.Text ?? string.Empty : string.Empty;
        var footer = _notesFooter.IsChecked == true ? _notesFooterText.Text ?? string.Empty : string.Empty;
        var number = _notesNumber.IsChecked == true;
        return new HeaderFooterResult(
            Settings(),
            _deck.NotesMaster with { Header = header, Footer = footer, PageNumber = number },
            _deck.Handout with { Header = header, Footer = footer, PageNumber = number });
    }

    private Control SlidesPage()
    {
        var section = new DialogSection(Texts["deck.headerFooter.include"], _metrics);
        section.Add(null, _date, DialogRowKind.Indented);
        section.Add(null, _dateMode, DialogRowKind.Indented);
        section.Add(null, _dateFormat, DialogRowKind.Indented);
        section.Add(null, _fixedDate, DialogRowKind.Indented);
        section.Add(null, _number, DialogRowKind.Indented);
        section.Add(null, _footer, DialogRowKind.Indented);
        section.Add(null, _footerText, DialogRowKind.Indented);
        var options = new DialogSection(Texts["form.section.options"], _metrics);
        options.Add(null, _skipTitle, DialogRowKind.Indented);
        var settings = new StackPanel { Spacing = _metrics.SpacingLarge, Children = { section, options } };
        Grid.SetIsSharedSizeScope(settings, true);
        return TwoColumns(settings, _slidePreview);
    }

    private Control NotesPage()
    {
        var section = new DialogSection(Texts["deck.headerFooter.includeNotes"], _metrics);
        section.Add(null, _notesHeader, DialogRowKind.Indented);
        section.Add(null, _notesHeaderText, DialogRowKind.Indented);
        section.Add(null, _notesFooter, DialogRowKind.Indented);
        section.Add(null, _notesFooterText, DialogRowKind.Indented);
        section.Add(null, _notesNumber, DialogRowKind.Indented);
        Grid.SetIsSharedSizeScope(section, true);
        return TwoColumns(section, _notesPreview);
    }

    private Control TwoColumns(Control settings, Control preview)
    {
        var frame = new Border
        {
            Child = preview,
            Padding = new Thickness(_metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Top,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken).Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingLarge };
        Grid.SetColumn(frame, 1);
        grid.Children.Add(settings);
        grid.Children.Add(frame);
        return grid;
    }

    private void Check(CheckBox box, string key, bool value)
    {
        box.Content = Texts[key];
        box.IsChecked = value;
        box.IsCheckedChanged += (_, _) => Refresh();
    }

    private void Edit(TextBox box, string key)
    {
        AutomationProperties.SetName(box, Texts[key]);
        box.TextChanged += (_, _) => Refresh();
    }

    private void Refresh()
    {
        if (_dateMode is null || _dateFormat is null) return;
        var settings = Settings();
        _dateMode.IsEnabled = settings.ShowDate;
        _fixedDate.IsEnabled = settings.ShowDate && settings.DateIsFixed;
        _dateFormat.IsVisible = !settings.DateIsFixed;
        _dateFormat.IsEnabled = settings.ShowDate;
        _dateFormat.Opacity = settings.ShowDate ? 1 : 0.45;
        _fixedDate.IsVisible = settings.DateIsFixed;
        _footerText.IsEnabled = settings.ShowFooter;
        _notesHeaderText.IsEnabled = _notesHeader.IsChecked == true;
        _notesFooterText.IsEnabled = _notesFooter.IsChecked == true;

        var ratio = _deck.Size.Width.Value > 0 ? (double)_deck.Size.Height.Value / _deck.Size.Width.Value : 9.0 / 16.0;
        var height = PreviewWidth * ratio;
        var picture = _services.Pictures.Create(_deck, _slide, PreviewWidth, height, new SlidePictureOptions { HeaderFooter = settings });
        var overlay = new FooterPlaceholderOverlay(PreviewWidth, height, settings.ShowDate, settings.ShowFooter, settings.ShowSlideNumber);
        Placeholders = overlay.Boxes;
        _slidePreview.Children.Clear();
        _slidePreview.Width = PreviewWidth;
        _slidePreview.Height = height;
        _slidePreview.Children.Add(picture);
        _slidePreview.Children.Add(overlay);

        var header = _notesHeader.IsChecked == true ? _notesHeaderText.Text : null;
        var footer = _notesFooter.IsChecked == true ? _notesFooterText.Text : null;
        _notesPreview.Sketch = new PageSketch(210, 297)
        {
            Margins = new PageSketchMargins(15, 15, 15, 15),
            Header = new PageSketchBand(8, 4, header, null, settings.ShowDate ? DateText(settings) : null),
            Footer = new PageSketchBand(8, 4, footer, null, _notesNumber.IsChecked == true ? "1" : null),
            Body = PageSketchBody.TextLines,
        };
    }

    private string DateText(HeaderFooterSettings settings) =>
        settings.DateIsFixed ? settings.FixedDate : _services.DateFormats.Render(settings.DateFormat, DateTime.Now, Culture);

    private CultureInfo Culture => _services.Cultures.For(_deck.TextLanguage);
}
