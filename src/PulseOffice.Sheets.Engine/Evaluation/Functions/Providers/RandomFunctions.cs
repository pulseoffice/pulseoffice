using PulseOffice.Sheets.Values;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public interface IRandomSource
{
    double NextDouble();

    long NextInt64(long minimumInclusive, long maximumInclusive);
}

public sealed class SystemRandomSource : IRandomSource
{
    public double NextDouble() => Random.Shared.NextDouble();

    public long NextInt64(long minimumInclusive, long maximumInclusive) =>
        Random.Shared.NextInt64(minimumInclusive, maximumInclusive + 1);
}

public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private readonly Random _random = new(seed);
    private readonly Lock _gate = new();

    public double NextDouble()
    {
        lock (_gate) return _random.NextDouble();
    }

    public long NextInt64(long minimumInclusive, long maximumInclusive)
    {
        lock (_gate) return _random.NextInt64(minimumInclusive, maximumInclusive + 1);
    }
}

public sealed class RandomFunctions(IRandomSource random) : IFunctionProvider
{
    public string Category => "Matematica";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Constant("RAND", () => CellValue.Number(random.NextDouble()))

        .Numeric2("RANDBETWEEN", (low, high) => System.Math.Ceiling(low) > System.Math.Ceiling(high)
            ? F.OutOfDomain()
            : CellValue.Number(random.NextInt64((long)System.Math.Ceiling(low), (long)System.Math.Ceiling(high))))

        .Build();
}
