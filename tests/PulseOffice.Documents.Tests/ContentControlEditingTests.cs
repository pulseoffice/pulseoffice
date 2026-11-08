using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ContentControlEditingTests
{
    private readonly Fixture _f = new();
    private readonly ContentControlCatalog _catalog;
    private readonly ContentControlEditing _editing;
    private readonly ContentControlGuard _guard;

    public ContentControlEditingTests()
    {
        _catalog = new ContentControlCatalog(_f.Stories, _f.Tree);
        _editing = new ContentControlEditing(_catalog, _f.Stories, _f.Text, _f.Deletion, _f.Trails, new MarkOrder(), new PulseOffice.Text.Rich.TextCultures());
        _guard = new ContentControlGuard(_catalog);
    }

    private ParagraphBlock First => _f.Document.Body.Blocks.OfType<ParagraphBlock>().First();

    private ContentControlSpan Only => Assert.Single(_catalog.All(_f.Document));

    private string Inside(ContentControlSpan span) => First.Text[span.Start.Offset..span.End.Offset];

    [Fact]
    public void UnControlloVuotoMostraIlSegnapostoESiRiempieInUnPasso()
    {
        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(First.Id, 0), "Nome: ");
        _editing.Insert(_f.Document, _f.History, DocumentRange.At(new DocumentPosition(First.Id, 6)), new ContentControl { Kind = ContentControlKind.PlainText, PlaceholderText = "Scrivi" });
        Assert.Equal("Nome: Scrivi", First.Text);
        Assert.True(Only.Control.ShowingPlaceholder);

        _editing.Fill(_f.Document, _f.History, Only.Id, "Ada");
        Assert.Equal("Nome: Ada", First.Text);
        Assert.Equal("Ada", Inside(Only));
        Assert.False(Only.Control.ShowingPlaceholder);

        _f.History.Undo();
        Assert.Equal("Nome: Scrivi", First.Text);
        Assert.True(Only.Control.ShowingPlaceholder);
        _f.History.Redo();
        Assert.Equal("Ada", Inside(Only));
    }

    [Fact]
    public void LaCasellaSiCommutaEIlSimboloCambia()
    {
        _editing.Insert(_f.Document, _f.History, DocumentRange.At(new DocumentPosition(First.Id, 0)), new ContentControl { Kind = ContentControlKind.CheckBox });
        Assert.Equal("☐", First.Text);
        _editing.Toggle(_f.Document, _f.History, Only.Id);
        Assert.Equal("☒", First.Text);
        Assert.True(Only.Control.Checked);
        _f.History.Undo();
        Assert.Equal("☐", First.Text);
        Assert.False(Only.Control.Checked);
    }

    [Fact]
    public void ElencoEDataSostituisconoIlContenutoSenzaToccareIVicini()
    {
        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(First.Id, 0), "AB");
        _editing.Insert(_f.Document, _f.History, DocumentRange.In(First.Id, 0, 1), new ContentControl { Kind = ContentControlKind.DropDownList, Items = [new ContentControlItem("Uno", "1")] });
        _editing.Insert(_f.Document, _f.History, DocumentRange.In(First.Id, 1, 2), new ContentControl { Kind = ContentControlKind.Date, DateFormat = "yyyy-MM-dd" });
        var spans = _catalog.All(_f.Document);
        _editing.Choose(_f.Document, _f.History, spans[0].Id, "Uno");
        _editing.SetDate(_f.Document, _f.History, spans[1].Id, new DateTime(2026, 10, 13));
        spans = _catalog.All(_f.Document);
        Assert.Equal("Uno2026-10-13", First.Text);
        Assert.Equal("Uno", First.Text[spans[0].Start.Offset..spans[0].End.Offset]);
        Assert.Equal("2026-10-13", First.Text[spans[1].Start.Offset..spans[1].End.Offset]);
        Assert.Equal(new DateTime(2026, 10, 13), spans[1].Control.Date);
    }

    [Theory]
    [InlineData("it-IT", "en-US", "13 October 2026")]
    [InlineData("en-US", "it-IT", "13 ottobre 2026")]
    public void DateWithoutItsOwnLanguageFollowsTheDocumentLanguage(string system, string language, string expected)
    {
        _f.Document.Language = language;
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(system);
        try
        {
            Assert.Equal(expected, _editing.DateText(_f.Document, new ContentControl { Kind = ContentControlKind.Date, DateFormat = "d MMMM yyyy" }, new DateTime(2026, 10, 13)));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void IlBloccoProteggeContenutoEControllo()
    {
        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(First.Id, 0), "x testo y");
        _editing.Insert(_f.Document, _f.History, DocumentRange.In(First.Id, 2, 7), new ContentControl { ContentLocked = true });
        Assert.True(_guard.LocksTyping(_f.Document, new DocumentPosition(First.Id, 4)));
        Assert.False(_guard.LocksTyping(_f.Document, new DocumentPosition(First.Id, 0)));
        Assert.True(_guard.Protects(_f.Document, DocumentRange.In(First.Id, 3, 5)));
        Assert.False(_guard.Protects(_f.Document, DocumentRange.In(First.Id, 0, 9)));

        _editing.Change(_f.Document, _f.History, Only.Id, Only.Control with { ContentLocked = false, ControlLocked = true });
        Assert.False(_guard.Protects(_f.Document, DocumentRange.In(First.Id, 3, 5)));
        Assert.True(_guard.Protects(_f.Document, DocumentRange.In(First.Id, 0, 9)));
    }

    [Fact]
    public void CancellareTuttoIlControlloLoTogliePulireIlContenutoNo()
    {
        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(First.Id, 0), "a bc d");
        _editing.Insert(_f.Document, _f.History, DocumentRange.In(First.Id, 2, 4), new ContentControl());
        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(First.Id, 2, 4));
        Assert.True(Only.IsEmpty);

        _editing.Refill(_f.Document, _f.History, "Segnaposto");
        Assert.Equal("a Segnaposto d", First.Text);
        Assert.True(Only.Control.ShowingPlaceholder);

        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(First.Id, 1, 13));
        Assert.Empty(_catalog.All(_f.Document));
        Assert.DoesNotContain(First.Marks, mark => mark is ContentControlMark);
    }

    [Fact]
    public void ScriverePrimaDellaFineEntraNelControlloDividereLoAllunga()
    {
        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(First.Id, 0), "abcd");
        _editing.Insert(_f.Document, _f.History, DocumentRange.In(First.Id, 1, 3), new ContentControl { Kind = ContentControlKind.RichText });
        _f.Editor.InsertText(_f.Document, _f.History, new DocumentPosition(First.Id, 3), "X");
        Assert.Equal("bcX", Inside(Only));
        _f.Editor.SplitParagraph(_f.Document, _f.History, new DocumentPosition(First.Id, 2));
        Assert.False(Only.InOneParagraph);
        _editing.Remove(_f.Document, _f.History, Only.Id);
        Assert.Empty(_catalog.All(_f.Document));
    }

    [Fact]
    public void ISegmentiConLoStessoIdentificativoSiRicompongono()
    {
        var document = new TextDocument();
        document.Body.Add(new ParagraphBlock("a", "uno") { Marks = [new ContentControlMark(1, "c", MarkEdge.Start) { Control = new ContentControl { Title = "T" } }, new ContentControlMark(3, "c", MarkEdge.End)] });
        document.Body.Add(new ParagraphBlock("b", "due") { Marks = [new ContentControlMark(0, "c", MarkEdge.Start) { Control = new ContentControl { Title = "T" } }, new ContentControlMark(2, "c", MarkEdge.End)] });
        _catalog.Join(document);
        var span = Assert.Single(_catalog.All(document));
        Assert.Equal(new DocumentPosition("a", 1), span.Start);
        Assert.Equal(new DocumentPosition("b", 2), span.End);
    }

    [Fact]
    public void LOrdineMetteLeChiusurePrimaDelleAperture()
    {
        var order = new MarkOrder();
        var marks = new InlineMark[]
        {
            new ContentControlMark(0, "a", MarkEdge.Start),
            new ContentControlMark(2, "b", MarkEdge.Start),
            new ContentControlMark(2, "a", MarkEdge.End),
            new ContentControlMark(2, "b", MarkEdge.End),
        };
        var ordered = order.Ordered(marks).Cast<ContentControlMark>().Select(mark => mark.ControlId + mark.Edge).ToList();
        Assert.Equal(["aStart", "aEnd", "bStart", "bEnd"], ordered);
    }
}
