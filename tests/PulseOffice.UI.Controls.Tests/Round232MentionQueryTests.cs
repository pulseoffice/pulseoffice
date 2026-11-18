using PulseOffice.UI.Controls.Review;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round232MentionQueryTests
{
    private readonly IMentionQuery _query = new MentionQuery();

    [Fact]
    public void An_at_sign_starts_a_token_and_lists_matching_people()
    {
        var token = _query.TokenAt("Thanks @ma", 10);
        Assert.Equal(new MentionToken(7, "ma"), token);
        Assert.Equal(["Mario Rossi", "Anna Marchi"], _query.Candidates(token!, ["Anna Marchi", "Mario Rossi", "Luca Bianchi", "Mario Rossi"]));
    }

    [Fact]
    public void An_at_sign_inside_a_word_is_not_a_mention()
    {
        Assert.Null(_query.TokenAt("mail@example", 12));
        Assert.Null(_query.TokenAt("no mention", 10));
    }

    [Fact]
    public void Choosing_a_person_replaces_the_token()
    {
        var token = _query.TokenAt("Hi @ma, see", 6)!;
        Assert.Equal(new MentionInsertion("Hi @Mario Rossi , see", 16), _query.Insert("Hi @ma, see", token, 6, "Mario Rossi"));
    }
}
