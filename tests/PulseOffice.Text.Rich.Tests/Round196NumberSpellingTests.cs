using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class Round196NumberSpellingTests
{
    private readonly NumberSystems _systems = new();

    [Theory]
    [InlineData("it-IT", 21, "Ventuno")]
    [InlineData("it", 23, "Ventitré")]
    [InlineData("it", 188, "Centottantotto")]
    [InlineData("it", 2003, "Duemilatré")]
    [InlineData("de-DE", 21, "Einundzwanzig")]
    [InlineData("de", 1001, "Eintausendeins")]
    [InlineData("es-ES", 100, "Cien")]
    [InlineData("es", 31, "Treinta y uno")]
    [InlineData("es", 21000, "Veintiún mil")]
    [InlineData("fr-FR", 71, "Soixante et onze")]
    [InlineData("fr", 80, "Quatre-vingts")]
    [InlineData("fr", 200, "Deux cents")]
    [InlineData("en-GB", 21, "Twenty-one")]
    [InlineData("pt-BR", 2, "Two")]
    [InlineData(null, 3, "Three")]
    public void Cardinal_text_follows_the_language(string? language, int value, string expected) =>
        Assert.Equal(expected, _systems.Render(NumberSystem.CardinalText, value, language));

    [Theory]
    [InlineData("it", 1, "Primo")]
    [InlineData("it", 11, "Undicesimo")]
    [InlineData("it", 23, "Ventitreesimo")]
    [InlineData("it", 26, "Ventiseiesimo")]
    [InlineData("it", 2000, "Duemillesimo")]
    [InlineData("de", 1, "Erste")]
    [InlineData("de", 7, "Siebte")]
    [InlineData("de", 20, "Zwanzigste")]
    [InlineData("de", 103, "Einhundertdritte")]
    [InlineData("es", 3, "Tercero")]
    [InlineData("es", 23, "Vigésimo tercero")]
    [InlineData("es", 12, "Duodécimo")]
    [InlineData("fr", 1, "Premier")]
    [InlineData("fr", 5, "Cinquième")]
    [InlineData("fr", 21, "Vingt et unième")]
    [InlineData("fr", 80, "Quatre-vingtième")]
    [InlineData("en", 2, "Second")]
    public void Ordinal_text_follows_the_language(string language, int value, string expected) =>
        Assert.Equal(expected, _systems.Render(NumberSystem.OrdinalText, value, language));

    [Theory]
    [InlineData("it", 2, "2º")]
    [InlineData("de", 2, "2.")]
    [InlineData("fr", 1, "1er")]
    [InlineData("fr", 2, "2e")]
    [InlineData("en", 2, "2nd")]
    public void Numeric_ordinals_follow_the_language(string language, int value, string expected) =>
        Assert.Equal(expected, _systems.Render(NumberSystem.Ordinal, value, language));
}
