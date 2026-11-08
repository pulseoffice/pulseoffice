using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class TitlePageTests
{
    private readonly Fixture _f = new();
    private readonly TitlePages _titles;

    public TitlePageTests()
    {
        _titles = new TitlePages(_f.Editor);
        _f.Type("Primo", "Secondo");
    }

    [Fact]
    public void InserireDuePagineDelTitoloPreparaIParagrafiEFaRipartireLaNumerazione()
    {
        _titles.Insert(_f.Document, _f.History, new TitlePageSettings(2, DefaultStyleNames.FirstPage, 1));

        var blocks = _f.Document.Body.Blocks.Cast<ParagraphBlock>().ToList();
        Assert.Equal(4, blocks.Count);
        Assert.Equal(DefaultStyleNames.FirstPage, blocks[0].Content.PageStyleName);
        Assert.Equal(string.Empty, blocks[0].Text);
        Assert.True(blocks[1].Content.PageBreakBefore);
        Assert.Null(blocks[1].Content.PageStyleName);
        Assert.Equal("Primo", blocks[2].Text);
        Assert.Equal(DefaultStyleNames.Standard, blocks[2].Content.PageStyleName);
        Assert.Equal(1, blocks[2].Content.PageNumberRestart);

        _f.History.Undo();

        var restored = _f.Document.Body.Blocks.Cast<ParagraphBlock>().ToList();
        Assert.Equal(2, restored.Count);
        Assert.Equal("Primo", restored[0].Text);
        Assert.Null(restored[0].Content.PageStyleName);

        _f.History.Redo();

        var redone = _f.Document.Body.Blocks.Cast<ParagraphBlock>().ToList();
        Assert.Equal(4, redone.Count);
        Assert.Equal(DefaultStyleNames.FirstPage, redone[0].Content.PageStyleName);
        Assert.Equal(1, redone[2].Content.PageNumberRestart);
    }

    [Fact]
    public void ConvertirePagineEsistentiAssegnaSoloGliStiliDiPagina()
    {
        var second = _f.Paragraph(1).Id;

        _titles.Convert(_f.Document, _f.History, second, new TitlePageSettings(1, DefaultStyleNames.FirstPage, null));

        Assert.Equal(2, _f.Document.Body.Blocks.Count);
        Assert.Equal(DefaultStyleNames.FirstPage, _f.Paragraph(0).Content.PageStyleName);
        Assert.Equal(DefaultStyleNames.Standard, _f.Paragraph(1).Content.PageStyleName);
        Assert.Null(_f.Paragraph(1).Content.PageNumberRestart);
    }
}
