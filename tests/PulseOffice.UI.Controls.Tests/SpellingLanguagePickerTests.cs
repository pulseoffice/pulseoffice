using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Spelling;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class SpellingLanguagePickerTests
{
    [Fact]
    public async Task SenzaDizionariSiAvvisaSoloSeNonCeUnaVoceInPiu()
    {
        var notice = new Notice();
        var prompt = new Prompt(0);
        var picker = new SpellingLanguagePicker(new Dictionaries([]), notice, prompt, new DictionaryLanguageNames());

        Assert.Null(await picker.PickAsync(LanguageTag.Parse("it-IT"), "t", CancellationToken.None));
        Assert.Equal(1, notice.Shown);

        Assert.Equal("zxx", await picker.PickAsync(LanguageTag.Parse("it-IT"), "t", new DictionaryLanguage("zxx", "Nessuna"), CancellationToken.None));
        Assert.Equal(1, notice.Shown);
    }

    [Fact]
    public async Task LaLinguaCorrenteEPreselezionataELaVoceInPiuVaInCoda()
    {
        var prompt = new Prompt(2);
        var picker = new SpellingLanguagePicker(new Dictionaries([LanguageTag.Parse("it-IT"), LanguageTag.Parse("en-US")]), new Notice(), prompt, new DictionaryLanguageNames());

        var chosen = await picker.PickAsync(LanguageTag.Parse("it-IT"), "t", new DictionaryLanguage("zxx", "Nessuna"), CancellationToken.None);

        Assert.Equal("zxx", chosen);
        Assert.Equal(1, prompt.Current);
        Assert.Equal(["en-US", "it-IT", "zxx"], prompt.Offered.Select(choice => choice.Code));
    }

    private sealed class Dictionaries(IReadOnlyList<LanguageTag> available) : ISpellDictionaryProvider
    {
        public IReadOnlyList<LanguageTag> Available => available;

        public ISpellDictionary? For(LanguageTag language) => null;

        public void Refresh()
        {
        }
    }

    private sealed class Notice : IDictionaryNotice
    {
        public int Shown { get; private set; }

        public void ShowMissingDictionaries() => Shown++;
    }

    private sealed class Prompt(int answer) : ILanguagePrompt
    {
        public IReadOnlyList<DictionaryLanguage> Offered { get; private set; } = [];

        public int Current { get; private set; }

        public Task<int?> AskAsync(IReadOnlyList<DictionaryLanguage> choices, int current, string titleKey, CancellationToken ct)
        {
            Offered = choices;
            Current = current;
            return Task.FromResult<int?>(answer);
        }
    }
}
