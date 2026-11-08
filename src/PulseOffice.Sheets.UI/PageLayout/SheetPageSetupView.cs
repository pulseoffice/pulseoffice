using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Document.Pages;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.PageLayout;

public sealed record SheetPageSetupResult(PageSetup Setup, string PrintArea, int RepeatRows, int RepeatColumns);

public sealed record SheetPageSetupInput(
    PageSetup Setup, IReadOnlyList<PaperSize> Papers, string PrintArea, int RepeatRows, int RepeatColumns,
    string SheetName, string FileName, IReferenceSource AreaSource);

public sealed class SheetPageSetupServices(
    IPageSketchLayout sketches, IHeaderFooterTokens tokens, IMeasureUnits units, IIconCatalog icons)
{
    public IPageSketchLayout Sketches { get; } = sketches ?? throw new ArgumentNullException(nameof(sketches));

    public IHeaderFooterTokens Tokens { get; } = tokens ?? throw new ArgumentNullException(nameof(tokens));

    public IMeasureUnits Units { get; } = units ?? throw new ArgumentNullException(nameof(units));

    public IIconCatalog Icons { get; } = icons ?? throw new ArgumentNullException(nameof(icons));
}

public sealed class SheetPageSetupView
{
    private readonly SheetPageSetupInput _input;
    private readonly SheetPageSetupServices _services;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly List<PagePreview> _previews = [];
    private readonly IReadOnlyList<HeaderFooterField> _fields;
    private PageSetup _setup;
    private HeaderFooterZonesEditor? _header;
    private HeaderFooterZonesEditor? _footer;
    private HeaderFooterZonesEditor? _firstHeader;
    private HeaderFooterZonesEditor? _firstFooter;
    private HeaderFooterZonesEditor? _evenHeader;
    private HeaderFooterZonesEditor? _evenFooter;
    private RangeFieldControl? _area;
    private MeasureField? _repeatRows;
    private MeasureField? _repeatColumns;

    public SheetPageSetupView(SheetPageSetupInput input, SheetPageSetupServices services, ITextCatalog texts)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _setup = input.Setup;
        _fields = Fields(texts, input, DateTime.Now);
    }

    public PageSetup Setup => _setup;

    public IReadOnlyList<HeaderFooterField> HeaderFields => _fields;

    public HeaderFooterZonesEditor? HeaderEditor => _header;

    public HeaderFooterZonesEditor? FooterEditor => _footer;

    public HeaderFooterZonesEditor? FirstHeaderEditor => _firstHeader;

    public HeaderFooterZonesEditor? EvenFooterEditor => _evenFooter;

    public IReadOnlyList<PagePreview> Previews => _previews;

    public TabbedDialogView Build(int initialTab = 0) =>
        new(
            [
                new DialogTab(_texts["page.tab.page"], PageTab),
                new DialogTab(_texts["page.tab.margins"], MarginsTab),
                new DialogTab(_texts["page.tab.headerFooter"], HeaderFooterTab),
                new DialogTab(_texts["page.tab.sheet"], SheetTab),
            ],
            () => Collect(),
            _texts,
            initialTab: initialTab);

    public SheetPageSetupResult Collect() => new(
        Current(),
        _area?.Text.Trim() ?? _input.PrintArea,
        (int)(_repeatRows?.Read() ?? _input.RepeatRows),
        (int)(_repeatColumns?.Read() ?? _input.RepeatColumns));

    public PageSketch Sketch()
    {
        var setup = Current();
        var tokens = _services.Tokens;
        return new PageSketch(setup.Paper.Width.ToCentimeters() * 10, setup.Paper.Height.ToCentimeters() * 10)
        {
            Orientation = setup.Orientation == PageOrientation.Landscape ? PageSketchOrientation.Landscape : PageSketchOrientation.Portrait,
            Margins = new PageSketchMargins(Mm(setup.Margins.Left), Mm(setup.Margins.Top), Mm(setup.Margins.Right), Mm(setup.Margins.Bottom)),
            Header = new PageSketchBand(Mm(setup.HeaderHeight), 0,
                tokens.Expand(setup.Header.Left, _fields), tokens.Expand(setup.Header.Center, _fields), tokens.Expand(setup.Header.Right, _fields)),
            Footer = new PageSketchBand(Mm(setup.FooterHeight), 0,
                tokens.Expand(setup.Footer.Left, _fields), tokens.Expand(setup.Footer.Center, _fields), tokens.Expand(setup.Footer.Right, _fields)),
            Body = PageSketchBody.Cells,
        };
    }

    public IReadOnlyList<HeaderFooterPreset> Presets()
    {
        var texts = _texts;
        var input = _input;
        var page = texts["headerFooter.word.page"];
        var of = texts["headerFooter.word.of"];
        return
        [
            new(texts["headerFooter.preset.none"], string.Empty, string.Empty, string.Empty),
            new(page + " 1", string.Empty, page + " &P", string.Empty),
            new(page + " 1 " + of + " 3", string.Empty, page + " &P " + of + " &N", string.Empty),
            new(input.SheetName, string.Empty, "&A", string.Empty),
            new(input.FileName, string.Empty, "&F", string.Empty),
            new(input.SheetName + " · " + page + " 1", "&A", string.Empty, page + " &P"),
            new(texts["headerFooter.preset.confidential"] + " · " + DateTime.Now.ToString("d", CultureInfo.CurrentCulture) + " · " + page + " 1",
                texts["headerFooter.preset.confidential"], "&D", page + " &P"),
            new(input.FileName + " · " + input.SheetName, "&F", string.Empty, "&A"),
        ];
    }

    private IReadOnlyList<HeaderFooterField> Fields(ITextCatalog texts, SheetPageSetupInput input, DateTime now) =>
    [
        new("&P", texts["headerFooter.field.page"], "folio.insert.field.pageNumber", "1"),
        new("&N", texts["headerFooter.field.pages"], "folio.insert.field.pageCount", "3"),
        new("&D", texts["headerFooter.field.date"], "folio.insert.field.date", now.ToString("d", CultureInfo.CurrentCulture)),
        new("&T", texts["headerFooter.field.time"], "folio.insert.field.time", now.ToString("t", CultureInfo.CurrentCulture)),
        new("&F", texts["headerFooter.field.file"], "folio.insert.field.fileName", input.FileName),
        new("&A", texts["headerFooter.field.sheet"], "ledger.insert.sheetName", input.SheetName),
    ];

    private PageSetup Current() => _setup with
    {
        Header = Zones(_header, _setup.Header),
        Footer = Zones(_footer, _setup.Footer),
        FirstHeader = Zones(_firstHeader, _setup.FirstHeader),
        FirstFooter = Zones(_firstFooter, _setup.FirstFooter),
        EvenHeader = Zones(_evenHeader, _setup.EvenHeader),
        EvenFooter = Zones(_evenFooter, _setup.EvenFooter),
    };

    private HeaderFooter Zones(HeaderFooterZonesEditor? editor, HeaderFooter fallback) =>
        editor?.Value is { } zones ? new HeaderFooter(zones.Left, zones.Center, zones.Right) : fallback;

    private void Change(PageSetup setup)
    {
        _setup = setup;
        Refresh();
    }

    private void Refresh()
    {
        var sketch = Sketch();
        foreach (var preview in _previews) preview.Sketch = sketch;
    }

    private TwoPaneDialogLayout Layout()
    {
        var preview = new PagePreview(_services.Sketches) { Width = 190, Height = 250 };
        AutomationProperties.SetName(preview, _texts["page.preview"]);
        _previews.Add(preview);
        preview.Sketch = Sketch();
        return new TwoPaneDialogLayout(_metrics) { Preview = preview };
    }

    private Control PageTab()
    {
        var layout = Layout();
        var paper = layout.AddSection(_texts["page.section.paper"]);

        var listed = _input.Papers.ToList();
        var known = listed.FindIndex(item => item.Id == _setup.Paper.Id && item.Size == _setup.Paper.Size);
        IReadOnlyList<PaperSize> papers = known >= 0 ? listed : [.. listed, _setup.Paper];
        var paperBox = new ComboBox
        {
            ItemsSource = papers.Select(item => PaperName(item, listed) + "  ·  " + Size(item)).ToList(),
            SelectedIndex = known >= 0 ? known : papers.Count - 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        paperBox.SelectionChanged += (_, _) =>
        {
            if (paperBox.SelectedIndex >= 0) Change(_setup with { Paper = papers[paperBox.SelectedIndex] });
        };
        paper.Add(_texts["page.paper"], paperBox, editorWidth: 240);

        var orientation = new IconChoiceGroup(
        [
            new IconChoice("portrait", "folio.page.portrait", _texts["page.orientation.portrait"]),
            new IconChoice("landscape", "folio.page.landscape", _texts["page.orientation.landscape"]),
        ], _services.Icons, _metrics, _texts["page.orientation"], _setup.Orientation == PageOrientation.Landscape ? 1 : 0, showLabels: true);
        orientation.SelectionChanged += (_, index) => Change(_setup with { Orientation = index == 1 ? PageOrientation.Landscape : PageOrientation.Portrait });
        paper.Add(_texts["page.orientation"], orientation, editorWidth: 240);

        var scaling = layout.AddSection(_texts["page.section.scaling"]);
        var mode = new IconChoiceGroup(
        [
            new IconChoice("scale", string.Empty, _texts["page.scale.percent"]),
            new IconChoice("fit", string.Empty, _texts["page.scale.fit"]),
        ], null, _metrics, _texts["page.section.scaling"], _setup.FitsToPages ? 1 : 0, showLabels: true);
        scaling.Add(null, mode, DialogRowKind.Indented, editorWidth: 260);

        var percent = Measure("%", 10, 400, 5, Math.Round(_setup.Scale * 100), _texts["page.scale.percent"], presets: [50, 75, 100, 125, 150]);
        percent.ValueCommitted += (_, value) => Change(_setup with { Scale = value / 100, FitToWidthPages = null, FitToHeightPages = null });
        var percentRow = scaling.Add(_texts["page.scale.value"], percent, editorWidth: 160);

        var wide = Measure(_texts["page.scale.pagesUnit"], 0, 100, 1, _setup.FitToWidthPages ?? 1, _texts["page.scale.wide"]);
        var tall = Measure(_texts["page.scale.pagesUnit"], 0, 100, 1, _setup.FitToHeightPages ?? 0, _texts["page.scale.tall"]);
        void Fit() => Change(_setup with { FitToWidthPages = Pages(wide.Value) ?? (Pages(tall.Value) is null ? 1 : null), FitToHeightPages = Pages(tall.Value) });
        wide.ValueCommitted += (_, _) => Fit();
        tall.ValueCommitted += (_, _) => Fit();
        var wideRow = scaling.Add(_texts["page.scale.wide"], wide, editorWidth: 170);
        var tallRow = scaling.Add(_texts["page.scale.tall"], tall, editorWidth: 170);
        var fitHelp = new HelpTip(_texts["page.scale.fitHelp"], _texts, _metrics);
        var fitHelpRow = scaling.Add(null, fitHelp, DialogRowKind.Indented, editorWidth: 300);

        void ShowMode()
        {
            var fit = mode.SelectedIndex == 1;
            percentRow.IsVisible = !fit;
            wideRow.IsVisible = tallRow.IsVisible = fitHelpRow.IsVisible = fit;
        }
        mode.SelectionChanged += (_, index) =>
        {
            if (index == 1) Fit();
            else Change(_setup with { Scale = percent.Value / 100, FitToWidthPages = null, FitToHeightPages = null });
            ShowMode();
        };
        ShowMode();

        return layout;
    }

    private Control MarginsTab()
    {
        var layout = Layout();
        var margins = layout.AddSection(_texts["page.section.margins"]);
        var top = Millimetres(_setup.Margins.Top, _texts["page.margin.top"]);
        var bottom = Millimetres(_setup.Margins.Bottom, _texts["page.margin.bottom"]);
        var left = Millimetres(_setup.Margins.Left, _texts["page.margin.left"]);
        var right = Millimetres(_setup.Margins.Right, _texts["page.margin.right"]);
        void Margins(MeasureField edited, MeasureField opposite, bool horizontal)
        {
            var page = Mm(horizontal ? _setup.PageWidth : _setup.PageHeight);
            var room = page - MinimumPrintableMillimetres - opposite.Value;
            if (edited.Value > room) edited.Value = Math.Max(0, room);
            Change(_setup with
            {
                Margins = new Margins(Twip.FromMillimeters(left.Value), Twip.FromMillimeters(top.Value), Twip.FromMillimeters(right.Value), Twip.FromMillimeters(bottom.Value)),
            });
        }
        foreach (var (field, key, opposite, horizontal) in new[]
        {
            (top, "page.margin.top", bottom, false), (bottom, "page.margin.bottom", top, false),
            (left, "page.margin.left", right, true), (right, "page.margin.right", left, true),
        })
        {
            field.ValueCommitted += (_, _) => Margins(field, opposite, horizontal);
            var row = margins.Add(_texts[key], field, editorWidth: 160);
            if (row.Label is { } handle) field.AttachScrubber(handle);
        }

        var bands = layout.AddSection(_texts["page.section.bands"]);
        var header = Millimetres(_setup.HeaderHeight, _texts["page.headerHeight"]);
        var footer = Millimetres(_setup.FooterHeight, _texts["page.footerHeight"]);
        header.ValueCommitted += (_, value) => Change(_setup with { HeaderHeight = Twip.FromMillimeters(value) });
        footer.ValueCommitted += (_, value) => Change(_setup with { FooterHeight = Twip.FromMillimeters(value) });
        bands.Add(_texts["page.headerHeight"], header, editorWidth: 160);
        bands.Add(_texts["page.footerHeight"], footer, editorWidth: 160);

        var centre = layout.AddSection(_texts["page.section.centre"]);
        centre.Add(null, Check(_texts["page.centerHorizontally"], _setup.CenterHorizontally, value => Change(_setup with { CenterHorizontally = value })), DialogRowKind.Indented);
        centre.Add(null, Check(_texts["page.centerVertically"], _setup.CenterVertically, value => Change(_setup with { CenterVertically = value })), DialogRowKind.Indented);

        return layout;
    }

    private Control HeaderFooterTab()
    {
        var presets = Presets();
        HeaderFooterZonesEditor Editor(string key, HeaderFooter content)
        {
            var editor = new HeaderFooterZonesEditor(_texts[key], new HeaderFooterZones(content.Left, content.Center, content.Right),
                _fields, presets, _services.Tokens, _services.Icons, _texts, _metrics);
            editor.Changed += (_, _) => Refresh();
            return editor;
        }

        _header = Editor("page.header", _setup.Header);
        _footer = Editor("page.footer", _setup.Footer);
        _firstHeader = Editor("page.firstHeader", _setup.FirstHeader);
        _firstFooter = Editor("page.firstFooter", _setup.FirstFooter);
        _evenHeader = Editor("page.evenHeader", _setup.EvenHeader);
        _evenFooter = Editor("page.evenFooter", _setup.EvenFooter);

        var layout = Layout();
        layout.Explanation = _texts["page.headerFooter.explanation"];
        var section = layout.AddSection((string?)null);
        section.Add(null, _header, DialogRowKind.Wide, editorWidth: 440);
        section.Add(null, _footer, DialogRowKind.Wide, editorWidth: 440);

        var variants = layout.AddSection(_texts["page.section.variants"]);
        IReadOnlyList<DialogRow> first = [];
        IReadOnlyList<DialogRow> even = [];
        void Show()
        {
            foreach (var row in first) row.IsVisible = _setup.DifferentFirstPage;
            foreach (var row in even) row.IsVisible = _setup.DifferentOddEven;
        }
        variants.Add(null, Check(_texts["page.differentFirst"], _setup.DifferentFirstPage, value =>
        {
            Change(_setup with { DifferentFirstPage = value });
            Show();
        }), DialogRowKind.Indented);
        variants.Add(null, Check(_texts["page.differentOddEven"], _setup.DifferentOddEven, value =>
        {
            Change(_setup with { DifferentOddEven = value });
            Show();
        }), DialogRowKind.Indented);
        first = [variants.Add(null, _firstHeader, DialogRowKind.Wide, editorWidth: 440), variants.Add(null, _firstFooter, DialogRowKind.Wide, editorWidth: 440)];
        even = [variants.Add(null, _evenHeader, DialogRowKind.Wide, editorWidth: 440), variants.Add(null, _evenFooter, DialogRowKind.Wide, editorWidth: 440)];
        Show();
        return layout;
    }

    private Control SheetTab()
    {
        var layout = Layout();
        var area = layout.AddSection(_texts["page.section.area"]);
        _area = new RangeFieldControl(new RangeField("printArea", _texts["page.printArea"], _input.PrintArea, _input.AreaSource)
        {
            Optional = true,
            Placeholder = _texts["page.printArea.placeholder"],
        }, _texts, _metrics);
        area.Add(_texts["page.printArea"], _area, editorWidth: 240);
        _repeatRows = Measure(_texts["page.repeat.rowsUnit"], 0, 100, 1, _input.RepeatRows, _texts["page.repeatRows"]);
        _repeatColumns = Measure(_texts["page.repeat.columnsUnit"], 0, 100, 1, _input.RepeatColumns, _texts["page.repeatColumns"]);
        area.Add(_texts["page.repeatRows"], _repeatRows, editorWidth: 170);
        area.Add(_texts["page.repeatColumns"], _repeatColumns, editorWidth: 170);

        var print = layout.AddSection(_texts["page.section.print"]);
        print.Add(null, Check(_texts["page.printGridLines"], _setup.PrintGridLines, value => Change(_setup with { PrintGridLines = value })), DialogRowKind.Indented);
        print.Add(null, Check(_texts["page.printHeadings"], _setup.PrintHeadings, value => Change(_setup with { PrintHeadings = value })), DialogRowKind.Indented);

        var order = layout.AddSection(_texts["page.section.order"]);
        var choice = new IconChoiceGroup(
        [
            new IconChoice("down", "ledger.print.downThenAcross", _texts["page.order.downThenAcross"]),
            new IconChoice("across", "ledger.print.acrossThenDown", _texts["page.order.acrossThenDown"]),
        ], _services.Icons, _metrics, _texts["page.section.order"], _setup.Order == PageOrder.AcrossThenDown ? 1 : 0, showLabels: true);
        choice.SelectionChanged += (_, index) => Change(_setup with { Order = index == 1 ? PageOrder.AcrossThenDown : PageOrder.DownThenAcross });
        order.Add(null, choice, DialogRowKind.Indented, editorWidth: 320);

        var numbering = layout.AddSection(_texts["page.section.numbering"]);
        var firstNumber = Measure(string.Empty, 0, MaximumFirstPageNumber, 1, _setup.FirstPageNumber ?? 0, _texts["page.firstPageNumber"]);
        firstNumber.ValueCommitted += (_, value) => Change(_setup with { FirstPageNumber = Pages(value) });
        numbering.Add(_texts["page.firstPageNumber"], firstNumber, editorWidth: 170);
        numbering.Add(null, new HelpTip(_texts["page.firstPageNumber.help"], _texts, _metrics), DialogRowKind.Indented, editorWidth: 300);

        return layout;
    }

    private const double MinimumPrintableMillimetres = 10;

    private const double MaximumFirstPageNumber = short.MaxValue;

    private MeasureField Millimetres(Twip value, string name) =>
        Measure("mm", 0, 200, 1, Math.Round(Mm(value), 1), name);

    private MeasureField Measure(string unit, double minimum, double maximum, double step, double initial, string name, IReadOnlyList<double>? presets = null) =>
        new(new MeasureEntry(new MeasureOptions(unit, minimum, maximum, step) { Presets = presets ?? [] }, _services.Units, CultureInfo.CurrentCulture),
            initial, _texts, _metrics, _services.Icons, name, boxWidth: 56);

    private static CheckBox Check(string caption, bool value, Action<bool> write)
    {
        var box = new CheckBox { Content = caption, IsChecked = value };
        box.IsCheckedChanged += (_, _) => write(box.IsChecked == true);
        return box;
    }

    private static int? Pages(double value) => value >= 1 ? (int)Math.Round(value) : null;

    private static double Mm(Twip value) => value.ToCentimeters() * 10;

    private string PaperName(PaperSize paper, IReadOnlyList<PaperSize> listed) =>
        listed.Contains(paper) ? _texts[$"page.paper.{paper.Id}"] : _texts["page.paper.custom"];

    private string Size(PaperSize paper) =>
        string.Format(CultureInfo.CurrentCulture, "{0:0} × {1:0} mm", Mm(paper.Width), Mm(paper.Height));
}
