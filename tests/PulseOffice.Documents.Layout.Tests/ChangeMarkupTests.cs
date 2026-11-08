using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class ChangeMarkupTests
{
    private static readonly DateTimeOffset When = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private IReadOnlyList<GlyphRun> Runs(LayoutFixture fixture, string paragraphId) =>
        [.. fixture.Lines(fixture.Layout()).Where(line => line.ParagraphId == paragraphId).SelectMany(line => line.Line.Runs)];

    [Fact]
    public void LInserimentoSiSottolineaColColoreDellAutoreELaCancellazioneSiBarra()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("uno due"));
        f.Document.Changes.Add(new Insertion("ct1", "Anna", When));
        f.Document.Changes.Add(new Deletion("ct2", "Bruno", When, new BlockContainer([new ParagraphBlock("px", "tre ")])));
        block.State = block.State with
        {
            Marks = [new ChangeMark(4, "ct1", MarkEdge.Start), new ChangeMark(7, "ct1", MarkEdge.End), new ChangeMark(0, "ct2", MarkEdge.Point)],
        };

        var runs = Runs(f, block.Id);

        var deleted = runs.Where(run => run.Kind == GlyphRunKind.Citation).ToList();
        Assert.Equal("tre ", string.Concat(deleted.Select(run => run.Text)));
        Assert.All(deleted, run => Assert.True(run.Style!.Strikethrough));
        var inserted = runs.Single(run => run.Kind == GlyphRunKind.Text && run.Text == "due");
        Assert.True(inserted.Style!.Underline);
        Assert.NotEqual(deleted[0].Style!.Color, inserted.Style.Color);
        Assert.Equal("uno due", block.Text);
    }

    [Fact]
    public void ADeletionNestedInAnotherAuthorsInsertionIsStruckAndUnderlinedInTheDeletersColour()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("uno tre"));
        f.Document.Changes.Add(new Insertion("ct1", "Anna", When));
        f.Document.Changes.Add(new Deletion("ct2", "Bruno", When, new BlockContainer([new ParagraphBlock("px", "due ")])) { WithinInsertion = "ct1" });
        f.Document.Changes.Add(new Deletion("ct3", "Bruno", When, new BlockContainer([new ParagraphBlock("py", "zero ")])));
        block.State = block.State with
        {
            Marks = [new ChangeMark(0, "ct3", MarkEdge.Point), new ChangeMark(4, "ct1", MarkEdge.Start), new ChangeMark(4, "ct2", MarkEdge.Point), new ChangeMark(7, "ct1", MarkEdge.End)],
        };

        var runs = Runs(f, block.Id).Where(run => run.Kind == GlyphRunKind.Citation).ToList();

        var nested = runs.Single(run => run.Text == "due ");
        var plain = runs.Single(run => run.Text == "zero ");
        Assert.True(nested.Style!.Strikethrough);
        Assert.True(nested.Style.Underline);
        Assert.False(plain.Style!.Underline);
        Assert.Equal(plain.Style.Color, nested.Style.Color);
    }

    [Fact]
    public void NascondereLeRevisioniToglieLaCancellazioneDalDisegno()
    {
        var f = new LayoutFixture(width: 400, height: 400);
        var block = f.AddRuns(new TextRun("uno"));
        f.Document.Changes.Add(new Deletion("ct2", "Bruno", When, new BlockContainer([new ParagraphBlock("px", "tre")])));
        block.State = block.State with { Marks = [new ChangeMark(3, "ct2", MarkEdge.Point)] };
        f.Document.Changes.ShowChanges = false;

        Assert.DoesNotContain(Runs(f, block.Id), run => run.Kind == GlyphRunKind.Citation);
    }

    [Fact]
    public void UnaCancellazioneLungaVaACapoFraLeParole()
    {
        var f = new LayoutFixture(width: 60, height: 400);
        var block = f.AddRuns(new TextRun("a"));
        f.Document.Changes.Add(new Deletion("ct2", "Bruno", When, new BlockContainer([new ParagraphBlock("px", "uno due tre quattro cinque sei sette")])));
        block.State = block.State with { Marks = [new ChangeMark(1, "ct2", MarkEdge.Point)] };

        var lines = f.Lines(f.Layout()).Where(line => line.ParagraphId == block.Id).ToList();

        Assert.True(lines.Count > 1);
        Assert.All(lines, line => Assert.True(line.Line.Runs.Sum(run => run.Width) <= 60.5));
    }
}
