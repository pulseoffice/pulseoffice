using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round228CommentMentionTests
{
    private readonly ICommentMentions _mentions = new CommentMentions();

    [Fact]
    public void Known_people_after_an_at_sign_are_mentions()
    {
        var found = _mentions.Detect("Hi @ada lovelace and @Bob, not me@Bob", ["Ada Lovelace", "Bob", "Ada"]);

        Assert.Equal([new CommentMention(3, 13, "Ada Lovelace"), new CommentMention(21, 4, "Bob")], found);
    }

    [Fact]
    public void Stale_mentions_are_dropped()
    {
        Assert.Empty(_mentions.Kept("Hi Bob", [new CommentMention(3, 4, "Bob")]));
        Assert.Single(_mentions.Kept("Hi @Bob", [new CommentMention(3, 4, "Bob")]));
    }
}
