using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class Round191NumberSystemsTests
{
    private readonly NumberSystems _systems = new();

    [Theory]
    [InlineData(NumberSystem.DecimalZero, 7, "07")]
    [InlineData(NumberSystem.DecimalZero, 12, "12")]
    [InlineData(NumberSystem.Ordinal, 1, "1st")]
    [InlineData(NumberSystem.Ordinal, 12, "12th")]
    [InlineData(NumberSystem.Ordinal, 23, "23rd")]
    [InlineData(NumberSystem.CardinalText, 21, "Twenty-one")]
    [InlineData(NumberSystem.OrdinalText, 3, "Third")]
    [InlineData(NumberSystem.OrdinalText, 20, "Twentieth")]
    [InlineData(NumberSystem.OrdinalText, 112, "One hundred twelfth")]
    [InlineData(NumberSystem.Hebrew, 15, "טו")]
    [InlineData(NumberSystem.Hebrew, 11, "יא")]
    [InlineData(NumberSystem.RussianLower, 2, "б")]
    [InlineData(NumberSystem.RussianUpper, 29, "АА")]
    [InlineData(NumberSystem.Katakana, 1, "ア")]
    [InlineData(NumberSystem.Hiragana, 2, "い")]
    [InlineData(NumberSystem.ThaiLetters, 1, "ก")]
    [InlineData(NumberSystem.ArabicAlpha, 3, "ت")]
    [InlineData(NumberSystem.ArabicAbjad, 3, "ج")]
    [InlineData(NumberSystem.IdeographDigital, 105, "一〇五")]
    [InlineData(NumberSystem.ChineseCounting, 11, "十一")]
    [InlineData(NumberSystem.ChineseCounting, 101, "一百零一")]
    [InlineData(NumberSystem.ChineseCounting, 1010, "一千零一十")]
    public void Each_system_renders_word_s_labels(NumberSystem system, int value, string expected) =>
        Assert.Equal(expected, _systems.Render(system, value));
}
