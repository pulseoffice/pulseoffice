using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class NumberFormatDesignerTests
{
    private readonly NumberFormatDesigner _designer = new();

    [Theory]
    [InlineData(NumberCategory.Number, 2, true, NegativeNumberStyle.Minus, "#,##0.00")]
    [InlineData(NumberCategory.Number, 0, false, NegativeNumberStyle.Red, "0;[RED]-0")]
    [InlineData(NumberCategory.Number, 1, true, NegativeNumberStyle.Parentheses, "#,##0.0;(#,##0.0)")]
    [InlineData(NumberCategory.Percent, 1, false, NegativeNumberStyle.Minus, "0.0%")]
    [InlineData(NumberCategory.Scientific, 3, false, NegativeNumberStyle.Minus, "0.000E+00")]
    [InlineData(NumberCategory.General, 2, false, NegativeNumberStyle.Minus, "General")]
    [InlineData(NumberCategory.Text, 2, false, NegativeNumberStyle.Minus, "@")]
    public void ComponeIlCodice(NumberCategory category, int decimals, bool thousands, NegativeNumberStyle negative, string expected) =>
        Assert.Equal(expected, _designer.Compose(new NumberFormatChoice { Category = category, Decimals = decimals, Thousands = thousands, Negative = negative }));

    [Fact]
    public void LaValutaMetteIlSimboloAlSuoPosto()
    {
        Assert.Equal("#,##0.00 \"€\"", _designer.Compose(new NumberFormatChoice { Category = NumberCategory.Currency, Currency = new("€", false) }));
        Assert.Equal("\"$\"#,##0.00;[RED](\"$\"#,##0.00)",
            _designer.Compose(new NumberFormatChoice { Category = NumberCategory.Currency, Currency = new("$", true), Negative = NegativeNumberStyle.RedParentheses }));
    }

    [Theory]
    [InlineData(NumberCategory.Number, 3, true, NegativeNumberStyle.Red)]
    [InlineData(NumberCategory.Number, 0, false, NegativeNumberStyle.Parentheses)]
    [InlineData(NumberCategory.Currency, 2, true, NegativeNumberStyle.RedParentheses)]
    [InlineData(NumberCategory.Percent, 2, false, NegativeNumberStyle.Minus)]
    [InlineData(NumberCategory.Scientific, 1, false, NegativeNumberStyle.Minus)]
    public void RiconosceCiòCheCompone(NumberCategory category, int decimals, bool thousands, NegativeNumberStyle negative)
    {
        var choice = new NumberFormatChoice { Category = category, Decimals = decimals, Thousands = thousands, Negative = negative };
        var recognized = _designer.Recognize(_designer.Compose(choice));
        Assert.Equal(category, recognized.Category);
        Assert.Equal(decimals, recognized.Decimals);
        if (_designer.HasNegatives(category)) Assert.Equal(negative, recognized.Negative);
        if (_designer.HasThousands(category)) Assert.Equal(thousands, recognized.Thousands);
    }

    [Fact]
    public void DateOreFrazioniECodiciLiberi()
    {
        Assert.Equal(NumberCategory.Date, _designer.Recognize("d mmmm yyyy").Category);
        Assert.Equal(NumberCategory.Time, _designer.Recognize("h:mm:ss").Category);
        Assert.Equal(NumberCategory.Fraction, _designer.Recognize("# ?/4").Category);
        Assert.Equal(NumberCategory.Custom, _designer.Recognize("0.00 \"kg\";[RED]-0.00").Category);
        Assert.Contains("0.00%", _designer.Presets(NumberCategory.Custom, new NumberFormatCatalog()));
    }
}
