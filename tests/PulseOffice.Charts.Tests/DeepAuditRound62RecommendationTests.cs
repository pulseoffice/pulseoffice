using System.Globalization;
using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class DeepAuditRound62RecommendationTests
{
    private static ChartData DecimalCommaCategories() =>
        new(["1,5", "2,5", "3,5"], [new ChartSeries("A", [3, 8, 5])]);

    [Fact]
    public void ItalianSheetOnEnglishSystemReadsDecimalCommaCategoriesAsNumbers()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var result = new ChartRecommender().Recommend(DecimalCommaCategories(), CultureInfo.GetCultureInfo("it-IT"));

            Assert.Contains(ChartType.Scatter, result);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void EnglishSheetOnItalianSystemDoesNotReadDecimalCommaCategoriesAsNumbers()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var result = new ChartRecommender().Recommend(DecimalCommaCategories(), CultureInfo.GetCultureInfo("en-US"));

            Assert.DoesNotContain(ChartType.Scatter, result);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
