using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class IntegrationFunctions : IFunctionProvider
{
    private const int GoalSeekIterations = 120;
    private const int MaximumUrlLength = 2048;

    private static readonly System.Xml.XmlReaderSettings SafeXml = new()
    {
        DtdProcessing = System.Xml.DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    public string Category => "Integrazione";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .RawHandlingErrors("COLOR", 3, 4, r =>
        {
            var red = Channel(r.Number(0));
            var green = Channel(r.Number(1));
            var blue = Channel(r.Number(2));
            var alpha = r.Has(3) ? Channel(r.Number(3)) : 0;

            if (r.HasError) return r.Error;

            return CellValue.Number((uint)((alpha << 24) | (red << 16) | (green << 8) | blue));
        })

        .RawHandlingErrors("STYLE", 1, 3, r =>
        {
            var name = r.Text(0);
            if (r.HasError) return r.Error;

            r.Context.RequestStyle(name);
            return CellValue.Number(0);
        })

        .RawHandlingErrors("WEBSERVICE", 1, 1, r =>
        {
            var url = r.Text(0);
            if (r.HasError) return r.Error;
            if (url.Length > MaximumUrlLength) return CellValue.FromError(CellError.Value);

            var content = r.Context.FetchResource(url);

            return content is null || !ResultLimits.TextFits(content.Length)
                ? CellValue.FromError(CellError.Value)
                : CellValue.Text(content);
        })

        .ArrayResult("FILTERXML", 2, 2, r =>
        {
            var document = r.Text(0);
            var path = r.Text(1);

            if (r.HasError) return r.Error.AsEval();

            return SelectXml(document, path);
        }, handlesErrors: true)

        .ArrayResult("MULTIRANGE", 1, 32, r =>
        {
            var values = new List<CellValue>();

            for (var index = 0; index < r.Count; index++)
            {
                if (!r.Has(index)) continue;

                switch (r.Raw(index))
                {
                    case RangeValue range:
                        values.AddRange(r.Context.GetRangeValues(range.SheetName, range.Range));
                        break;

                    case ArrayValue array:
                        values.AddRange(array.Rows.SelectMany(row => row));
                        break;

                    case ScalarValue scalar:
                        values.Add(scalar.Value);
                        break;
                }
            }

            return values.Count == 0
                ? EvalValue.Scalar(CellValue.FromError(CellError.Reference))
                : new ArrayValue([values]);
        })

        .RawHandlingErrors("GETPIVOTDATA", 2, 32, r =>
        {
            if (r.Count == 2 && r.Raw(0) is RangeValue table && r.Raw(1) is not RangeValue)
            {
                var constraint = r.Text(1);
                if (r.HasError) return r.Error;
                var pairs = PivotConstraint().Matches(constraint)
                    .Select(match => (match.Groups["field"].Value, match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["item"].Value.Trim()))
                    .ToList();
                var dataField = PivotConstraint().Replace(constraint, " ").Trim();
                return r.Context.LookupPivot(table.SheetName, table.Range.Start, dataField, pairs);
            }

            var field = r.Text(0);
            if (r.HasError) return r.Error;

            if (r.Raw(1) is not RangeValue target) return CellValue.FromError(CellError.Reference);

            var filters = new List<(string Field, string Item)>();

            for (var index = 2; index + 1 < r.Count; index += 2)
            {
                var name = r.Text(index);
                var item = r.Text(index + 1);

                if (r.HasError) return r.Error;

                filters.Add((name, item));
            }

            return r.Context.LookupPivot(target.SheetName, target.Range.Start, field, filters);
        })

        .RawHandlingErrors("MULTIPLE.OPERATIONS", 3, 5, r =>
        {
            if (Cell(r, 0) is not { } formula) return CellValue.FromError(CellError.Reference);
            if (Cell(r, 1) is not { } rowInput) return CellValue.FromError(CellError.Reference);

            var inputs = new List<(SheetCell Cell, CellValue Value)>
            {
                (rowInput, r.Context.GetCellValue(null, Address(r, 2))),
            };

            if (r.Has(3) && r.Has(4))
            {
                if (Cell(r, 3) is not { } columnInput) return CellValue.FromError(CellError.Reference);

                inputs.Add((columnInput, r.Context.GetCellValue(null, Address(r, 4))));
            }

            return r.Context.EvaluateWith(formula, inputs);
        })

        .RawHandlingErrors("GOALSEEK", 3, 3, r =>
        {
            if (Cell(r, 0) is not { } formula) return CellValue.FromError(CellError.Reference);
            if (Cell(r, 1) is not { } variable) return CellValue.FromError(CellError.Reference);

            var target = r.Number(2);
            if (r.HasError) return r.Error;

            return Solve(r, formula, variable, target);
        })

        .Build();

    private static CellValue Solve(
        ArgumentReader r, SheetCell formula, SheetCell variable, double target)
    {
        double At(double x) =>
            r.Context.EvaluateWith(formula, [(variable, CellValue.Number(x))]).AsNumber() - target;

        var start = r.Context.GetCellValue(null, variable.Address).AsNumber();

        var first = start;
        var second = Math.Abs(start) < 1e-9 ? 1 : start * 1.1;

        var firstValue = At(first);
        var secondValue = At(second);

        for (var iteration = 0; iteration < GoalSeekIterations; iteration++)
        {
            if (Math.Abs(secondValue) < 1e-10) return CellValue.Number(second);

            var denominator = secondValue - firstValue;
            if (Math.Abs(denominator) < 1e-15) break;

            var next = second - secondValue * (second - first) / denominator;

            first = second;
            firstValue = secondValue;
            second = next;
            secondValue = At(second);
        }

        return Math.Abs(secondValue) < 1e-6
            ? CellValue.Number(second)
            : CellValue.FromError(CellError.NotAvailable);
    }

    private static SheetCell? Cell(ArgumentReader r, int index)
    {
        if (r.Raw(index) is not RangeValue reference) return null;

        var sheet = reference.SheetName is null
            ? r.Context.CurrentCell.SheetIndex
            : r.Context.SheetIndex(reference.SheetName) - 1;

        return sheet < 0 ? null : new SheetCell(sheet, reference.Range.Start);
    }

    private static CellAddress Address(ArgumentReader r, int index) =>
        r.Raw(index) is RangeValue reference
            ? reference.Range.Start
            : r.Context.CurrentCell.Address;

    private static int Channel(double value) =>
        (int)Math.Clamp(Math.Round(value), 0, 255);

    private static EvalValue SelectXml(string document, string path)
    {
        XDocument parsed;

        try
        {
            using var reader = System.Xml.XmlReader.Create(new StringReader(document), SafeXml);
            parsed = XDocument.Load(reader);
        }
        catch (System.Xml.XmlException)
        {
            return EvalValue.Error(CellError.Value);
        }

        object result;

        try
        {
            result = parsed.XPathEvaluate(path);
        }
        catch (XPathException)
        {
            return EvalValue.Error(CellError.Value);
        }

        return result switch
        {
            string text => EvalValue.Scalar(CellValue.Text(text)),
            double number when double.IsFinite(number) => EvalValue.Scalar(CellValue.Number(number)),
            bool flag => EvalValue.Scalar(CellValue.Boolean(flag)),
            IEnumerable<object> nodes => Column([.. nodes.Select(Node)]),
            _ => EvalValue.Error(CellError.Value),
        };
    }

    private static EvalValue Column(List<CellValue> values) => values.Count switch
    {
        0 => EvalValue.Error(CellError.Value),
        1 => EvalValue.Scalar(values[0]),
        _ => new ArrayValue([.. values.Select(value => (IReadOnlyList<CellValue>)[value])]),
    };

    private static CellValue Node(object node)
    {
        var text = node switch
        {
            XElement element => element.Value,
            XAttribute attribute => attribute.Value,
            XText content => content.Value,
            _ => node.ToString() ?? string.Empty,
        };

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? CellValue.Number(number)
            : CellValue.Text(text);
    }

    private static readonly System.Text.RegularExpressions.Regex PivotConstraintPattern = new(
        @"(?<field>[^\[\]\s]+)\s*\[\s*(?:'(?<quoted>(?:[^']|'')*)'|(?<item>[^\]]*))\s*\]",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static System.Text.RegularExpressions.Regex PivotConstraint() => PivotConstraintPattern;
}
