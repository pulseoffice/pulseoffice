using PulseOffice.Foundation.Units;

namespace PulseOffice.Foundation.Pages;

public enum PageOrientation
{
    Portrait,
    Landscape,
}

public enum PageOrder
{
    DownThenAcross,
    AcrossThenDown,
}

public sealed record HeaderFooter(string Left = "", string Center = "", string Right = "")
{
    public static readonly HeaderFooter Empty = new();

    public bool IsEmpty =>
        string.IsNullOrEmpty(Left) && string.IsNullOrEmpty(Center) && string.IsNullOrEmpty(Right);
}

public sealed record PageSetup
{
    public required PaperSize Paper { get; init; }

    public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;

    public Margins Margins { get; init; } = new(
        Twip.FromMillimeters(20), Twip.FromMillimeters(20),
        Twip.FromMillimeters(20), Twip.FromMillimeters(20));

    public Twip HeaderHeight { get; init; } = Twip.FromMillimeters(10);

    public Twip FooterHeight { get; init; } = Twip.FromMillimeters(10);

    public HeaderFooter Header { get; init; } = HeaderFooter.Empty;

    public HeaderFooter Footer { get; init; } = HeaderFooter.Empty;

    public bool DifferentFirstPage { get; init; }

    public bool DifferentOddEven { get; init; }

    public HeaderFooter FirstHeader { get; init; } = HeaderFooter.Empty;

    public HeaderFooter FirstFooter { get; init; } = HeaderFooter.Empty;

    public HeaderFooter EvenHeader { get; init; } = HeaderFooter.Empty;

    public HeaderFooter EvenFooter { get; init; } = HeaderFooter.Empty;

    public int? FirstPageNumber { get; init; }

    public HeaderFooter HeaderOn(bool firstPage, int pageNumber) =>
        DifferentFirstPage && firstPage ? FirstHeader
        : DifferentOddEven && pageNumber % 2 == 0 ? EvenHeader
        : Header;

    public HeaderFooter FooterOn(bool firstPage, int pageNumber) =>
        DifferentFirstPage && firstPage ? FirstFooter
        : DifferentOddEven && pageNumber % 2 == 0 ? EvenFooter
        : Footer;

    public double Scale { get; init; } = 1.0;

    public int? FitToWidthPages { get; init; }

    public int? FitToHeightPages { get; init; }

    public bool FitsToPages => FitToWidthPages is not null || FitToHeightPages is not null;

    public PageOrder Order { get; init; } = PageOrder.DownThenAcross;

    public bool PrintGridLines { get; init; }

    public bool PrintHeadings { get; init; }

    public bool CenterHorizontally { get; init; }

    public bool CenterVertically { get; init; }

    public Twip PageWidth => Orientation == PageOrientation.Portrait ? Paper.Width : Paper.Height;

    public Twip PageHeight => Orientation == PageOrientation.Portrait ? Paper.Height : Paper.Width;

    public DocumentRect ContentArea => new(
        Margins.Left,
        Margins.Top + HeaderHeight,
        PageWidth - Margins.Horizontal,
        PageHeight - Margins.Vertical - HeaderHeight - FooterHeight);

    public DocumentRect HeaderArea => new(Margins.Left, Margins.Top, PageWidth - Margins.Horizontal, HeaderHeight);

    public DocumentRect FooterArea => new(
        Margins.Left, PageHeight - Margins.Bottom - FooterHeight, PageWidth - Margins.Horizontal, FooterHeight);
}
