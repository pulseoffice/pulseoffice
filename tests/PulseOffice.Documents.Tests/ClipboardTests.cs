using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ClipboardTests
{
    private readonly Fixture _f = new();
    private readonly DocumentClipboard _clipboard;

    public ClipboardTests()
    {
        var scanner = new FragmentScanner(_f.Tree);
        var extractor = new FragmentExtractor(_f.Trails, _f.Text, _f.Tree, scanner, new StyleClosure(scanner));
        _clipboard = new DocumentClipboard(extractor, _f.Editor);
    }

    private IEnumerable<string> AllIds() =>
        _f.Stories.Stories(_f.Document).SelectMany(story => _f.Tree.Blocks(story)).Select(block => block.Id);

    [Fact]
    public void IncollareInMezzoAUnParagrafoConservaLaFormattazioneEFaUnSoloAnnullamento()
    {
        _f.Type("abcdef", "XYZ");
        _f.Editor.SetCharacter(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 1, 3), new CharacterProperties { Bold = true });
        var fragment = _clipboard.Copy(_f.Document, DocumentRange.In(_f.First.Id, 1, 4));
        var before = _f.Snapshot();

        _clipboard.Paste(_f.Document, _f.History, _f.At(1, 1), fragment);

        Assert.Equal("XbcdYZ", _f.Paragraph(1).Text);
        Assert.Contains(_f.Paragraph(1).Content.Runs, run => run is { Text: "bc", Bold: true });
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        _f.History.Redo();
        Assert.Equal("XbcdYZ", _f.Paragraph(1).Text);
    }

    [Fact]
    public void IncollarePiuParagrafiDivideIlBersaglioEDaIdentificativiNuovi()
    {
        _f.Type("Uno", "Due", "Tre", "XY");
        var fragment = _clipboard.Copy(_f.Document, new DocumentRange(_f.At(0, 1), _f.At(2, 2)));
        var before = _f.Snapshot();

        _clipboard.Paste(_f.Document, _f.History, _f.At(3, 1), fragment);

        Assert.Equal(["Uno", "Due", "Tre", "Xno", "Due", "TrY"], _f.Document.Body.Blocks.Cast<ParagraphBlock>().Select(p => p.Text));
        Assert.Equal(AllIds().Count(), AllIds().Distinct().Count());
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        _f.History.Redo();
        Assert.Equal(6, _f.Document.Body.Count);
    }

    [Fact]
    public void LaPosizioneDopoIlPuntoDIncollaggioFinisceInCoda()
    {
        _f.Type("Uno", "Due", "XY");
        var fragment = _clipboard.Copy(_f.Document, new DocumentRange(_f.At(0, 0), _f.At(1, 3)));
        using var tracker = new PositionTracker(_f.Document);
        var after = tracker.Track(_f.At(2, 2));

        _clipboard.Paste(_f.Document, _f.History, _f.At(2, 1), fragment);

        Assert.Equal(new DocumentPosition(_f.Paragraph(3).Id, 4), after.Position);
        Assert.Equal("DueY", _f.Paragraph(3).Text);
    }

    [Fact]
    public void TabelleNoteECommentiArrivanoConIdentificativiNuovi()
    {
        _f.Type("Testo", "Fine");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 0), 1, 2);
        _f.Editor.InsertNote(_f.Document, _f.History, _f.At(0, 2), NoteKind.Footnote, "nota");
        var comment = _f.Editor.AddComment(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 3), "Ada", DateTimeOffset.UnixEpoch, "commento");
        var root = _f.Document.Comments.All[0].Id;
        _f.Editor.AddComment(_f.Document, _f.History, default, "Bob", DateTimeOffset.UnixEpoch, "risposta", root);
        var fragment = _clipboard.Copy(_f.Document, new DocumentRange(_f.At(0, 0), _f.At(2, 0)));
        Assert.Single(fragment.Notes);
        Assert.Equal(2, fragment.Comments.Count);
        var before = _f.Snapshot();

        _clipboard.Paste(_f.Document, _f.History, _f.At(2, 4), fragment);

        Assert.Equal(2, _f.Document.Notes.All.Count);
        Assert.Equal(4, _f.Document.Comments.All.Count);
        Assert.Equal(2, _f.Document.Body.Blocks.OfType<TableBlock>().Count());
        Assert.Equal(AllIds().Count(), AllIds().Distinct().Count());
        var pastedRoot = _f.Document.Comments.All[2];
        Assert.Equal(pastedRoot.Id, _f.Document.Comments.All[3].ParentId);
        var marks = _f.Stories.Paragraphs(_f.Document).SelectMany(p => p.Marks).ToList();
        Assert.Contains(marks, mark => mark is NoteMark note && note.NoteId == _f.Document.Notes.All[1].Id);
        Assert.Contains(marks, mark => mark is CommentMark c && c.CommentId == pastedRoot.Id);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        _ = comment;
    }

    [Fact]
    public void GliStiliMancantiSiCopianoQuelliEsistentiVincono()
    {
        var source = new Fixture();
        source.Type("Citazione");
        var parent = new ParagraphStyle("Mio genitore") { Paragraph = new ParagraphProperties { SpaceAfter = 6 } };
        var child = new ParagraphStyle("Mio stile") { ParentName = "Mio genitore", Paragraph = new ParagraphProperties { Align = TextAlign.Center } };
        source.Document.Styles.Paragraph.Add(parent);
        source.Document.Styles.Paragraph.Add(child);
        source.Document.Styles.Character.Add(new CharacterStyle("Rosso") { Character = new CharacterProperties { Color = 0xFFFF0000 } });
        source.Document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Paragraph = new ParagraphProperties { SpaceAfter = 99 };
        source.Editor.ApplyParagraphStyle(source.Document, source.History, DocumentRange.At(source.At(0, 0)), "Mio stile");
        source.Editor.ApplyCharacterStyle(source.Document, source.History, DocumentRange.In(source.First.Id, 0, 3), "Rosso");
        source.Editor.InsertText(source.Document, source.History, source.At(0, 9), "\nStandard");
        source.Editor.ApplyParagraphStyle(source.Document, source.History, DocumentRange.At(source.At(1, 0)), DefaultStyleNames.Standard);
        var scanner = new FragmentScanner(source.Tree);
        var fragment = new FragmentExtractor(source.Trails, source.Text, source.Tree, scanner, new StyleClosure(scanner))
            .Copy(source.Document, new DocumentRange(source.At(0, 0), source.At(1, 8)));
        _f.Type("A");
        var before = _f.Snapshot();
        var standard = _f.Document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Paragraph;

        _clipboard.Paste(_f.Document, _f.History, _f.At(0, 1), fragment);

        Assert.NotNull(_f.Document.Styles.Paragraph.Find("Mio stile"));
        Assert.NotNull(_f.Document.Styles.Paragraph.Find("Mio genitore"));
        Assert.NotNull(_f.Document.Styles.Character.Find("Rosso"));
        Assert.Equal(standard, _f.Document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Paragraph);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        Assert.Null(_f.Document.Styles.Paragraph.Find("Mio stile"));
    }

    [Fact]
    public void ISegnalibriOmonimiSiRinominanoESegnaliMonchiNonPassano()
    {
        _f.Type("Uno", "Due");
        _f.Editor.AddBookmark(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 3), "segno");
        _f.Editor.AddBookmark(_f.Document, _f.History, new DocumentRange(_f.At(0, 2), _f.At(1, 2)), "lungo");
        var fragment = _clipboard.Copy(_f.Document, DocumentRange.In(_f.First.Id, 0, 3));

        _clipboard.Paste(_f.Document, _f.History, _f.At(1, 3), fragment);

        var marks = _f.Paragraph(1).Marks.OfType<BookmarkMark>().ToList();
        Assert.Equal(2, marks.Count(mark => mark.Name == "segno1"));
        Assert.DoesNotContain(marks, mark => mark.Name.StartsWith("lungo", StringComparison.Ordinal) && mark.Offset >= 3);
    }

    [Fact]
    public void GliOggettiAncoratiSeguonoIlParagrafoCopiato()
    {
        _f.Type("Uno", "Due");
        var item = new AnchoredObject("o1", new CharacterAnchor(_f.First.Id), new EmbeddedContent(new PulseOffice.Document.Embedding.PackagedObject { Id = "img", FileName = "a.png", MediaType = "image/png", Data = new byte[] { 1 } }),
            new DocumentSize(Twip.FromCentimeters(1), Twip.FromCentimeters(1))) { Name = "Forma" };
        _f.Editor.InsertObject(_f.Document, _f.History, item, _f.At(0, 1));
        var fragment = _clipboard.Copy(_f.Document, DocumentRange.In(_f.First.Id, 0, 3));

        _clipboard.Paste(_f.Document, _f.History, _f.At(1, 3), fragment);

        Assert.Equal(2, _f.Document.Objects.All.Count);
        var pasted = _f.Document.Objects.All[1];
        Assert.NotEqual("o1", pasted.Id);
        Assert.Equal(new CharacterAnchor(_f.Paragraph(1).Id), pasted.Anchor);
        Assert.Equal("Forma1", pasted.Name);
        Assert.Contains(_f.Paragraph(1).Marks, mark => mark is ObjectMark o && o.ObjectId == pasted.Id);
    }

    [Fact]
    public void TagliareEIncollareSonoDueAnnullamenti()
    {
        _f.Type("abcdef");
        var before = _f.Snapshot();

        var fragment = _clipboard.Cut(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 2));
        _clipboard.Paste(_f.Document, _f.History, _f.At(0, 4), fragment);

        Assert.Equal("cdefab", _f.First.Text);
        _f.History.Undo();
        Assert.Equal("cdef", _f.First.Text);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
    }

    [Fact]
    public void UnaCopiaDaUnaCellaAlCorpoPortaLaTabellaIntera()
    {
        _f.Type("Uno", "Due");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 0), 2, 2);
        var table = (TableBlock)_f.Document.Body[1];
        var cell = (ParagraphBlock)table.Cell(0, 1).Content[0];

        var fragment = _clipboard.Copy(_f.Document, new DocumentRange(new DocumentPosition(cell.Id, 0), _f.At(2, 2)));

        Assert.IsType<TableBlock>(fragment.Blocks[0]);
        Assert.Equal("Du", ((ParagraphBlock)fragment.Blocks[1]).Text);
    }
}
