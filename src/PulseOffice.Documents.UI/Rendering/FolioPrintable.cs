using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Documents.UI.Rendering;

public interface IFolioPrintables
{
    IPrintableDocument? From(DocumentLayout layout, TextDocument document, bool? printComments = null);
}

public sealed class FolioPrintables(IPageRenderer renderer, IPageSurfaceFactory surfaces, IFolioChangeBars bars, IFolioViewMarks marks, IFolioCommentSheet comments,
    PulseOffice.Documents.Layout.Flow.IPrintLayouts engines, IFolioPrintMarks printMarks, IFolioLineNumberMarks lines) : IFolioPrintables
{
    public IPrintableDocument? From(DocumentLayout layout, TextDocument document, bool? printComments = null)
    {
        if (layout.Pages.Count == 0) return null;
        var appendix = (printComments ?? marks.PrintComments) && comments.Build(document) is { } sheet ? engines.Layout(sheet).Pages : [];
        return new FolioPrintable(layout, renderer, surfaces, document)
        {
            Marks = lines.Apply(PageMarks.None with { ChangeBar = bars.For(document) }, document, layout),
            Appendix = appendix,
            Navigation = printMarks,
            Index = printMarks.Index(document),
        };
    }
}

public sealed class FolioPrintable : IPrintableDocument
{
    private readonly DocumentLayout _layout;
    private readonly IPageRenderer _renderer;
    private readonly IPageSurfaceFactory _surfaces;

    private readonly TextDocument _document;

    public FolioPrintable(DocumentLayout layout, IPageRenderer renderer, IPageSurfaceFactory surfaces, TextDocument document)
    {
        _document = document;
        _layout = layout;
        _renderer = renderer;
        _surfaces = surfaces;
        var first = layout.Pages[0];
        Setup = new PageSetup
        {
            Paper = new PaperSize("folio", new DocumentSize(Twip.FromPoints(first.Width), Twip.FromPoints(first.Height))),
            Margins = Margins.Zero,
            HeaderHeight = Twip.Zero,
            FooterHeight = Twip.Zero,
        };
    }

    public PageSetup Setup { get; }

    public PageMarks Marks { get; init; } = PageMarks.None;

    public PulseOffice.Documents.Objects.IGroupMemberText GroupText { get; init; } = new PulseOffice.Documents.Objects.GroupMemberText();

    public IReadOnlyList<PageBox> Appendix { get; init; } = [];

    public IFolioPrintMarks? Navigation { get; init; }

    public FolioMarkIndex Index { get; init; } = FolioMarkIndex.Empty;

    public int PageCount => _layout.Pages.Count + Appendix.Count;

    public void PaintPage(int index, IPageCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (index < 0 || index >= PageCount) return;
        if (index >= _layout.Pages.Count)
        {
            _renderer.Paint(_surfaces.Print(canvas, _document.Language), Appendix[index - _layout.Pages.Count]);
            return;
        }
        _renderer.Paint(_surfaces.Print(canvas, _document.Language), _layout.Pages[index],
            id => _document.Objects.Find(id)?.Content is PulseOffice.Documents.Objects.EmbeddedContent embedded ? GroupText.Painted(embedded) : null, Marks);
        Navigation?.Mark(canvas, _layout.Pages[index], Index);
    }
}
