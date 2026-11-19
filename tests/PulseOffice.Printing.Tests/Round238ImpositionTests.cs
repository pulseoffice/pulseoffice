using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class Round238ImpositionTests
{
    private static readonly DocumentSize Portrait = new(Twip.FromMillimeters(210), Twip.FromMillimeters(297));
    private static readonly DocumentSize Wide = new(Twip.FromMillimeters(297), Twip.FromMillimeters(210));

    private sealed class Mixed(params DocumentSize[] sizes) : IPrintableDocument
    {
        public PageSetup Setup { get; } = new() { Paper = new PaperCatalog().Default };

        public int PageCount => sizes.Length;

        public DocumentSize PageSize(int index) => sizes[index];

        public void PaintPage(int index, IPageCanvas canvas) { }
    }

    private readonly PrintImposition _imposition = new(new PageRangeParser());

    [Fact]
    public void Each_page_on_a_sheet_is_scaled_by_its_own_size()
    {
        var imposed = (ImposedDocument)_imposition.Impose(new Mixed(Portrait, Wide), new PrintJobOptions { PagesPerSheet = 2 }, nativeCopies: true);
        var slots = imposed.Sheets[0];

        Assert.True(slots[1].Area.Width.Value > slots[1].Area.Height.Value);
        Assert.True(slots[0].Area.Width.Value < slots[0].Area.Height.Value);
    }

    [Fact]
    public void Brochure_halves_fit_each_page()
    {
        var imposed = (ImposedDocument)_imposition.Impose(new Mixed(Portrait, Wide, Portrait, Portrait), new PrintJobOptions { Brochure = true }, nativeCopies: true);
        var wide = imposed.Sheets.SelectMany(sheet => sheet).Single(slot => slot.Page == 1);

        Assert.True(wide.Area.Width.Value > wide.Area.Height.Value);
    }

    [Fact]
    public void Brochure_forces_short_edge_duplex()
    {
        Assert.Equal(DuplexMode.ShortEdge, new PrintJobOptions { Brochure = true }.EffectiveDuplex);
        Assert.Equal(DuplexMode.LongEdge, new PrintJobOptions { Duplex = DuplexMode.LongEdge }.EffectiveDuplex);
    }

    [Fact]
    public void Chosen_paper_and_orientation_set_the_sheet_and_fit_the_page()
    {
        var letter = new DocumentSize(Twip.FromInches(8.5), Twip.FromInches(11));
        var imposed = (ImposedDocument)_imposition.Impose(new Mixed(Portrait), new PrintJobOptions { Paper = letter, Orientation = SheetOrientation.Landscape }, nativeCopies: true);

        Assert.Equal(letter.Height, imposed.PageSize(0).Width);
        Assert.Equal(letter.Width, imposed.PageSize(0).Height);
        Assert.True(imposed.Sheets[0][0].Scale < 1);
    }
}
