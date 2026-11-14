using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.Document.Borders;
using PulseOffice.Document.Pages;
using PulseOffice.Documents.Editing.Borders;
using PulseOffice.UI.Controls.Borders;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Pages;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Controls.TextFormat;
using PulseOffice.UI.Icons;

namespace PulseOffice.Documents.UI.Commands;

public enum FolioPageTab
{
    Page,
    Borders,
    Header,
    Footer,
    Columns,
    Watermark,
}

public interface IFolioPageTabs
{
    IReadOnlyList<FormTab> Tabs(PageStyle style, IReadOnlyList<string> others);

    void Write(PageStyle style, IReadOnlyList<string> others, IReadOnlyDictionary<string, object> values);
}

public interface IFolioPageDialog
{
    Task EditAsync(FolioEditor editor, string styleName, CancellationToken ct, FolioPageTab tab = FolioPageTab.Page);
}

public sealed record FolioPageServices(
    IFontPickerFactory Fonts,
    IPageMarginsEditors Margins,
    IColumnLayoutEditors Columns,
    IBorderEditors Borders,
    PulseOffice.UI.Controls.Fills.IFillEditors Fills,
    IBoxBorderFrames Frames,
    IPageSketchLayout Sketches,
    ITextFormatPreviews Previews,
    IIconCatalog Icons,
    ITextCatalog Texts);

public sealed class FolioPageTabs(FolioPageServices services) : IFolioPageTabs
{
    private const string Centimetres = "cm";

    private readonly ColorFieldValue _colors = new();

    private readonly BorderFrameScope _scope = new(false, false, false);

    private readonly IReadOnlyList<(string Key, double Width, double Height)> _sizes =
    [
        ("a4", 21.0, 29.7),
        ("a5", 14.8, 21.0),
        ("a3", 29.7, 42.0),
        ("letter", 21.59, 27.94),
        ("legal", 21.59, 35.56),
    ];

    private readonly PageUsage[] _usages = [PageUsage.All, PageUsage.Mirrored, PageUsage.Right, PageUsage.Left];

    private readonly NumberFormat[] _numbers = [NumberFormat.Arabic, NumberFormat.LowerRoman, NumberFormat.UpperRoman, NumberFormat.LowerLetter, NumberFormat.UpperLetter, NumberFormat.None];

    private readonly BorderSide[] _sides = [BorderSide.Top, BorderSide.Bottom, BorderSide.Left, BorderSide.Right];

    private ITextCatalog Texts => services.Texts;

    public IReadOnlyList<FormTab> Tabs(PageStyle style, IReadOnlyList<string> others)
    {
        var texts = Texts;
        var size = _sizes.ToList().FindIndex(entry => Near(entry.Width, style.Size.Width) && Near(entry.Height, style.Size.Height));
        var borders = style.Borders ?? new BoxBorders();
        var watermark = style.Watermark;
        var format = texts["folio.page.section.format"];
        var margins = texts["folio.page.section.margins"];
        var layout = texts["folio.page.section.layout"];
        return
        [
            Previewed(new FormTab(texts["folio.page.tab.page"], texts["folio.page.explanation"],
            [
                new ChoiceField("size", texts["folio.page.size"], [.. _sizes.Select(entry => texts[$"folio.page.size.{entry.Key}"]), texts["folio.page.size.user"]], size < 0 ? _sizes.Count : size) { Section = format },
                new NumberField("width", texts["folio.page.width"], style.Size.Width.ToCentimeters(), 1, 300, 0.1) { Unit = Centimetres, Section = format, ShownWhen = Custom },
                new NumberField("height", texts["folio.page.height"], style.Size.Height.ToCentimeters(), 1, 300, 0.1) { Unit = Centimetres, Section = format, ShownWhen = Custom },
                new IconChoiceField("orientation", texts["folio.page.orientation"],
                [
                    new IconChoice("portrait", "page.orientation.portrait", texts["command.folio.page.portrait.title"]),
                    new IconChoice("landscape", "page.orientation.landscape", texts["command.folio.page.landscape.title"]),
                ], services.Icons, style.Orientation == PageOrientation.Landscape ? 1 : 0) { ShowLabels = true, Section = format },
                new PageMarginsField("margins", margins,
                    new PageMargins(style.Margins.Top.ToCentimeters(), style.Margins.Bottom.ToCentimeters(), style.Margins.Left.ToCentimeters(), style.Margins.Right.ToCentimeters())
                    {
                        Mirrored = style.Usage == PageUsage.Mirrored,
                    },
                    Centimetres, true, services.Margins) { Section = margins },
                new ChoiceField("usage", texts["folio.page.usage"], [.. _usages.Select(usage => texts[$"folio.page.usage.{usage.ToString().ToLowerInvariant()}"])], Math.Max(0, Array.IndexOf(_usages, style.Usage))) { Section = layout },
                new ChoiceField("numbers", texts["folio.page.numbers"], [.. _numbers.Select(item => texts[$"folio.list.format.{item.ToString().ToLowerInvariant()}"])], Math.Max(0, Array.IndexOf(_numbers, style.NumberFormat))) { Section = layout },
                new ChoiceField("next", texts["folio.page.next"], [texts["folio.styles.same"], .. others], style.NextStyleName is { } next ? others.ToList().IndexOf(next) + 1 : 0) { Section = layout },
            ]), style),
            Previewed(new FormTab(texts["folio.paragraph.tab.borders"], texts["folio.page.borders.explanation"],
            [
                new BorderEditorField("pborders", texts["folio.paragraph.tab.borders"], services.Borders, _scope, services.Frames.Frame(borders)),
                new NumberField("pborder.padding", texts["folio.border.padding"], _sides.Select(borders.Padding).Max(), 0, 200) { Unit = "pt" },
                new PulseOffice.UI.Controls.Fills.SolidBackgroundField("pbackground", texts["folio.paragraph.background"], services.Fills, style.Background is { } fill && fill >> 24 != 0 ? _colors.Hex(fill) : null),
            ]), style),
            Previewed(Region("header", style.Header, texts["folio.page.tab.header"],
            [
                new CheckField("firstDifferent", texts["folio.page.firstDifferent"], style.FirstPageDifferent),
                new CheckField("evenDifferent", texts["folio.page.evenDifferent"], style.EvenPagesDifferent),
            ]), style),
            Previewed(Region("footer", style.Footer, texts["folio.page.tab.footer"], []), style),
            Previewed(new FormTab(texts["folio.page.tab.columns"], texts["folio.page.columns.explanation"],
            [
                new ColumnLayoutField("columnLayout", texts["folio.page.tab.columns"], new ColumnLayout(style.Columns.Count, style.Columns.Gap.ToCentimeters(), style.Columns.Separator), Centimetres, services.Columns),
            ]), style),
            Previewed(new FormTab(texts["folio.page.tab.watermark"], texts["folio.page.watermark.explanation"],
            [
                new TextField("mark", texts["folio.page.watermark.text"], watermark?.Text ?? string.Empty),
                new FontField("markFont", texts["folio.character.font"], watermark?.Font ?? "Liberation Sans", services.Fonts),
                new NumberField("markAngle", texts["folio.page.watermark.angle"], watermark?.Angle ?? 45, -90, 90) { Unit = "°", Integer = true, Presets = [-45, 0, 45, 90] },
                new NumberField("markTransparency", texts["folio.page.watermark.transparency"], watermark?.Transparency ?? 50, 0, 100) { Unit = "%", Integer = true, ShowSlider = true },
                new ColorField("markColor", texts["folio.character.color"], _colors.Hex(watermark?.Color ?? 0xFFC0C0C0), AllowNone: false),
            ]), style),
        ];
    }

    public void Write(PageStyle style, IReadOnlyList<string> others, IReadOnlyDictionary<string, object> values)
    {
        var (width, height) = Size(style, values);
        style.Size = new DocumentSize(Twip.FromCentimeters(width), Twip.FromCentimeters(height));
        style.Orientation = values.Choice("orientation", 2) == 1 ? PageOrientation.Landscape : PageOrientation.Portrait;
        var margins = Margins(style, values);
        var (left, right) = Fit(margins.Left, margins.Right, style.Width.ToCentimeters());
        var (top, bottom) = Fit(margins.Top, margins.Bottom, style.Height.ToCentimeters());
        style.Margins = new Margins(Twip.FromCentimeters(left), Twip.FromCentimeters(top), Twip.FromCentimeters(right), Twip.FromCentimeters(bottom));
        style.Usage = margins.Mirrored ? PageUsage.Mirrored : _usages[values.Choice("usage", _usages.Length)];
        style.NumberFormat = _numbers[values.Choice("numbers", _numbers.Length)];
        var next = values.Choice("next", others.Count + 1);
        style.NextStyleName = next == 0 ? null : others[next - 1];
        style.Borders = Borders(style, values);
        style.Background = _colors.Argb(values.Text("pbackground"));
        Region(style.Header, "header", values);
        Region(style.Footer, "footer", values);
        style.FirstPageDifferent = values.Flag("firstDifferent");
        style.EvenPagesDifferent = values.Flag("evenDifferent");
        var columns = Columns(style, values);
        style.Columns = columns.Count <= 1 && !columns.Separator
            ? SectionColumns.Single
            : new SectionColumns(Math.Clamp(columns.Count, 1, 10), Twip.FromCentimeters(columns.Gap)) { Separator = columns.Separator };
        style.Watermark = values.Text("mark").Trim() is { Length: > 0 } text
            ? new PageWatermark(text)
            {
                Font = values.Text("markFont") is { Length: > 0 } font ? font : "Liberation Sans",
                Angle = values.Number("markAngle", 45),
                Transparency = values.Number("markTransparency", 50),
                Color = _colors.Argb(values.Text("markColor")) ?? 0xFFC0C0C0,
            }
            : null;
    }

    public PageSketch Sketch(PageStyle style, IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(values);
        var (width, height) = Size(style, values);
        var margins = Margins(style, values);
        var columns = Columns(style, values);
        return new PageSketch(width, height)
        {
            Orientation = values.Choice("orientation", 2) == 1 ? PageSketchOrientation.Landscape : PageSketchOrientation.Portrait,
            Margins = new PageSketchMargins(margins.Left, margins.Top, margins.Right, margins.Bottom),
            Header = Band("header", style.Header, values),
            Footer = Band("footer", style.Footer, values),
            Columns = Math.Clamp(columns.Count, 1, 10),
            ColumnGap = columns.Gap,
            Paper = _colors.Argb(values.Text("pbackground")) ?? 0xFFFFFFFF,
            Watermark = values.Text("mark").Trim() is { Length: > 0 } mark ? mark : null,
        };
    }

    private FormTab Previewed(FormTab tab, PageStyle style) => tab with
    {
        LivePreview = feed => services.Previews.Create(feed, new FeedPreview<PageSketch>(
            values => Sketch(style, values),
            sketch => new PagePreview(services.Sketches) { Sketch = sketch, Width = 200, Height = 220 },
            PreviewBackdrop.Surface,
            240)),
    };

    private bool Custom(IReadOnlyDictionary<string, object> values) => values.Choice("size", _sizes.Count + 1) == _sizes.Count;

    private (double Width, double Height) Size(PageStyle style, IReadOnlyDictionary<string, object> values)
    {
        var index = values.ContainsKey("size") ? values.Choice("size", _sizes.Count + 1) : _sizes.Count;
        return index < _sizes.Count
            ? (_sizes[index].Width, _sizes[index].Height)
            : (values.Number("width", style.Size.Width.ToCentimeters()), values.Number("height", style.Size.Height.ToCentimeters()));
    }

    private PageMargins Margins(PageStyle style, IReadOnlyDictionary<string, object> values) =>
        values.TryGetValue("margins", out var value) && value is PageMargins chosen
            ? chosen
            : new PageMargins(style.Margins.Top.ToCentimeters(), style.Margins.Bottom.ToCentimeters(), style.Margins.Left.ToCentimeters(), style.Margins.Right.ToCentimeters());

    private ColumnLayout Columns(PageStyle style, IReadOnlyDictionary<string, object> values) =>
        values.TryGetValue("columnLayout", out var value) && value is ColumnLayout chosen
            ? chosen
            : new ColumnLayout(style.Columns.Count, style.Columns.Gap.ToCentimeters(), style.Columns.Separator);

    private PageSketchBand? Band(string key, HeaderFooterContent region, IReadOnlyDictionary<string, object> values)
    {
        var on = values.TryGetValue($"{key}.on", out var flag) ? flag is true : region.Enabled;
        if (!on) return null;
        var spacing = values.Number($"{key}.spacing", region.Spacing.ToCentimeters());
        var minimum = values.Number($"{key}.height", region.MinimumHeight.ToCentimeters());
        return new PageSketchBand(Math.Max(minimum, 0.5) + spacing, spacing);
    }

    private FormTab Region(string key, HeaderFooterContent region, string title, IReadOnlyList<FormField> extra) =>
        new(title, Texts[$"folio.page.{key}.explanation"],
        [
            new CheckField($"{key}.on", Texts[$"folio.page.{key}.on"], region.Enabled),
            new NumberField($"{key}.spacing", Texts["folio.page.region.spacing"], region.Spacing.ToCentimeters(), 0, 20, 0.05) { Unit = Centimetres, ShownWhen = values => values.Flag($"{key}.on") },
            new NumberField($"{key}.height", Texts["folio.page.region.height"], region.MinimumHeight.ToCentimeters(), 0, 20, 0.05) { Unit = Centimetres, ShownWhen = values => values.Flag($"{key}.on") },
            .. extra,
        ]);

    private void Region(HeaderFooterContent region, string key, IReadOnlyDictionary<string, object> values)
    {
        region.Enabled = values.Flag($"{key}.on");
        region.Spacing = Twip.FromCentimeters(values.Number($"{key}.spacing", region.Spacing.ToCentimeters()));
        region.MinimumHeight = Twip.FromCentimeters(values.Number($"{key}.height", region.MinimumHeight.ToCentimeters()));
    }

    private BoxBorders? Borders(PageStyle style, IReadOnlyDictionary<string, object> values)
    {
        var frame = values.TryGetValue("pborders", out var value) && value is BorderFrame chosen ? chosen : services.Frames.Frame(style.Borders);
        var borders = services.Frames.Apply(new BoxBorders(), frame, values.Number("pborder.padding", 0));
        return borders.HasLines ? borders : null;
    }

    private (double First, double Second) Fit(double first, double second, double extent)
    {
        first = Math.Max(0, first);
        second = Math.Max(0, second);
        var room = Math.Max(0, extent - MinimumBody);
        var total = first + second;
        if (total <= room || total <= 0) return (first, second);
        var scale = room / total;
        return (first * scale, second * scale);
    }

    private const double MinimumBody = 1;

    private bool Near(double centimetres, Twip value) => Math.Abs(centimetres - value.ToCentimeters()) < 0.02;
}

public sealed class FolioPageDialog(IFolioPageTabs tabs, ITabbedForms forms, IStyleEditing styles) : IFolioPageDialog
{
    public async Task EditAsync(FolioEditor editor, string styleName, CancellationToken ct, FolioPageTab tab = FolioPageTab.Page)
    {
        var sheet = editor.Document.Styles;
        if (sheet.Page.Find(styleName) is not { } style) return;
        var others = sheet.Page.All.Where(other => other.Name != styleName).Select(other => other.Name).ToList();
        if (await forms.AskAsync("command.folio.page.setup.title", tabs.Tabs(style, others), ct, (int)tab, 640).ConfigureAwait(true) is not { } values) return;
        editor.Change(() => styles.Change(editor.Document, editor.History, sheet.Page, styleName, edited => tabs.Write(edited, others, values), "page-style"));
    }
}
