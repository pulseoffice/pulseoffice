using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Pages;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class PageStyleTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void IntestazionePrimaPaginaEPariSonoStorieModificabili()
    {
        var page = _f.Document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        page.Header.Enabled = true;
        page.FirstPageDifferent = true;
        page.Header.Default.Add(new ParagraphBlock("ph", "Intestazione", DefaultStyleNames.Header));
        page.Header.First = new BlockContainer([new ParagraphBlock("pf", "Copertina", DefaultStyleNames.Header)]);

        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition("pf", 9), "!");

        Assert.Equal("Copertina!", ((ParagraphBlock)page.Header.First[0]).Text);
        var copy = (PageStyle)page.Clone();
        Assert.Equal("Copertina", ((ParagraphBlock)copy.Header.First![0]).Text[..9]);
        _f.History.Undo();
        Assert.Equal("Copertina", ((ParagraphBlock)page.Header.First[0]).Text);
        Assert.Equal("Copertina!", ((ParagraphBlock)copy.Header.First![0]).Text);
    }

    [Fact]
    public void LOrientamentoOrizzontaleScambiaLeMisure()
    {
        var page = new PageStyle("P") { Orientation = PageOrientation.Landscape };

        Assert.True(page.Width.Value > page.Height.Value);
    }

    [Fact]
    public void ILivelliDegliElenchiSonoLimitati()
    {
        var list = _f.Document.Styles.List.Find(DefaultStyleNames.List1)!;

        Assert.Equal(NumberFormat.Bullet, list.Level(42).Format);
        Assert.Equal(18, list.Level(0).Indent);
        Assert.Equal(")", _f.Document.Styles.List.Find(DefaultStyleNames.NumberingAbc)!.Level(3).Suffix);
    }
}
