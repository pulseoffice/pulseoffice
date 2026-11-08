using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class CommentThreadRepairTests
{
    private static Comment Comment(string id, string? parent) => new(id, "Ada", DateTimeOffset.UnixEpoch, new BlockContainer([])) { ParentId = parent };

    [Fact]
    public void Missing_parents_self_parents_and_cycles_are_detached_and_good_replies_kept()
    {
        var comments = new CommentCollection();
        comments.Replace([Comment("root", null), Comment("reply", "root"), Comment("orphan", "gone"), Comment("self", "self"),
            Comment("a", "b"), Comment("b", "c"), Comment("c", "a"), Comment("tail", "a")]);

        var detached = comments.DetachBrokenReplies();

        Assert.Equal(3, detached);
        Assert.Equal("root", comments.Find("reply")!.ParentId);
        Assert.Null(comments.Find("orphan")!.ParentId);
        Assert.Null(comments.Find("self")!.ParentId);
        Assert.Null(comments.Find("a")!.ParentId);
        Assert.Equal("c", comments.Find("b")!.ParentId);
        Assert.Equal("a", comments.Find("tail")!.ParentId);
        Assert.Equal(["a", "b", "c", "tail"], comments.Thread("a").Select(comment => comment.Id).Order(StringComparer.Ordinal));
    }
}
