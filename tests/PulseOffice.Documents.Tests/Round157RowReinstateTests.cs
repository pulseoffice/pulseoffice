using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Revisions;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round157RowReinstateTests
{
    private readonly ServiceProvider _services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();

    private (TextDocument Document, TableRow Row) Table(TrackedChange change)
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        var row = new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("c2", "due")]))]) { ChangeId = change.Id };
        var table = new TableBlock("t", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("c1", "uno")]))]), row], [new Twip(2000)]);
        document.Body.Replace([table, new ParagraphBlock("p", "fine")]);
        document.Changes.Add(change);
        return (document, row);
    }

    [Fact]
    public void Reinstating_an_inserted_row_keeps_it_as_a_tracked_deletion_and_undo_restores_the_insertion()
    {
        var (document, row) = Table(new Insertion("r1", "Anna", DateTimeOffset.UnixEpoch));
        var history = new UndoStack();

        _services.GetRequiredService<IRevisionResolver>().Resolve(document, history, ["r1"], RevisionVerdict.Reinstate);

        var table = Assert.IsType<TableBlock>(document.Body[0]);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("r1", row.ChangeId);
        Assert.IsType<Deletion>(document.Changes.Find("r1"));

        history.Undo();

        Assert.IsType<Insertion>(document.Changes.Find("r1"));
        Assert.Equal("Anna", document.Changes.Find("r1")!.Author);
    }

    [Fact]
    public void Reinstating_a_deleted_row_keeps_it_as_a_tracked_insertion()
    {
        var (document, row) = Table(new Deletion("r1", "Anna", DateTimeOffset.UnixEpoch, new BlockContainer()));

        _services.GetRequiredService<IRevisionResolver>().Resolve(document, new UndoStack(), ["r1"], RevisionVerdict.Reinstate);

        Assert.Equal(2, Assert.IsType<TableBlock>(document.Body[0]).Rows.Count);
        Assert.Equal("r1", row.ChangeId);
        Assert.IsType<Insertion>(document.Changes.Find("r1"));
    }
}
