using System.Globalization;

namespace PulseOffice.Charts;

public sealed record ChartDataShape(int SeriesCount, int CategoryCount, bool NumericCategories, bool AllPositive)
{
    public static ChartDataShape Of(ChartData data, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(culture);

        var values = data.Series.SelectMany(series => series.Values).Where(double.IsFinite).ToList();
        var categories = data.Categories.Where(category => !string.IsNullOrWhiteSpace(category)).ToList();

        return new ChartDataShape(
            data.Series.Count,
            Math.Max(data.Categories.Count, data.Series.Select(series => series.Values.Count).DefaultIfEmpty(0).Max()),
            !data.OrdinalCategories && categories.Count > 1 && categories.All(category => IsNumber(category, culture)),
            values.Count > 0 && values.All(value => value >= 0) && values.Any(value => value > 0));
    }

    private static bool IsNumber(string text, CultureInfo culture) =>
        double.TryParse(text, NumberStyles.Float, culture, out _)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}

public interface IChartRecommendationRule
{
    IEnumerable<ChartType> Suggest(ChartDataShape shape);
}

public sealed class CorrelationRule : IChartRecommendationRule
{
    public IEnumerable<ChartType> Suggest(ChartDataShape shape) =>
        shape.NumericCategories && shape.SeriesCount >= 1
            ? [ChartType.Scatter, ChartType.ScatterWithLines]
            : [];
}

public sealed class PartsOfWholeRule : IChartRecommendationRule
{
    private const int MaximumSlices = 8;

    public IEnumerable<ChartType> Suggest(ChartDataShape shape) =>
        shape.SeriesCount == 1 && shape.AllPositive && !shape.NumericCategories && shape.CategoryCount is > 1 and <= MaximumSlices
            ? [ChartType.Pie, ChartType.Doughnut]
            : [];
}

public sealed class TrendRule : IChartRecommendationRule
{
    private const int LongSeries = 12;

    public IEnumerable<ChartType> Suggest(ChartDataShape shape) =>
        shape.CategoryCount > LongSeries ? [ChartType.Line, ChartType.Area] : [];
}

public sealed class ComparisonRule : IChartRecommendationRule
{
    public IEnumerable<ChartType> Suggest(ChartDataShape shape) =>
        shape.SeriesCount > 1
            ? [ChartType.Column, ChartType.StackedColumn, ChartType.Line, ChartType.Bar]
            : [ChartType.Column, ChartType.Line, ChartType.Bar];
}

public interface IChartRecommender
{
    IReadOnlyList<ChartType> Recommend(ChartData data, CultureInfo culture);
}

public sealed class ChartRecommender : IChartRecommender
{
    public const int MaximumSuggestions = 6;

    private readonly IReadOnlyList<IChartRecommendationRule> _rules;

    public ChartRecommender(IEnumerable<IChartRecommendationRule> rules)
    {
        _rules = [.. rules];
        if (_rules.Count == 0) throw new ArgumentException("At least one recommendation rule is required.", nameof(rules));
    }

    public ChartRecommender() : this([new CorrelationRule(), new PartsOfWholeRule(), new TrendRule(), new ComparisonRule()])
    {
    }

    public IReadOnlyList<ChartType> Recommend(ChartData data, CultureInfo culture)
    {
        var shape = ChartDataShape.Of(data, culture);
        if (shape.SeriesCount == 0) return [ChartType.Column, ChartType.Line, ChartType.Pie];

        return [.. _rules.SelectMany(rule => rule.Suggest(shape)).Distinct().Take(MaximumSuggestions)];
    }
}
