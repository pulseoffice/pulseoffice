using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public enum DeckExportLayout
{
    Slides,
    Notes,
    Handout,
    Handout2,
    Handout3,
    Handout4,
    Handout6,
    Handout9,
}

public sealed class DeckPrintable : IPrintableDocument
{
    private readonly IReadOnlyList<PulseOffice.Presentations.UI.Rendering.IPrintableSlide> _slides;
    private readonly IReadOnlyList<string> _notes;
    private readonly DeckExportLayout _layout;
    private readonly NotesMaster _notesMaster;
    private readonly HandoutMaster _handout;
    private readonly IReadOnlyList<PageBox> _cells;

    public DeckPrintable(
        PageSetup setup, IReadOnlyList<PulseOffice.Presentations.UI.Rendering.IPrintableSlide> slides, IReadOnlyList<string> notes, DeckExportLayout layout,
        NotesMaster? notesMaster = null, HandoutMaster? handout = null, IHandoutLayout? handoutLayout = null,
        ITextWrapper? wrapper = null)
    {
        _wrapper = wrapper ?? new TextWrapper();
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentNullException.ThrowIfNull(notes);

        Setup = setup;
        _slides = slides;
        _notes = notes;
        _layout = layout;
        _notesMaster = notesMaster ?? new NotesMaster();
        _handout = handout ?? new HandoutMaster();

        var perPage = layout switch
        {
            DeckExportLayout.Handout2 => 2,
            DeckExportLayout.Handout3 => 3,
            DeckExportLayout.Handout4 => 4,
            DeckExportLayout.Handout6 => 6,
            DeckExportLayout.Handout9 => 9,
            DeckExportLayout.Handout => _handout.SlidesPerPage,
            _ => 1,
        };
        _cells = (handoutLayout ?? new HandoutLayout()).Cells(perPage);

        PageCount = layout is DeckExportLayout.Slides or DeckExportLayout.Notes
            ? slides.Count
            : (slides.Count + _cells.Count - 1) / _cells.Count;
    }

    private readonly ITextWrapper _wrapper;

    public PageSetup Setup { get; }

    public int PageCount { get; }

    public IReadOnlyList<string> Titles { get; init; } = [];

    public string? Language { get; init; }

    public void PaintPage(int index, IPageCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (_layout is DeckExportLayout.Slides or DeckExportLayout.Notes && index < Titles.Count)
            canvas.MarkHeading(1, Titles[index] is { Length: > 0 } title ? title : (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture), 0);

        switch (_layout)
        {
            case DeckExportLayout.Slides:
                PaintSlide(canvas, index, Setup.ContentArea);
                break;
            case DeckExportLayout.Notes:
                PaintNotes(canvas, index);
                PaintChrome(canvas, index, _notesMaster.Header, _notesMaster.Footer, _notesMaster.PageNumber);
                break;
            default:
                PaintHandout(canvas, index);
                PaintChrome(canvas, index, _handout.Header, _handout.Footer, _handout.PageNumber);
                break;
        }
    }

    private DocumentRect Place(PageBox box) => new(
        new Twip((int)Math.Round(box.X * Setup.PageWidth.Value)),
        new Twip((int)Math.Round(box.Y * Setup.PageHeight.Value)),
        new Twip((int)Math.Round(box.Width * Setup.PageWidth.Value)),
        new Twip((int)Math.Round(box.Height * Setup.PageHeight.Value)));

    private void PaintSlide(IPageCanvas canvas, int slideIndex, DocumentRect area)
    {
        if (slideIndex < 0 || slideIndex >= _slides.Count) return;
        _slides[slideIndex].Paint(canvas, Fit(_slides[slideIndex], area));
    }

    private void PaintNotes(IPageCanvas canvas, int slideIndex)
    {
        if (slideIndex < 0 || slideIndex >= _slides.Count) return;

        var fitted = Fit(_slides[slideIndex], Place(_notesMaster.SlideBox));
        _slides[slideIndex].Paint(canvas, fitted);
        Outline(canvas, fitted);

        var area = Place(_notesMaster.NotesBox);
        var text = slideIndex < _notes.Count ? _notes[slideIndex] : string.Empty;
        canvas.Tag(_slides[slideIndex].Structure is { } slideTag && text.Trim().Length > 0 ? new PrintTag(slideTag.Key + ":notes", PrintRole.Note, slideTag) : null);
        var size = _notesMaster.FontSize;
        var minimum = _notesMaster.FontSize * MinimumNotesScale;
        var (font, lines, lineHeight) = NotesLines(canvas, text, area, size);
        while (lines.Count * lineHeight > area.Height.Value && size > minimum)
        {
            size = Math.Max(minimum, size - 0.5);
            (font, lines, lineHeight) = NotesLines(canvas, text, area, size);
        }
        var fitting = Math.Max(0, Math.Min(lines.Count, area.Height.Value / Math.Max(1, lineHeight)));
        for (var index = 0; index < fitting; index++)
        {
            var line = index == fitting - 1 && fitting < lines.Count ? lines[index].TrimEnd() + " …" : lines[index];
            canvas.DrawText(line, new DocumentRect(area.Left, new Twip(area.Top.Value + index * lineHeight), area.Width, new Twip(lineHeight)),
                font, PrintColor.Black, PulseOffice.Printing.TextAlignment.Left, PulseOffice.Printing.TextBaseline.Top);
        }
        canvas.Tag(null);
    }

    private const double MinimumNotesScale = 0.6;

    private (PrintFont Font, IReadOnlyList<string> Lines, int LineHeight) NotesLines(IPageCanvas canvas, string text, DocumentRect area, double size)
    {
        var font = new PrintFont("Helvetica", size);
        var lines = _wrapper.Wrap(text, area.Width, part => canvas.MeasureText(part, font)).ToList();
        return (font, lines, Math.Max(1, Twip.FromPoints(size * 1.25).Value));
    }

    private void PaintHandout(IPageCanvas canvas, int pageIndex)
    {
        for (var cell = 0; cell < _cells.Count; cell++)
        {
            var slideIndex = pageIndex * _cells.Count + cell;
            if (slideIndex >= _slides.Count) break;

            var fitted = Fit(_slides[slideIndex], Place(_cells[cell]));
            _slides[slideIndex].Paint(canvas, fitted);
            Outline(canvas, fitted);
        }
    }

    private void PaintChrome(IPageCanvas canvas, int pageIndex, string header, string footer, bool pageNumber)
    {
        var font = new PrintFont("Helvetica", 9);
        var band = Twip.FromMillimeters(6);
        var side = Twip.FromMillimeters(12);
        var width = new Twip(Setup.PageWidth.Value - 2 * side.Value);
        var top = new DocumentRect(side, Twip.FromMillimeters(6), width, band);
        var bottom = new DocumentRect(side, new Twip(Setup.PageHeight.Value - Twip.FromMillimeters(12).Value), width, band);

        if (header.Length > 0)
            canvas.DrawText(header, top, font, PrintColor.Black, PulseOffice.Printing.TextAlignment.Left, PulseOffice.Printing.TextBaseline.Top);
        if (footer.Length > 0)
            canvas.DrawText(footer, bottom, font, PrintColor.Black, PulseOffice.Printing.TextAlignment.Left, PulseOffice.Printing.TextBaseline.Top);
        if (pageNumber)
            canvas.DrawText((pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), bottom, font,
                PrintColor.Black, PulseOffice.Printing.TextAlignment.Right, PulseOffice.Printing.TextBaseline.Top);
    }

    private void Outline(IPageCanvas canvas, DocumentRect fitted)
    {
        var width = new Twip(15);
        canvas.DrawLine(new DocumentPoint(fitted.Left, fitted.Top), new DocumentPoint(fitted.Right, fitted.Top), PrintColor.Grid, width);
        canvas.DrawLine(new DocumentPoint(fitted.Right, fitted.Top), new DocumentPoint(fitted.Right, fitted.Bottom), PrintColor.Grid, width);
        canvas.DrawLine(new DocumentPoint(fitted.Right, fitted.Bottom), new DocumentPoint(fitted.Left, fitted.Bottom), PrintColor.Grid, width);
        canvas.DrawLine(new DocumentPoint(fitted.Left, fitted.Bottom), new DocumentPoint(fitted.Left, fitted.Top), PrintColor.Grid, width);
    }

    private DocumentRect Fit(PulseOffice.Presentations.UI.Rendering.IPrintableSlide slide, DocumentRect area)
    {
        if (slide.Ratio <= 0 || area.Width.Value <= 0 || area.Height.Value <= 0)
            return area;

        var imageRatio = slide.Ratio;
        var areaRatio = area.Height.Value / (double)area.Width.Value;

        int width, height;
        if (imageRatio <= areaRatio)
        {
            width = area.Width.Value;
            height = (int)Math.Round(width * imageRatio);
        }
        else
        {
            height = area.Height.Value;
            width = (int)Math.Round(height / imageRatio);
        }

        var left = area.Left.Value + (area.Width.Value - width) / 2;
        var top = area.Top.Value + (area.Height.Value - height) / 2;

        return new DocumentRect(new Twip(left), new Twip(top), new Twip(width), new Twip(height));
    }
}
