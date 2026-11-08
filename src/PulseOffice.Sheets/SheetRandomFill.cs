using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public enum RandomDistribution
{
    Uniform,
    UniformInteger,
    Normal,
    Bernoulli,
    Binomial,
}

public sealed record RandomFillRequest(CellRange Range, RandomDistribution Distribution)
{
    public double Minimum { get; init; }

    public double Maximum { get; init; } = 1;

    public double Mean { get; init; }

    public double StandardDeviation { get; init; } = 1;

    public double Probability { get; init; } = 0.5;

    public int Trials { get; init; } = 10;

    public int? Seed { get; init; }
}

public interface ISheetRandomFill
{
    IReadOnlyList<(CellAddress Address, CellValue Value)> Plan(RandomFillRequest request);

    IReadOnlyList<(CellAddress Address, CellValue Value)> Shuffle(
        Worksheet sheet, CellRange range, int? seed = null);
}

public sealed class SheetRandomFill(ISelectionWorkArea workArea) : ISheetRandomFill
{
    public IReadOnlyList<(CellAddress Address, CellValue Value)> Plan(RandomFillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var random = request.Seed is { } seed ? new Random(seed) : new Random();
        var values = new List<(CellAddress, CellValue)>();

        foreach (var address in request.Range.Cells())
            values.Add((address, CellValue.Number(Next(random, request))));

        return values;
    }

    public IReadOnlyList<(CellAddress Address, CellValue Value)> Shuffle(
        Worksheet sheet, CellRange range, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var random = seed is { } value ? new Random(value) : new Random();

        if (workArea.Bound(range, sheet.UsedRange()) is not { } bounded) return [];
        var addresses = bounded.Cells().ToList();
        var contents = addresses.Select(address => sheet[address].Value).ToList();

        for (var i = contents.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (contents[i], contents[j]) = (contents[j], contents[i]);
        }

        return [.. addresses.Select((address, index) => (address, contents[index]))];
    }

    private static double Next(Random random, RandomFillRequest request) => request.Distribution switch
    {
        RandomDistribution.UniformInteger =>
            UniformInteger(random, request.Minimum, request.Maximum),

        RandomDistribution.Normal => Normal(random, request.Mean, request.StandardDeviation),

        RandomDistribution.Bernoulli => random.NextDouble() < request.Probability ? 1 : 0,

        RandomDistribution.Binomial => Binomial(random, request.Trials, request.Probability),

        _ => request.Minimum + (random.NextDouble() * (request.Maximum - request.Minimum)),
    };

    private static double UniformInteger(Random random, double minimum, double maximum)
    {
        var low = Math.Round(Math.Min(minimum, maximum));
        var high = Math.Round(Math.Max(minimum, maximum));
        if (!double.IsFinite(low) || !double.IsFinite(high)) return double.NaN;
        return low + Math.Floor(random.NextDouble() * (high - low + 1));
    }

    private static double Normal(Random random, double mean, double deviation)
    {
        var first = 1.0 - random.NextDouble();
        var second = 1.0 - random.NextDouble();

        return mean + (deviation * Math.Sqrt(-2.0 * Math.Log(first)) * Math.Sin(2.0 * Math.PI * second));
    }

    private static double Binomial(Random random, int trials, double probability)
    {
        var successes = 0;

        for (var i = 0; i < Math.Max(0, trials); i++)
            if (random.NextDouble() < probability) successes++;

        return successes;
    }
}
