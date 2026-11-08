using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Text.Spelling.Hunspell;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class UserDictionaryRound29Tests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulse-user-dic-").FullName;

    private string PathOf => Path.Combine(_folder, "user.dic");

    private UserDictionaryFile Open() => new(NullLogger<UserDictionaryFile>.Instance, PathOf);

    [Fact]
    public void AWordWithALineBreakDoesNotSplitIntoTwoWordsOnReload()
    {
        var dictionary = Open();
        dictionary.Add("uno" + (char)10 + "due");
        dictionary.Add("tre" + (char)0x2028 + "quattro");
        dictionary.Add("cinque");

        Assert.Equal(dictionary.Words.Order(StringComparer.Ordinal), Open().Words.Order(StringComparer.Ordinal));
        Assert.False(Open().Knows("due"));
    }

    [Fact]
    public void AnEnormousWordIsRefused()
    {
        var dictionary = Open();
        dictionary.Add(new string('a', 100_000));

        Assert.Empty(dictionary.Words);
    }

    [Fact(Timeout = 60_000)]
    public async Task ConcurrentAdditionsAreAllSaved()
    {
        var dictionary = Open();

        await Task.WhenAll(Enumerable.Range(0, 64).Select(index =>
            Task.Run(() => dictionary.Add($"parola{index}"), TestContext.Current.CancellationToken)));

        Assert.Equal(64, Open().Words.Count);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
