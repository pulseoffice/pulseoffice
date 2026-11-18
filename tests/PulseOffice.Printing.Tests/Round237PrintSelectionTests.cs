using PulseOffice.Foundation.Pages;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class Round237PrintSelectionTests
{
    private sealed class Blank(int pages) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => pages;

        public void PaintPage(int index, IPageCanvas canvas) { }
    }

    [Fact]
    public void A_valid_range_can_still_select_no_page_once_the_subset_applies()
    {
        var imposition = new PrintImposition(new PageRangeParser());
        var options = new PrintJobOptions { PageRange = "1", Subset = PageSubset.Even };

        Assert.NotEmpty(new PageRangeParser().Parse(options.PageRange, 1)!);
        Assert.Empty(imposition.SelectedPages(new Blank(1), options));
        Assert.Equal(0, imposition.Impose(new Blank(1), options, nativeCopies: true).PageCount);
    }
}
