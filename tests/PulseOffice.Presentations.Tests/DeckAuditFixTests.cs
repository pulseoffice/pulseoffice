using PulseOffice.Document.Tables;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Spelling;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class DeckAuditFixTests
{
    private ObjectElement Shape(string id) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(1000), new Twip(1000)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };

    private TextElement Text(string id, params TextRun[] runs) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(1000), new Twip(1000)),
        Text = new RichText(PlaceholderRole.Body, [new TextParagraph(string.Empty).WithRuns(runs)]),
    };

    [Fact]
    public void DuplicandoLaDiapositivaIConnettoriPuntanoAlleCopie()
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.Add(Shape("a"));
        slide.Add(Shape("b"));
        slide.Add(new ObjectElement
        {
            Id = "c",
            Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(10), new Twip(10)),
            Content = new ConnectorObject { Id = "c", Start = new ConnectorEndpoint("a"), End = new ConnectorEndpoint("b") },
        });

        var clone = new SlideFactory().Clone(slide, "s2");

        var connector = (ConnectorObject)clone.Elements.OfType<ObjectElement>().Single(e => e.Content is ConnectorObject).Content;
        var ids = clone.Elements.Select(e => e.Id).ToHashSet();
        Assert.Contains(connector.Start.ObjectId!, ids);
        Assert.Contains(connector.End.ObjectId!, ids);
        Assert.StartsWith("s2.", connector.Start.ObjectId);
    }

    [Fact]
    public void LaMappaDegliScostamentiSegueLaCorrezioneAutomatica()
    {
        var map = new TextOffsetMap("teh casa rossa", "the casa rossa");
        Assert.Equal(4, map.Map(4));
        Assert.Equal(14, map.Map(14));

        var longer = new TextOffsetMap("a -- b", "a — b");
        Assert.Equal(4, longer.Map(5));
        Assert.Equal(5, longer.Map(6));
    }

    [Fact]
    public void LaRicercaPercorreTutteLeOccorrenzeAncheNelleNote()
    {
        var first = new Slide("s1", SlideLayout.Blank) { Notes = "gatto" };
        first.Add(Text("t", new TextRun("gatto e gatto")));
        var second = new Slide("s2", SlideLayout.Blank);
        second.Add(Text("u", new TextRun("gatto")));
        var search = new DeckTextSearch();

        var matches = search.Matches([first, second], "gatto", matchCase: false);
        Assert.Equal(4, matches.Count);
        Assert.Single(matches, match => match.InNotes);

        var visited = new List<DeckTextMatch>();
        DeckTextMatch? current = null;
        for (var i = 0; i < 4; i++)
        {
            current = search.Next(matches, current, 0);
            visited.Add(current!);
        }
        Assert.Equal(matches, visited);
        Assert.Equal(matches[0], search.Next(matches, current, 0));
    }

    [Fact]
    public void ISinonimiSostituisconoSoloLaSelezione()
    {
        var text = Text("t", new TextRun("casa "), new TextRun("casa") { Bold = true }).Text;

        var replaced = new RichTextFormatter().ReplaceWord(text, (5, 9), "dimora");

        Assert.Equal("casa dimora", replaced.PlainText);
        Assert.True(replaced.Paragraphs[0].Runs.Last(run => run.Text.Length > 0).Bold);
    }

    [Fact]
    public void LaSillabazioneTogliePrimaITrattiniEUnisceLePorzioniSaltandoICampi()
    {
        var paragraph = Text("t",
            new TextRun("ca­sa"),
            new TextRun("mento") { Bold = true },
            new TextRun("12") { Field = TextFieldKind.SlideNumber }).Text;

        var result = new SlideTextHyphenation(new WordTokenizer()).Hyphenate(paragraph, new MiddleHyphenator());

        var runs = result.Paragraphs[0].Runs;
        Assert.Equal("casa", runs[0].Text.Replace("­", string.Empty));
        Assert.Equal(1, runs.Sum(run => run.Text.Count(c => c == '­')));
        Assert.Equal("12", runs[2].Text);
        Assert.Equal("casamento", string.Concat(runs.Take(2).Select(r => r.Text)).Replace("­", string.Empty));
    }

    [Fact]
    public void LaSelezioneDellIntestazioneNonContaComeRigaDiDati()
    {
        Assert.Equal((0, 1), new TableCellRange(-1, 0, 1, 0).DataRows(3));
        Assert.Null(new TableCellRange(-1, 0, -1, 0).DataRows(3));
        Assert.Equal((0, 2), new TableCellRange(0, 0, 9, 0).DataRows(3));
    }

    private sealed class MiddleHyphenator : IHyphenator
    {
        public LanguageTag Language => LanguageTag.Parse("it-IT");

        public IReadOnlyList<int> Positions(string word, int minimumEdge = 2) =>
            word.Length >= 6 ? [word.Length / 2] : [];
    }
}
