using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Navigation;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class NavigationTests
{
    private readonly Fixture _f = new();
    private readonly ChapterMoving _chapters;
    private readonly DocumentNavigation _navigation;

    public NavigationTests()
    {
        _chapters = new ChapterMoving(_f.Outline);
        _navigation = new DocumentNavigation(_f.Tree, _f.Outline, _f.Bookmarks, new Fields.DocumentReadingOrder(_f.Tree, _f.Stories), _f.Stories);
    }

    private void Build(params (string Id, string Text, string Style)[] paragraphs) =>
        _f.Document.Body.Replace(paragraphs.Select(entry => (Block)new ParagraphBlock(entry.Id, entry.Text, entry.Style)));

    private string Order => string.Join(",", _f.Document.Body.Blocks.Select(block => block.Id));

    private void Book() => Build(
        ("a", "A", DefaultStyleNames.HeadingLevel(1)), ("a1", "testo a", DefaultStyleNames.TextBody),
        ("a2", "A.1", DefaultStyleNames.HeadingLevel(2)), ("a3", "testo a.1", DefaultStyleNames.TextBody),
        ("b", "B", DefaultStyleNames.HeadingLevel(1)), ("b1", "testo b", DefaultStyleNames.TextBody),
        ("c", "C", DefaultStyleNames.HeadingLevel(1)));

    [Fact]
    public void IlCapitoloSiSpostaConTuttiISottocapitoliESiAnnulla()
    {
        Book();
        _chapters.Move(_f.Document, _f.History, "a", "c");
        Assert.Equal("b,b1,a,a1,a2,a3,c", Order);
        _f.History.Undo();
        Assert.Equal("a,a1,a2,a3,b,b1,c", Order);
    }

    [Fact]
    public void IlCapitoloSaleEScendeFraIFratelli()
    {
        Book();
        _chapters.MoveUp(_f.Document, _f.History, "b");
        Assert.Equal("b,b1,a,a1,a2,a3,c", Order);
        _chapters.MoveDown(_f.Document, _f.History, "b");
        Assert.Equal("a,a1,a2,a3,b,b1,c", Order);
        _chapters.MoveUp(_f.Document, _f.History, "a2");
        Assert.Equal("a,a1,a2,a3,b,b1,c", Order);
    }

    [Fact]
    public void IlCapitoloNonSiSpostaDentroSeStesso()
    {
        Book();
        _chapters.Move(_f.Document, _f.History, "a", "a2");
        Assert.Equal("a,a1,a2,a3,b,b1,c", Order);
    }

    [Fact]
    public void AbbassareIlLivelloSpostaAncheISottocapitoli()
    {
        Book();
        _chapters.Shift(_f.Document, _f.History, "a", 1);
        Assert.Equal(DefaultStyleNames.HeadingLevel(2), _f.Paragraph(0).StyleName);
        Assert.Equal(DefaultStyleNames.HeadingLevel(3), _f.Paragraph(2).StyleName);
        Assert.Equal(DefaultStyleNames.HeadingLevel(1), _f.Paragraph(4).StyleName);
    }

    [Fact]
    public void IlNavigatoreElencaIntestazioniSegnalibriEcollegamenti()
    {
        Book();
        _f.Paragraph(1).Marks = [new BookmarkMark(0, "qui", MarkEdge.Point), new BookmarkMark(0, "_Ref1", MarkEdge.Point)];
        _f.Paragraph(3).Content = _f.Paragraph(3).Content.WithRuns([new TextRun("vai "), new TextRun("sito") { Hyperlink = "https://example.org" }]);

        var headings = _navigation.Entries(_f.Document, NavigationCategory.Headings);
        Assert.Equal(["A", "A.1", "B", "C"], headings.Select(entry => entry.Title));
        Assert.Equal(1, headings[1].Depth);
        Assert.Equal("qui", Assert.Single(_navigation.Entries(_f.Document, NavigationCategory.Bookmarks)).Title);
        var link = Assert.Single(_navigation.Entries(_f.Document, NavigationCategory.Hyperlinks));
        Assert.Equal(new NavigationTarget("a3", 4), link.Target);
        Assert.Equal(new NavigationTarget("b", 0), _navigation.Resolve(_f.Document, "#B|outline"));
        Assert.Equal(new NavigationTarget("a1", 0), _navigation.Resolve(_f.Document, "#qui"));
    }
}
