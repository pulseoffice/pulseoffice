using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ThreadAndIndexLetterTests
{
    private readonly Fixture _f = new();

    private Comment Comment(string id, string? parent) =>
        new(id, "a", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock(id + "p", "x")])) { ParentId = parent };

    [Fact]
    public void UnaDiscussioneConRisposteACicloNonRicorreSenzaFine()
    {
        var comments = new CommentCollection();
        comments.Add(Comment("c1", "c2"));
        comments.Add(Comment("c2", "c1"));
        comments.Add(Comment("c3", "c3"));

        Assert.Equal(["c1", "c2"], comments.Thread("c1").Select(comment => comment.Id));
        Assert.Equal(["c3"], comments.Thread("c3").Select(comment => comment.Id));
    }

    [Fact]
    public void UnaCatenaDiRisposteMoltoLungaNonEsaurisceLoStack()
    {
        var comments = new CommentCollection();
        comments.Add(Comment("r0", null));
        for (var i = 1; i < 100_000; i++) comments.Add(Comment("r" + i, "r" + (i - 1)));

        Assert.Equal(100_000, comments.Thread("r0").Count());
    }

    [Fact]
    public void IlSeparatoreDiLetteraDiUnaVoceFuoriDalPianoBaseNonSpezzaLaCoppiaSurrogata()
    {
        _f.Type("uno", "due");
        _f.Paragraph(0).Marks = [new IndexEntryMark(0, "𝔸lfa", MarkEdge.Point)];
        _f.Paragraph(1).Marks = [new IndexEntryMark(0, "𝔹eta", MarkEdge.Point)];
        var source = new AlphabeticalEntrySource(new IndexScope(new DocumentReadingOrder(_f.Tree, _f.Stories), _f.Tree));

        var entries = source.Collect(_f.Document, new IndexDefinition(IndexKind.Alphabetical) { LetterSeparators = true });

        Assert.Equal(["𝔸", "𝔸lfa", "𝔹", "𝔹eta"], entries.Select(entry => entry.Text));
    }

    [Theory]
    [InlineData("it-IT", "sv-SE", new[] { "zebra", "äpple" })]
    [InlineData("sv-SE", "de-DE", new[] { "äpple", "zebra" })]
    public void TheAlphabeticalIndexSortsByTheDocumentLanguageNotTheSystem(string system, string language, string[] expected)
    {
        _f.Type("uno", "due");
        _f.Document.Language = language;
        _f.Paragraph(0).Marks = [new IndexEntryMark(0, "äpple", MarkEdge.Point)];
        _f.Paragraph(1).Marks = [new IndexEntryMark(0, "zebra", MarkEdge.Point)];
        var source = new AlphabeticalEntrySource(new IndexScope(new DocumentReadingOrder(_f.Tree, _f.Stories), _f.Tree));

        var saved = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(system);
        try
        {
            Assert.Equal(expected, source.Collect(_f.Document, new IndexDefinition(IndexKind.Alphabetical)).Select(entry => entry.Text));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("it-IT", "tr-TR", "İ")]
    [InlineData("tr-TR", "en-US", "I")]
    public void TheLetterSeparatorFollowsTheDocumentLanguage(string system, string language, string expected)
    {
        _f.Type("uno");
        _f.Document.Language = language;
        _f.Paragraph(0).Marks = [new IndexEntryMark(0, "isola", MarkEdge.Point)];
        var source = new AlphabeticalEntrySource(new IndexScope(new DocumentReadingOrder(_f.Tree, _f.Stories), _f.Tree));

        var saved = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(system);
        try
        {
            Assert.Equal(expected, source.Collect(_f.Document, new IndexDefinition(IndexKind.Alphabetical) { LetterSeparators = true })[0].Text);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }
}
