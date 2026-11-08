using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Formats.Pdf;
using PulseOffice.Localization;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Printing;

public sealed class PdfExportView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly int[] _resolutions = [75, 150, 300, 600, 1200];
    private readonly PdfExportDefaults _defaults;
    private readonly IPageRangeParser _ranges;
    private readonly ITextCatalog _texts;
    private readonly ComboBox? _layout;
    private readonly TextBox _range = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _rangeMessage;
    private readonly CheckBox _bookmarks = new();
    private readonly CheckBox _notes = new();
    private readonly CheckBox _links = new();
    private readonly CheckBox _archive = new();
    private readonly TextBox _title = new();
    private readonly TextBox _author = new();
    private readonly TextBox _subject = new();
    private readonly TextBox _keywords = new();
    private readonly RadioButton _lossless = new() { GroupName = "pdf-images", IsChecked = true };
    private readonly RadioButton _jpeg = new() { GroupName = "pdf-images" };
    private readonly Slider _quality = new() { Minimum = 5, Maximum = 100, Value = 90, SmallChange = 5, LargeChange = 10, IsSnapToTickEnabled = true, TickFrequency = 5, MinWidth = 180 };
    private readonly TextBlock _qualityValue = new() { MinWidth = 44, VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _reduce = new();
    private readonly ComboBox _resolution = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _openPassword = new() { PasswordChar = '•' };
    private readonly TextBox _openRepeat = new() { PasswordChar = '•' };
    private readonly TextBox _permissionsPassword = new() { PasswordChar = '•' };
    private readonly TextBox _permissionsRepeat = new() { PasswordChar = '•' };
    private readonly TextBlock _passwordMessage;
    private readonly ComboBox _printing = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _copy = new() { IsChecked = true };
    private readonly CheckBox _changes = new() { IsChecked = true };
    private readonly CheckBox _comments = new() { IsChecked = true };
    private readonly TextBlock _securityArchive;
    private readonly List<Control> _securityControls = [];
    private readonly PrintPagesView _pages;
    private readonly TextBlock _pageLabel;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _confirm;
    private IPrintableDocument? _document;
    private IReadOnlyList<int> _exported = [];
    private int _shown;

    public PdfExportView(PdfExportDefaults defaults, IPageRangeParser ranges, ITextCatalog texts, IPageCanvasFactory canvases, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _pages = new PrintPagesView(new PreviewSpreads(), canvases) { Zoom = PreviewZoom.Fit, Width = 300, Height = 380 };
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        var metrics = DesignTokens.For(density);

        _range.PlaceholderText = texts["print.range.all"];
        AutomationProperties.SetName(_range, texts["pdf.export.range"]);
        _rangeMessage = new TextBlock { FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _bookmarks.IsChecked = defaults.HasHeadings;
        _notes.IsChecked = defaults.HasComments;
        _links.IsChecked = true;
        _title.Text = defaults.Title;
        _author.Text = defaults.Author;
        _subject.Text = defaults.Subject;
        _keywords.Text = defaults.Keywords;

        var layout = new TwoPaneDialogLayout(metrics)
        {
            Header = new HelpTip(texts["pdf.export.explanation"], texts, metrics),
        };

        var general = layout.AddSection(texts["pdf.section.general"]);
        if (defaults.Layouts.Count > 0)
        {
            _layout = new ComboBox
            {
                ItemsSource = defaults.Layouts.Select(item => item.Label).ToList(),
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            _layout.SelectionChanged += (_, _) => Rebuild();
            general.Add(defaults.LayoutLabel, _layout);
        }

        var rangeStack = new StackPanel { Spacing = 2 };
        rangeStack.Children.Add(_range);
        rangeStack.Children.Add(_rangeMessage);
        general.Add(texts["pdf.export.range"], rangeStack);

        var images = layout.AddSection(texts["pdf.section.images"]);
        images.Add(null, Choice(_lossless, texts["pdf.images.lossless"], texts["pdf.images.lossless.help"], metrics), DialogRowKind.Indented);
        images.Add(null, Choice(_jpeg, texts["pdf.images.jpeg"], texts["pdf.images.jpeg.help"], metrics), DialogRowKind.Indented);
        var qualityRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        qualityRow.Children.Add(_quality);
        qualityRow.Children.Add(_qualityValue);
        AutomationProperties.SetName(_quality, texts["pdf.images.quality"]);
        images.Add(texts["pdf.images.quality"], qualityRow);
        images.Add(null, Check(_reduce, texts["pdf.images.reduce"]), DialogRowKind.Indented);
        _resolution.ItemsSource = _resolutions.Select(dpi => texts.Format("pdf.images.dpi", dpi)).ToList();
        _resolution.SelectedIndex = 2;
        AutomationProperties.SetName(_resolution, texts["pdf.images.resolution"]);
        images.Add(texts["pdf.images.resolution"], _resolution);
        _jpeg.IsCheckedChanged += (_, _) => RefreshImages();
        _reduce.IsCheckedChanged += (_, _) => RefreshImages();
        _quality.PropertyChanged += (_, change) =>
        {
            if (change.Property == RangeBase.ValueProperty) RefreshImages();
        };

        var structure = layout.AddSection(texts["pdf.section.structure"]);
        structure.Add(null, Check(_bookmarks, texts["pdf.export.bookmarks"]), DialogRowKind.Indented);
        if (defaults.HasComments) structure.Add(null, Check(_notes, texts["pdf.export.notes"]), DialogRowKind.Indented);
        structure.Add(null, Check(_links, texts["pdf.export.links"]), DialogRowKind.Indented);

        var security = layout.AddSection(texts["pdf.section.security"]);
        _securityArchive = Note(texts["pdf.security.archive"], metrics);
        security.Add(null, _securityArchive, DialogRowKind.Indented);
        Secure(security, texts["pdf.security.open"], _openPassword);
        Secure(security, texts["pdf.security.openRepeat"], _openRepeat);
        Secure(security, texts["pdf.security.permissions"], _permissionsPassword);
        Secure(security, texts["pdf.security.permissionsRepeat"], _permissionsRepeat);
        _passwordMessage = new TextBlock { FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap, IsVisible = false }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        security.Add(null, _passwordMessage, DialogRowKind.Indented);
        _printing.ItemsSource = new[] { texts["pdf.security.print.none"], texts["pdf.security.print.low"], texts["pdf.security.print.high"] };
        _printing.SelectedIndex = 2;
        Secure(security, texts["pdf.security.printing"], _printing);
        Secure(security, null, Check(_copy, texts["pdf.security.copy"]));
        Secure(security, null, Check(_changes, texts["pdf.security.changes"]));
        Secure(security, null, Check(_comments, texts["pdf.security.comments"]));
        security.Add(null, Note(texts["pdf.security.note"], metrics), DialogRowKind.Indented);

        var archive = layout.AddSection(texts["pdf.section.archive"]);
        archive.Add(null, Check(_archive, texts["pdf.export.archive"]), DialogRowKind.Indented);
        archive.Add(null, Note(texts["pdf.export.archive.note"], metrics), DialogRowKind.Indented);

        var description = layout.AddSection(texts["pdf.section.description"]);
        description.Add(texts["pdf.export.title"], _title);
        description.Add(texts["pdf.export.author"], _author);
        description.Add(texts["pdf.export.subject"], _subject);
        description.Add(texts["pdf.export.keywords"], _keywords);

        _pageLabel = new TextBlock { FontSize = metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center, MinWidth = 120, TextAlignment = Avalonia.Media.TextAlignment.Center }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _previous = Small("‹", texts["template.preview.previous"], -1);
        _next = Small("›", texts["template.preview.next"], 1);
        var paging = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = metrics.SpacingSmall };
        paging.Children.Add(_previous);
        paging.Children.Add(_pageLabel);
        paging.Children.Add(_next);

        var preview = new StackPanel { Spacing = metrics.SpacingSmall };
        preview.Children.Add(_pages);
        preview.Children.Add(paging);
        AutomationProperties.SetName(_pages, texts["pdf.preview"]);
        layout.Preview = preview;

        Content = new ScrollViewer { Content = layout, MaxHeight = 640, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["pdf.export.confirm"], Confirm, out _confirm, primary: true);

        _range.TextChanged += (_, _) => Refilter();
        _notes.IsCheckedChanged += (_, _) => Refilter();
        _archive.IsCheckedChanged += (_, _) => Refilter();
        foreach (var box in new[] { _openPassword, _openRepeat, _permissionsPassword, _permissionsRepeat })
            box.TextChanged += (_, _) => Refilter();
        RefreshImages();
        Rebuild();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public IReadOnlyList<int> ExportedPages => _exported;

    public int ShownPage => _exported.Count == 0 ? -1 : _exported[_shown];

    public bool CanExport => _confirm.IsEnabled;

    public bool SecurityEnabled => _securityControls.All(control => control.IsEnabled);

    public bool QualityEnabled => _quality.IsEnabled;

    public string RangeText
    {
        get => _range.Text ?? string.Empty;
        set
        {
            _range.Text = value;
            Refilter();
        }
    }

    public bool ArchiveChecked
    {
        get => _archive.IsChecked == true;
        set
        {
            _archive.IsChecked = value;
            Refilter();
        }
    }

    public int? LayoutIndex
    {
        get => _layout?.SelectedIndex;
        set
        {
            if (_layout is null || value is null) return;
            _layout.SelectedIndex = value.Value;
            Rebuild();
        }
    }

    public PdfImageOptions Images => new()
    {
        Compression = _jpeg.IsChecked == true ? PdfImageCompression.Jpeg : PdfImageCompression.Lossless,
        JpegQuality = (int)Math.Round(_quality.Value),
        MaxResolution = _reduce.IsChecked == true && _resolution.SelectedIndex >= 0 ? _resolutions[_resolution.SelectedIndex] : null,
    };

    public PdfSecurityOptions Security => new()
    {
        OpenPassword = _openPassword.Text ?? string.Empty,
        PermissionsPassword = _permissionsPassword.Text ?? string.Empty,
        Printing = (PdfPrintPermission)Math.Max(0, _printing.SelectedIndex),
        AllowCopy = _copy.IsChecked == true,
        AllowChanges = _changes.IsChecked == true,
        AllowComments = _comments.IsChecked == true,
    };

    public void SetImages(bool jpeg, int quality, int? maxResolution)
    {
        _jpeg.IsChecked = jpeg;
        _lossless.IsChecked = !jpeg;
        _quality.Value = quality;
        _reduce.IsChecked = maxResolution is not null;
        if (maxResolution is { } dpi && Array.IndexOf(_resolutions, dpi) is >= 0 and var index) _resolution.SelectedIndex = index;
        RefreshImages();
    }

    public void SetPasswords(string open, string openRepeat, string permissions, string permissionsRepeat)
    {
        _openPassword.Text = open;
        _openRepeat.Text = openRepeat;
        _permissionsPassword.Text = permissions;
        _permissionsRepeat.Text = permissionsRepeat;
        Refilter();
    }

    public void SetPermissions(PdfPrintPermission printing, bool copy, bool changes, bool comments)
    {
        _printing.SelectedIndex = (int)printing;
        _copy.IsChecked = copy;
        _changes.IsChecked = changes;
        _comments.IsChecked = comments;
    }

    public void Turn(int direction)
    {
        if (_exported.Count == 0) return;
        _shown = Math.Clamp(_shown + direction, 0, _exported.Count - 1);
        ShowPage();
    }

    public void Confirm()
    {
        if (!Refilter()) return;

        var count = PageCount;
        var pages = string.IsNullOrWhiteSpace(RangeText) ? null : _ranges.Parse(RangeText, count);
        var options = new PdfExportOptions(_title.Text ?? string.Empty, _author.Text ?? string.Empty, _subject.Text ?? string.Empty)
        {
            Keywords = _keywords.Text ?? string.Empty,
            Pages = pages,
            Bookmarks = _bookmarks.IsChecked == true,
            Links = _links.IsChecked == true,
            Notes = _defaults.HasComments && _notes.IsChecked == true,
            Archive = ArchiveChecked,
            Language = _defaults.Language,
            Images = Images,
            Security = ArchiveChecked ? null : Security,
        };

        Completed?.Invoke(this, new PdfExportChoice(options, _layout?.SelectedIndex));
    }

    private int PageCount => _document?.PageCount ?? _defaults.PageCount;

    private bool PasswordsMatch =>
        ArchiveChecked ||
        ((_openPassword.Text ?? string.Empty) == (_openRepeat.Text ?? string.Empty) &&
         (_permissionsPassword.Text ?? string.Empty) == (_permissionsRepeat.Text ?? string.Empty));

    private void Rebuild()
    {
        _document = _layout is { SelectedIndex: >= 0 } chosen && chosen.SelectedIndex < _defaults.Layouts.Count
            ? _defaults.Layouts[chosen.SelectedIndex].Build()
            : _defaults.Preview?.Invoke();
        _pages.Document = _document;
        Refilter();
    }

    private void RefreshImages()
    {
        _quality.IsEnabled = _jpeg.IsChecked == true;
        _qualityValue.Text = _texts.Format("pdf.images.quality.value", (int)Math.Round(_quality.Value));
        _resolution.IsEnabled = _reduce.IsChecked == true;
    }

    private bool Refilter()
    {
        var count = PageCount;
        var all = Enumerable.Range(0, count).ToList();
        IReadOnlyList<int>? pages = string.IsNullOrWhiteSpace(RangeText) ? all : _ranges.Parse(RangeText, count);
        var valid = pages is { Count: > 0 };
        var archived = ArchiveChecked;
        var matching = PasswordsMatch;

        foreach (var control in _securityControls) control.IsEnabled = !archived;
        _securityArchive.IsVisible = archived;
        _passwordMessage.IsVisible = !matching;
        _passwordMessage.Text = matching ? string.Empty : _texts["pdf.security.mismatch"];
        _openRepeat.Classes.Set("invalid", !archived && (_openPassword.Text ?? string.Empty) != (_openRepeat.Text ?? string.Empty));
        _permissionsRepeat.Classes.Set("invalid", !archived && (_permissionsPassword.Text ?? string.Empty) != (_permissionsRepeat.Text ?? string.Empty));

        _exported = valid ? pages! : [];
        _shown = Math.Clamp(_shown, 0, Math.Max(0, _exported.Count - 1));
        _confirm.IsEnabled = valid && matching;
        _range.Classes.Set("invalid", !valid);
        _rangeMessage.Text = valid
            ? count == 1 && _exported.Count == 1 ? _texts["pdf.export.single"] : _texts.Format("pdf.export.count", _exported.Count, count)
            : _texts.Format("pdf.export.badRange", RangeText.Trim(), count);
        _rangeMessage.Themed(TextBlock.ForegroundProperty, valid ? Palette.Keys.TextSecondary : Palette.Keys.Danger);
        AutomationProperties.SetHelpText(_range, _rangeMessage.Text);
        ShowPage();
        return valid && matching;
    }

    private void ShowPage()
    {
        if (_exported.Count == 0 || _document is null)
        {
            _pages.IsVisible = _document is not null;
            _pageLabel.Text = _document is null ? _texts["template.preview.none"] : string.Empty;
            _previous.IsEnabled = _next.IsEnabled = false;
            return;
        }

        _pages.IsVisible = true;
        _pages.First = _exported[_shown];
        _pageLabel.Text = _texts.Format("pdf.preview.page", _exported[_shown] + 1, PageCount);
        _previous.IsEnabled = _shown > 0;
        _next.IsEnabled = _shown < _exported.Count - 1;
    }

    private void Secure(DialogSection section, string? label, Control control)
    {
        if (label is not null) AutomationProperties.SetName(control, label);
        _securityControls.Add(control);
        if (label is null) section.Add(null, control, DialogRowKind.Indented);
        else section.Add(label, control);
    }

    private Control Choice(RadioButton button, string label, string help, DesignTokens.Metrics metrics)
    {
        var text = new StackPanel { Spacing = 1 };
        text.Children.Add(new TextBlock { Text = label });
        text.Children.Add(new TextBlock { Text = help, FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        button.Content = text;
        AutomationProperties.SetName(button, label);
        return button;
    }

    private Control Check(CheckBox box, string label)
    {
        box.Content = label;
        return box;
    }

    private TextBlock Note(string text, DesignTokens.Metrics metrics) =>
        new TextBlock { Text = text, FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private Button Small(string caption, string name, int direction)
    {
        var button = new Button { Content = caption, Padding = new Thickness(10, 0), MinHeight = 22 };
        button.Classes.Add("ribbon");
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        button.Click += (_, _) => Turn(direction);
        return button;
    }
}
