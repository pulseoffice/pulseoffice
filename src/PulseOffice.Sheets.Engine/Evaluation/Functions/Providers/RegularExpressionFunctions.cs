using System.Text.RegularExpressions;
using PulseOffice.Sheets.Values;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class RegularExpressionFunctions(IGuardedRegex patterns) : IFunctionProvider
{
    private const int FirstMatch = 0;
    private const int AllMatches = 1;
    private const int CaptureGroups = 2;

    public string Category => "Testo";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Raw("REGEXTEST", 2, 3, r =>
        {
            var text = r.Text(0);
            var pattern = r.Text(1);
            var regex = Pattern(r, pattern, 2);
            if (r.HasError) return r.Error;
            if (regex is null) return F.Invalid();

            return patterns.Matches(regex, text) is { } found ? CellValue.Boolean(found.Count > 0) : F.Invalid();
        })

        .ArrayResult("REGEXEXTRACT", 2, 4, r =>
        {
            var text = r.Text(0);
            var pattern = r.Text(1);
            var mode = r.Integer(2, FirstMatch);
            var regex = Pattern(r, pattern, 3);
            if (r.HasError) return EvalValue.Scalar(r.Error);
            if (regex is null || mode is not (FirstMatch or AllMatches or CaptureGroups))
                return EvalValue.Error(CellError.Value);
            if (patterns.Matches(regex, text) is not { } found) return EvalValue.Error(CellError.Value);
            if (found.Count == 0) return EvalValue.Error(CellError.NotAvailable);

            return mode switch
            {
                AllMatches => new ArrayValue([.. found.Select(match => (IReadOnlyList<CellValue>)[CellValue.Text(match.Value)])]),
                CaptureGroups => Groups(found[0]),
                _ => EvalValue.Scalar(CellValue.Text(found[0].Value)),
            };
        })

        .Raw("REGEXREPLACE", 3, 5, r =>
        {
            var text = r.Text(0);
            var pattern = r.Text(1);
            var replacement = r.Text(2);
            var occurrence = r.Integer(3, 0);
            var regex = Pattern(r, pattern, 4);
            if (r.HasError) return r.Error;
            if (regex is null) return F.Invalid();

            return patterns.Replace(regex, text, replacement, (index, count) => Chosen(occurrence, index, count)) is { } replaced
                ? CellValue.Text(replaced)
                : F.Invalid();
        })
        .Build();

    private Regex? Pattern(ArgumentReader r, string pattern, int caseIndex)
    {
        var sensitivity = r.Has(caseIndex) ? r.Number(caseIndex) : 0;
        if (r.HasError) return null;
        if (sensitivity is not (0 or 1))
        {
            r.Fail(CellError.Value);
            return null;
        }
        return patterns.Compile(pattern, ignoreCase: sensitivity == 1);
    }

    private static bool Chosen(int occurrence, int index, int count) =>
        occurrence == 0 || (occurrence > 0 ? index == occurrence - 1 : index == count + occurrence);

    private static EvalValue Groups(Match match)
    {
        if (match.Groups.Count < 2) return EvalValue.Error(CellError.NotAvailable);
        return new ArrayValue([[.. match.Groups.Cast<Group>().Skip(1).Select(group => CellValue.Text(group.Success ? group.Value : string.Empty))]]);
    }
}
