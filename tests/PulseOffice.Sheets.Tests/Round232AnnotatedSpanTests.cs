using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round232AnnotatedSpanTests
{
    private readonly IAnnotatedSpanFollowing _following = new AnnotatedSpanFollowing();

    [Fact]
    public void Text_typed_before_the_range_shifts_it()
    {
        Assert.Equal(new CommentTextSpan(10, 5), _following.Follow("the quick fox", "well, the quick fox", new CommentTextSpan(4, 5)));
    }

    [Fact]
    public void Text_typed_after_the_range_leaves_it()
    {
        Assert.Equal(new CommentTextSpan(4, 5), _following.Follow("the quick fox", "the quick fox jumps", new CommentTextSpan(4, 5)));
    }

    [Fact]
    public void Text_typed_inside_the_range_grows_it()
    {
        Assert.Equal(new CommentTextSpan(4, 13), _following.Follow("the quick fox", "the quick red fox", new CommentTextSpan(4, 9)));
    }

    [Fact]
    public void Deleting_the_whole_range_drops_it()
    {
        Assert.Null(_following.Follow("the quick fox", "the fox", new CommentTextSpan(4, 6)));
    }
}
