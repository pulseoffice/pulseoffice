using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class FieldRangeEditingTests
{
    private readonly Fixture _f = new();
    private readonly DocumentClipboard _clipboard;

    public FieldRangeEditingTests()
    {
        var scanner = new FragmentScanner(_f.Tree);
        var extractor = new FragmentExtractor(_f.Trails, _f.Text, _f.Tree, scanner, new StyleClosure(scanner));
        _clipboard = new DocumentClipboard(extractor, _f.Editor);
    }

    private void Field(ParagraphBlock paragraph, int begin, int separate, int end)
    {
        var id = _f.Document.NextId("field");
        paragraph.Marks =
        [
            new FieldCodeMark(begin, id, FieldCodePart.Begin, " DATE "),
            new FieldCodeMark(separate, id, FieldCodePart.Separate, string.Empty),
            new FieldCodeMark(end, id, FieldCodePart.End, string.Empty),
        ];
    }

    private IEnumerable<FieldCodeMark> FieldMarks() =>
        _f.Stories.Paragraphs(_f.Document).SelectMany(paragraph => paragraph.Marks).OfType<FieldCodeMark>();

    [Fact]
    public void PastingACopiedFieldTwiceGivesEachCopyItsOwnIdentifier()
    {
        _f.Type("ab2026cd");
        Field(_f.First, 2, 2, 6);
        var fragment = _clipboard.Copy(_f.Document, DocumentRange.In(_f.First.Id, 1, 7));

        _clipboard.Paste(_f.Document, _f.History, _f.At(0, 8), fragment);
        _clipboard.Paste(_f.Document, _f.History, _f.At(0, 14), fragment);

        var ids = FieldMarks().Where(mark => mark.Part == FieldCodePart.Begin).Select(mark => mark.FieldId).ToList();
        Assert.Equal(3, ids.Count);
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public void CopyingPartOfAFieldResultCarriesNoLoosePartsOfIt()
    {
        _f.Type("ab2026cd");
        Field(_f.First, 2, 2, 6);

        var fragment = _clipboard.Copy(_f.Document, DocumentRange.In(_f.First.Id, 1, 4));

        Assert.DoesNotContain(fragment.Blocks.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks), mark => mark is FieldCodeMark);
    }

    [Fact]
    public void DeletingAWholeFieldRemovesItsMarksAndUndoBringsThemBack()
    {
        _f.Type("ab2026cd");
        Field(_f.First, 2, 2, 6);
        var before = _f.Snapshot();

        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 1, 7));

        Assert.Equal("ad", _f.First.Text);
        Assert.Empty(FieldMarks());
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        Assert.Equal(3, FieldMarks().Count());
    }

    [Fact]
    public void DeletingOnlyTheResultKeepsTheField()
    {
        _f.Type("ab2026cd");
        Field(_f.First, 2, 2, 6);

        _f.Editor.Delete(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 2, 6));

        Assert.Equal(3, FieldMarks().Count());
    }

    [Fact]
    public void DeletingTheParagraphThatHoldsTheStartDropsTheRestOfTheField()
    {
        _f.Type("uno", "ab2026", "cd", "fine");
        _f.Paragraph(1).Marks = [new FieldCodeMark(2, "field1", FieldCodePart.Begin, " DATE "), new FieldCodeMark(2, "field1", FieldCodePart.Separate, string.Empty)];
        _f.Paragraph(2).Marks = [new FieldCodeMark(1, "field1", FieldCodePart.End, string.Empty)];

        _f.Editor.Delete(_f.Document, _f.History, new DocumentRange(_f.At(0, 1), _f.At(2, 0)));

        Assert.Empty(FieldMarks());
        _f.History.Undo();
        Assert.Equal(3, FieldMarks().Count());
    }
}
