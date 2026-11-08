using System.Text;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public interface IDeckPublisher
{
    int ExportWidth { get; }

    string SuggestedName(Presentation deck);

    ISlideImageRenderer Renderer();

    IPrintableDocument BuildPrintable(Presentation deck, DeckExportLayout layout);

    IPrintableDocument BuildEverySlide(Presentation deck);

    IPrintableDocument BuildExportable(Presentation deck, DeckExportLayout layout);

    string BuildHtml(Presentation deck);

    int WriteHtml(Presentation deck, TextWriter writer);
}

public sealed class DeckPublisher(IDrawingObjectRenderer objectRenderer, ITextCatalog texts) : IDeckPublisher
{
    public int ExportWidth => 1600;

    private static readonly char[] Unsafe = [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public string SuggestedName(Presentation deck)
    {
        var title = deck.Slides.FirstOrDefault()?.Placeholder(PlaceholderRole.Title)?.Text.PlainText ?? string.Empty;
        var line = title.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        var safe = string.Join(" ", line.Split(Unsafe, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim().TrimEnd('.');
        if (safe.Length > 80) safe = safe[..80].Trim();
        return safe.Length == 0 ? texts["document.defaultName"] : safe;
    }

    public ISlideImageRenderer Renderer() =>
        new SlideImageRenderer(new SlideRenderer(objectRenderer, texts));

    public IPrintableDocument BuildPrintable(Presentation deck, DeckExportLayout layout) =>
        Build(deck, layout, Paper(layout), slide => !slide.Hidden);

    public IPrintableDocument BuildEverySlide(Presentation deck) =>
        Build(deck, DeckExportLayout.Slides, Paper(DeckExportLayout.Slides), _ => true);

    private PageSetup Paper(DeckExportLayout layout) => new()
    {
        Paper = new PaperSize("a4", new DocumentSize(Twip.FromMillimeters(210), Twip.FromMillimeters(297))),
        Orientation = layout == DeckExportLayout.Slides ? PageOrientation.Landscape : PageOrientation.Portrait,
        HeaderHeight = new Twip(0),
        FooterHeight = new Twip(0),
    };

    public IPrintableDocument BuildExportable(Presentation deck, DeckExportLayout layout) =>
        layout == DeckExportLayout.Slides
            ? Build(deck, layout, new PageSetup
            {
                Paper = new PaperSize("slide", new DocumentSize(deck.Size.Width, deck.Size.Height)),
                Margins = new Margins(new Twip(0), new Twip(0), new Twip(0), new Twip(0)),
                HeaderHeight = new Twip(0),
                FooterHeight = new Twip(0),
            }, slide => !slide.Hidden)
            : BuildPrintable(deck, layout);

    private DeckPrintable Build(Presentation deck, DeckExportLayout layout, PageSetup setup, Func<Slide, bool> included)
    {
        var painter = new PulseOffice.Presentations.UI.Rendering.SlidePagePainter(new SlideRenderer(objectRenderer, texts));
        var shown = deck.Slides.Where(included).ToList();
        var images = shown.Select(slide => painter.For(deck, slide)).ToList();
        var notes = shown.Select(slide => slide.Notes ?? string.Empty).ToList();

        var fields = new SlideFieldsFactory();
        return new DeckPrintable(setup, images, notes, layout, deck.NotesMaster, deck.Handout)
        {
            Titles = [.. shown.Select(slide => fields.For(deck, slide).Title)],
        };
    }

    public string BuildHtml(Presentation deck)
    {
        using var writer = new StringWriter();
        WriteHtml(deck, writer);
        return writer.ToString();
    }

    public int WriteHtml(Presentation deck, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(writer);
        var renderer = Renderer();
        writer.Write("<!doctype html><html><head><meta charset=\"utf-8\"><title>");
        writer.Write(System.Net.WebUtility.HtmlEncode(SuggestedName(deck)));
        writer.Write("</title><style>body{margin:0;background:#111;font-family:sans-serif}");
        writer.Write("img{display:block;max-width:100%;margin:0 auto}section{min-height:100vh;");
        writer.Write("display:flex;align-items:center;justify-content:center}</style></head><body>");

        var written = 0;
        foreach (var slide in deck.Slides.Where(slide => !slide.Hidden))
        {
            var png = renderer.RenderPng(deck, slide, ExportWidth);
            writer.Write("<section><img src=\"data:image/png;base64,");
            writer.Write(Convert.ToBase64String(png));
            writer.Write("\"></section>");
            writer.Flush();
            written++;
        }

        writer.Write("</body></html>");
        writer.Flush();
        return written;
    }
}
