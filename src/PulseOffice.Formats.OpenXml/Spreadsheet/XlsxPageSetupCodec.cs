using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using Book = PulseOffice.Sheets.Workbook;
using HeaderFooter = PulseOffice.Foundation.Pages.HeaderFooter;
using Sheet = PulseOffice.Sheets.Worksheet;
using XlsxHeaderFooter = DocumentFormat.OpenXml.Spreadsheet.HeaderFooter;
using PageSetup = DocumentFormat.OpenXml.Spreadsheet.PageSetup;

namespace PulseOffice.Formats.OpenXml;

internal sealed record XlsxPaper(uint Code, string Paper);

internal sealed class XlsxHeaderFooterText
{
    private readonly IReadOnlyList<(char Code, Func<HeaderFooter, string> Get)> _regions =
    [
        ('L', content => content.Left),
        ('C', content => content.Center),
        ('R', content => content.Right),
    ];

    public string? Write(HeaderFooter content) =>
        content.IsEmpty
            ? null
            : string.Concat(_regions.Where(region => region.Get(content).Length > 0).Select(region => "&" + region.Code + region.Get(content)));

    public HeaderFooter Read(string? text)
    {
        if (string.IsNullOrEmpty(text)) return HeaderFooter.Empty;
        var parts = new Dictionary<char, StringBuilder> { ['L'] = new(), ['C'] = new(), ['R'] = new() };
        var current = 'C';
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '&' && i + 1 < text.Length)
            {
                var next = char.ToUpperInvariant(text[i + 1]);
                if (parts.ContainsKey(next))
                {
                    current = next;
                    i++;
                    continue;
                }
                parts[current].Append(text, i, 2);
                i++;
                continue;
            }
            parts[current].Append(text[i]);
        }
        return new HeaderFooter(parts['L'].ToString(), parts['C'].ToString(), parts['R'].ToString());
    }
}

internal sealed class XlsxPageSetupCodec
{
    private const int MaxColumn = 16383;
    private const int MaxRow = 1048575;
    private const double MaximumMarginInches = 49;

    private readonly IPaperCatalog _papers;

    public XlsxPageSetupCodec(IPaperCatalog? papers = null) => _papers = papers ?? new PaperCatalog();

    private readonly XlsxHeaderFooterText _text = new();
    private readonly IReadOnlyList<XlsxPaper> _codes =
    [
        new(1, "letter"), new(3, "tabloid"), new(5, "legal"), new(7, "executive"),
        new(8, "a3"), new(9, "a4"), new(11, "a5"), new(34, "b5"),
    ];

    public IEnumerable<OpenXmlElement> Build(Sheet sheet, Book workbook)
    {
        var setup = workbook.PageStyles.For(sheet).Setup;

        yield return new PrintOptions
        {
            GridLines = setup.PrintGridLines,
            Headings = setup.PrintHeadings,
            HorizontalCentered = setup.CenterHorizontally,
            VerticalCentered = setup.CenterVertically,
        };

        yield return new PageMargins
        {
            Left = setup.Margins.Left.ToInches(),
            Right = setup.Margins.Right.ToInches(),
            Top = (setup.Margins.Top + setup.HeaderHeight).ToInches(),
            Bottom = (setup.Margins.Bottom + setup.FooterHeight).ToInches(),
            Header = setup.Margins.Top.ToInches(),
            Footer = setup.Margins.Bottom.ToInches(),
        };

        var page = new PageSetup
        {
            Orientation = setup.Orientation == PageOrientation.Landscape ? OrientationValues.Landscape : OrientationValues.Portrait,
            PageOrder = setup.Order == PageOrder.AcrossThenDown ? PageOrderValues.OverThenDown : PageOrderValues.DownThenOver,
            Scale = (uint)Math.Clamp(Math.Round(setup.Scale * 100), 10, 400),
        };
        if (_codes.FirstOrDefault(code => code.Paper == setup.Paper.Id) is { } paper) page.PaperSize = paper.Code;
        if (setup.FirstPageNumber is { } first)
        {
            page.FirstPageNumber = (uint)Math.Clamp(first, 0, short.MaxValue);
            page.UseFirstPageNumber = true;
        }
        if (setup.FitsToPages)
        {
            page.FitToWidth = (uint)Math.Max(setup.FitToWidthPages ?? 0, 0);
            page.FitToHeight = (uint)Math.Max(setup.FitToHeightPages ?? 0, 0);
        }
        yield return page;

        var headerFooter = new XlsxHeaderFooter();
        if (_text.Write(setup.Header) is { } header) headerFooter.OddHeader = new OddHeader(header);
        if (_text.Write(setup.Footer) is { } footer) headerFooter.OddFooter = new OddFooter(footer);
        if (setup.DifferentOddEven)
        {
            headerFooter.DifferentOddEven = true;
            if (_text.Write(setup.EvenHeader) is { } evenHeader) headerFooter.EvenHeader = new EvenHeader(evenHeader);
            if (_text.Write(setup.EvenFooter) is { } evenFooter) headerFooter.EvenFooter = new EvenFooter(evenFooter);
        }
        if (setup.DifferentFirstPage)
        {
            headerFooter.DifferentFirst = true;
            if (_text.Write(setup.FirstHeader) is { } firstHeader) headerFooter.FirstHeader = new FirstHeader(firstHeader);
            if (_text.Write(setup.FirstFooter) is { } firstFooter) headerFooter.FirstFooter = new FirstFooter(firstFooter);
        }
        if (headerFooter.HasChildren || headerFooter.HasAttributes) yield return headerFooter;

        if (sheet.RowBreaks.Count > 0)
            yield return new RowBreaks(sheet.RowBreaks.Select(row => new Break { Id = (uint)row, Max = MaxColumn, ManualPageBreak = true }))
            {
                Count = (uint)sheet.RowBreaks.Count,
                ManualBreakCount = (uint)sheet.RowBreaks.Count,
            };

        if (sheet.ColumnBreaks.Count > 0)
            yield return new ColumnBreaks(sheet.ColumnBreaks.Select(column => new Break { Id = (uint)column, Max = MaxRow, ManualPageBreak = true }))
            {
                Count = (uint)sheet.ColumnBreaks.Count,
                ManualBreakCount = (uint)sheet.ColumnBreaks.Count,
            };
    }

    public bool WritesPaper(PaperSize paper) => _codes.Any(code => code.Paper == paper.Id);

    public bool FitsToPage(Sheet sheet, Book workbook) => workbook.PageStyles.For(sheet).Setup.FitsToPages;

    public void Read(WorksheetPart part, Sheet sheet, Book workbook)
    {
        if (part.Worksheet is not { } worksheet) return;

        foreach (var brk in worksheet.GetFirstChild<RowBreaks>()?.Elements<Break>() ?? [])
            if (brk.Id?.Value is { } row and > 0 and <= MaxRow) sheet.RowBreaks.Add((int)row);
        foreach (var brk in worksheet.GetFirstChild<ColumnBreaks>()?.Elements<Break>() ?? [])
            if (brk.Id?.Value is { } column and > 0 and <= MaxColumn) sheet.ColumnBreaks.Add((int)column);

        var margins = worksheet.GetFirstChild<PageMargins>();
        var page = worksheet.GetFirstChild<PageSetup>();
        var options = worksheet.GetFirstChild<PrintOptions>();
        var headerFooter = worksheet.GetFirstChild<XlsxHeaderFooter>();
        if (margins is null && page is null && options is null && headerFooter is null) return;

        var fallback = workbook.PageStyles.Default.Setup;
        var fits = worksheet.GetFirstChild<SheetProperties>()?.PageSetupProperties?.FitToPage?.Value == true;
        var top = Inches(margins?.Top?.Value);
        var bottom = Inches(margins?.Bottom?.Value);
        var header = Inches(margins?.Header?.Value);
        var footer = Inches(margins?.Footer?.Value);

        var setup = fallback with
        {
            Paper = page?.PaperSize?.Value is { } code && _codes.FirstOrDefault(entry => entry.Code == code) is { } paper
                ? _papers.Find(paper.Paper) ?? fallback.Paper
                : fallback.Paper,
            Orientation = page?.Orientation?.Value == OrientationValues.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait,
            Order = page?.PageOrder?.Value == PageOrderValues.OverThenDown ? PageOrder.AcrossThenDown : PageOrder.DownThenAcross,
            Scale = page?.Scale?.Value is { } scale and > 0 ? Math.Clamp(scale, 10u, 400u) / 100.0 : 1.0,
            FitToWidthPages = fits && page?.FitToWidth?.Value is { } across and > 0 ? (int)Math.Min(across, short.MaxValue) : null,
            FitToHeightPages = fits && page?.FitToHeight?.Value is { } down and > 0 ? (int)Math.Min(down, short.MaxValue) : null,
            Margins = margins is null
                ? fallback.Margins
                : new Margins(Inches(margins.Left?.Value) ?? fallback.Margins.Left, header ?? fallback.Margins.Top,
                    Inches(margins.Right?.Value) ?? fallback.Margins.Right, footer ?? fallback.Margins.Bottom),
            HeaderHeight = top is { } t && header is { } h && t.Value >= h.Value ? t - h : fallback.HeaderHeight,
            FooterHeight = bottom is { } b && footer is { } f && b.Value >= f.Value ? b - f : fallback.FooterHeight,
            Header = _text.Read(headerFooter?.OddHeader?.Text),
            Footer = _text.Read(headerFooter?.OddFooter?.Text),
            DifferentOddEven = headerFooter?.DifferentOddEven?.Value == true,
            DifferentFirstPage = headerFooter?.DifferentFirst?.Value == true,
            EvenHeader = headerFooter?.DifferentOddEven?.Value == true ? _text.Read(headerFooter.EvenHeader?.Text) : HeaderFooter.Empty,
            EvenFooter = headerFooter?.DifferentOddEven?.Value == true ? _text.Read(headerFooter.EvenFooter?.Text) : HeaderFooter.Empty,
            FirstHeader = headerFooter?.DifferentFirst?.Value == true ? _text.Read(headerFooter.FirstHeader?.Text) : HeaderFooter.Empty,
            FirstFooter = headerFooter?.DifferentFirst?.Value == true ? _text.Read(headerFooter.FirstFooter?.Text) : HeaderFooter.Empty,
            FirstPageNumber = page?.UseFirstPageNumber?.Value == true && page.FirstPageNumber?.Value is { } number
                ? (int)Math.Min(number, (uint)short.MaxValue)
                : null,
            PrintGridLines = options?.GridLines?.Value == true,
            PrintHeadings = options?.Headings?.Value == true,
            CenterHorizontally = options?.HorizontalCentered?.Value == true,
            CenterVertically = options?.VerticalCentered?.Value == true,
        };

        if (setup == fallback) return;
        var existing = workbook.PageStyles.All.FirstOrDefault(style => style.Setup == setup);
        sheet.PageStyleName = existing?.Name ?? workbook.PageStyles.Define("PageStyle_" + sheet.Name, setup).Name;
    }

    private Twip? Inches(double? value) => value is { } inches && double.IsFinite(inches) ? Twip.FromInches(Math.Clamp(inches, 0, MaximumMarginInches)) : null;
}
