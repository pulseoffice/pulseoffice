using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round191IndexDeletionTests
{
    private readonly IServiceProvider _services = new ServiceCollection().AddDocumentsModel().BuildServiceProvider();

    [Fact]
    public void A_caption_in_a_row_pending_deletion_is_left_out()
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        document.Changes.Add(new Deletion("gone", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer()));
        TableRow Row(string id, string? change) => new([new TableCell(new BlockContainer([new ParagraphBlock(id, Caption(id))]))]) { ChangeId = change };
        document.Body.Replace([new TableBlock("t1", [Row("kept", null), Row("dropped", "gone")], [new Foundation.Units.Twip(1440)])]);

        var entries = Source(IndexKind.Tables).Collect(document, new IndexDefinition(IndexKind.Tables));

        Assert.Equal(["kept"], entries.SelectMany(entry => entry.ParagraphIds));
    }

    [Fact]
    public void A_heading_with_a_pending_deletion_lists_only_the_text_that_stays()
    {
        var document = _services.GetRequiredService<ITextDocumentFactory>().Create();
        document.Changes.Add(new Deletion("gone", "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("r1", new TextParagraph("Old "))])));
        document.Body.Replace([new ParagraphBlock("h1", new TextParagraph("Title") { OutlineLevel = 1 }) { Marks = [new ChangeMark(0, "gone", MarkEdge.Point)] }]);

        var entries = Source(IndexKind.Contents).Collect(document, new IndexDefinition(IndexKind.Contents));

        Assert.Equal("Title", Assert.Single(entries).Text);
    }

    private IIndexEntrySource Source(IndexKind kind) => _services.GetServices<IIndexEntrySource>().First(source => source.Kinds.Contains(kind));

    private static TextParagraph Caption(string text) => new TextParagraph(string.Empty).WithRuns(
    [
        new TextRun("Table "),
        new TextRun("1") { Field = TextFieldKind.Sequence, FieldArgument = "Table" },
        new TextRun(": " + text),
    ]);
}
