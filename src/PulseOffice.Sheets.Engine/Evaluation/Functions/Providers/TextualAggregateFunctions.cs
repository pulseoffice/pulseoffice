using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class TextualAggregateFunctions : IFunctionProvider
{
    public string Category => "Statistica";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        (string Name, Func<List<double>, CellValue> Reduce)[] aggregations =
        [
            ("AVERAGEA", values => values.Count == 0 ? F.DivideByZero() : F.Number(values.Average())),
            ("MAXA", values => CellValue.Number(values.Count == 0 ? 0 : values.Max())),
            ("MINA", values => CellValue.Number(values.Count == 0 ? 0 : values.Min())),
            ("STDEVA", values => values.Count < 2 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.StandardDeviation(values, population: false))),
            ("STDEVPA", values => values.Count < 1 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.StandardDeviation(values, population: true))),
            ("VARA", values => values.Count < 2 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.Variance(values, population: false))),
            ("VARPA", values => values.Count < 1 ? F.DivideByZero()
                : F.Number(SpreadsheetMath.Variance(values, population: true))),
        ];

        foreach (var (name, reduce) in aggregations)
            builder.Raw(name, 1, int.MaxValue, r =>
            {
                var values = new List<double>();
                foreach (var value in r.AllValues())
                {
                    if (value.IsError) return value;
                    if (value.IsEmpty) continue;

                    values.Add(value.Kind switch
                    {
                        ValueKind.Number => value.AsNumber(),
                        ValueKind.Boolean => value.AsBoolean() ? 1 : 0,
                        _ => 0,
                    });
                }
                return reduce(values);
            });

        return builder.Build();
    }
}
