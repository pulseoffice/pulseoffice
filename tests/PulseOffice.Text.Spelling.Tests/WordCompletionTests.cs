using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class WordCompletionTests
{
    [Fact]
    public void RaccoglieSoloLeParoleLungheEProponeLaPiuFrequente()
    {
        var completion = new WordCompletion();
        completion.Collect("Il documento straordinario, strabiliante e strabiliante. Breve.");

        Assert.Equal(new[] { "documento", "strabiliante", "straordinario" }, completion.Words);
        Assert.Equal("strabiliante", completion.Suggest("str"));
        Assert.Equal("straordinario", completion.Suggest("STRAO"));
        Assert.Null(completion.Suggest("st"));
        Assert.Null(completion.Suggest("straordinario"));
    }

    [Fact]
    public void LeNuoveOpzioniDelProfiloSiConservano()
    {
        var codec = new AutoCorrectProfileCodec();
        var profile = AutoCorrectProfile.Default with { Options = AutoCorrectOptions.Default with { AutomaticLists = false, ApplyBorders = false, WordCompletion = false } };

        var back = codec.Read(codec.Write(profile)).Options;

        Assert.False(back.AutomaticLists);
        Assert.False(back.ApplyBorders);
        Assert.False(back.WordCompletion);
    }
}
