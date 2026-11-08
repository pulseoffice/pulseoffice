using System.Globalization;
using System.Text.RegularExpressions;

namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IGuideOperators
{
    bool Knows(string name);

    double Apply(string name, double x, double y, double z);
}

public sealed class GuideOperators : IGuideOperators
{
    private const double AngleUnit = Math.PI / 10800000;

    private readonly IReadOnlyDictionary<string, Func<double, double, double, double>> _operators =
        new Dictionary<string, Func<double, double, double, double>>(StringComparer.Ordinal)
        {
            ["val"] = (x, _, _) => x,
            ["*/"] = (x, y, z) => z == 0 ? 0 : x * y / z,
            ["+-"] = (x, y, z) => x + y - z,
            ["+/"] = (x, y, z) => z == 0 ? 0 : (x + y) / z,
            ["?:"] = (x, y, z) => x > 0 ? y : z,
            ["abs"] = (x, _, _) => Math.Abs(x),
            ["at2"] = (x, y, _) => Math.Atan2(y, x) / AngleUnit,
            ["cat2"] = (x, y, z) => x * Math.Cos(Math.Atan2(z, y)),
            ["sat2"] = (x, y, z) => x * Math.Sin(Math.Atan2(z, y)),
            ["cos"] = (x, y, _) => x * Math.Cos(y * AngleUnit),
            ["sin"] = (x, y, _) => x * Math.Sin(y * AngleUnit),
            ["tan"] = (x, y, _) => x * Math.Tan(y * AngleUnit),
            ["max"] = (x, y, _) => Math.Max(x, y),
            ["min"] = (x, y, _) => Math.Min(x, y),
            ["mod"] = (x, y, z) => Math.Sqrt(x * x + y * y + z * z),
            ["pin"] = (x, y, z) => y < x ? x : y > z ? z : y,
            ["sqrt"] = (x, _, _) => Math.Sqrt(Math.Max(0, x)),
        };

    public bool Knows(string name) => _operators.ContainsKey(name);

    public double Apply(string name, double x, double y, double z) =>
        _operators.TryGetValue(name, out var apply) ? apply(x, y, z) : 0;
}

public interface IDrawingBuiltIns
{
    bool TryValue(string name, double width, double height, out double value);
}

public sealed class DrawingBuiltIns : IDrawingBuiltIns
{
    private readonly Regex _divided = new("^(wd|hd|ssd)([0-9]+)$", RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<string, Func<double, double, double>> _fixed =
        new Dictionary<string, Func<double, double, double>>(StringComparer.Ordinal)
        {
            ["l"] = (_, _) => 0,
            ["t"] = (_, _) => 0,
            ["r"] = (w, _) => w,
            ["b"] = (_, h) => h,
            ["w"] = (w, _) => w,
            ["h"] = (_, h) => h,
            ["hc"] = (w, _) => w / 2,
            ["vc"] = (_, h) => h / 2,
            ["ss"] = Math.Min,
            ["ls"] = Math.Max,
            ["cd2"] = (_, _) => 10800000,
            ["cd4"] = (_, _) => 5400000,
            ["cd8"] = (_, _) => 2700000,
            ["3cd4"] = (_, _) => 16200000,
            ["3cd8"] = (_, _) => 8100000,
            ["5cd8"] = (_, _) => 13500000,
            ["7cd8"] = (_, _) => 18900000,
        };

    public bool TryValue(string name, double width, double height, out double value)
    {
        if (_fixed.TryGetValue(name, out var fixedValue))
        {
            value = fixedValue(width, height);
            return true;
        }
        var match = _divided.Match(name);
        if (match.Success)
        {
            var divisor = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var basis = match.Groups[1].Value switch { "wd" => width, "hd" => height, _ => Math.Min(width, height) };
            value = divisor == 0 ? 0 : basis / divisor;
            return true;
        }
        value = 0;
        return false;
    }
}

public sealed class DrawingGuideScope
{
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
    private readonly IDrawingBuiltIns _builtIns;
    private readonly IGuideOperators _operators;

    public DrawingGuideScope(double width, double height, IDrawingBuiltIns builtIns, IGuideOperators operators)
    {
        Width = width;
        Height = height;
        _builtIns = builtIns;
        _operators = operators;
    }

    public double Width { get; }

    public double Height { get; }

    public void Set(string name, double value) => _values[name] = value;

    public void Define(DrawingGuide guide)
    {
        var value = Evaluate(guide);
        _values[guide.Name] = double.IsFinite(value) ? value : 0;
    }

    public double Evaluate(DrawingGuide guide)
    {
        double Argument(int index) => index < guide.Arguments.Count ? Value(guide.Arguments[index]) : 0;
        return _operators.Apply(guide.Operator, Argument(0), Argument(1), Argument(2));
    }

    public double Value(string token)
    {
        if (_values.TryGetValue(token, out var value)) return value;
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
        return _builtIns.TryValue(token, Width, Height, out var builtIn) ? builtIn : 0;
    }
}
