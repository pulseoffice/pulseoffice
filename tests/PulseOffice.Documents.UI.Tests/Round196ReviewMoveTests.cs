using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Review;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round196ReviewMoveTests
{
    [Fact]
    public Task The_review_panel_lists_the_two_sides_of_a_move_as_moved() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Uno tre");
        var paragraph = (ParagraphBlock)scene.Document.Body[0];
        var when = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
        scene.Document.Changes.Add(new Deletion("mf", "Anna", when, new BlockContainer([new ParagraphBlock("dp", "due ")])) { MoveId = "m1" });
        scene.Document.Changes.Add(new Insertion("mt", "Anna", when) { MoveId = "m1" });
        paragraph.State = paragraph.State with
        {
            Marks = [new ChangeMark(0, "mf", MarkEdge.Point), new ChangeMark(4, "mt", MarkEdge.Start), new ChangeMark(7, "mt", MarkEdge.End)],
        };
        var context = scene.Services.GetRequiredService<IActiveFolioContext>();
        context.Attach(new FolioTarget(scene.Editor, () => default!, _ => { }, () => 1, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask));
        var source = scene.Services.GetRequiredService<FolioReviewSource>();

        Assert.Equal([ReviewItemKind.MovedFrom, ReviewItemKind.MovedTo], source.Changes.Select(change => change.Kind).Order());
        var texts = scene.Services.GetRequiredService<ITextCatalog>();
        Assert.NotEqual(texts["review.kind.deletion"], texts["review.kind.movedfrom"]);
        Assert.NotEqual(texts["review.kind.insertion"], texts["review.kind.movedto"]);
    }, CancellationToken.None);
}
