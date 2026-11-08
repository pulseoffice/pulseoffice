using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckChoiceNamesTests
{
    [Theory]
    [InlineData(UiLanguage.Italian)]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.Spanish)]
    [InlineData(UiLanguage.French)]
    public void I_layout_e_i_colori_dello_schema_hanno_nomi_tradotti(UiLanguage language)
    {
        var texts = new TextCatalog(language);
        var names = new DeckChoiceNames(texts);

        Assert.All(names.Layouts(), choice => Assert.True(texts.Contains($"deck.layout.{char.ToLowerInvariant(choice.Value.ToString()[0])}{choice.Value.ToString()[1..]}"), choice.Value.ToString()));
        Assert.All(names.ColorRoles(), choice => Assert.True(texts.Contains($"deck.colorRole.{char.ToLowerInvariant(choice.Value.ToString()[0])}{choice.Value.ToString()[1..]}"), choice.Value.ToString()));
    }
}
