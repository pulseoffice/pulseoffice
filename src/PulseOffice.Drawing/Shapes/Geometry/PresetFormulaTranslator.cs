using System.Globalization;
using System.Text;

namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IPresetFormulaTranslator
{
    string TypePrefix { get; }

    FormulaGeometry? Translate(PresetGeometry geometry, double width, double height);

    PresetGeometry? Preset(string? type, IReadOnlyList<double> modifiers, double width, double height);

    bool Adjusted(PresetGeometry geometry);
}

public sealed class PresetFormulaTranslator(IPresetShapeCatalog catalog, IPresetGeometryEngine engine, IDrawingBuiltIns builtIns) : IPresetFormulaTranslator
{
    private readonly IReadOnlyDictionary<string, Func<string[], string>> _operators =
        new Dictionary<string, Func<string[], string>>(StringComparer.Ordinal)
        {
            ["val"] = a => a[0],
            ["*/"] = a => $"{a[0]}*{a[1]}/{a[2]}",
            ["+-"] = a => $"{a[0]}+{a[1]}-{a[2]}",
            ["+/"] = a => $"({a[0]}+{a[1]})/{a[2]}",
            ["?:"] = a => $"if({a[0]},{a[1]},{a[2]})",
            ["abs"] = a => $"abs({a[0]})",
            ["at2"] = a => $"atan2({a[1]},{a[0]})*10800000/pi",
            ["cat2"] = a => $"{a[0]}*cos(atan2({a[2]},{a[1]}))",
            ["sat2"] = a => $"{a[0]}*sin(atan2({a[2]},{a[1]}))",
            ["cos"] = a => $"{a[0]}*cos({a[1]}*pi/10800000)",
            ["sin"] = a => $"{a[0]}*sin({a[1]}*pi/10800000)",
            ["tan"] = a => $"{a[0]}*tan({a[1]}*pi/10800000)",
            ["max"] = a => $"max({a[0]},{a[1]})",
            ["min"] = a => $"min({a[0]},{a[1]})",
            ["mod"] = a => $"sqrt({a[0]}*{a[0]}+{a[1]}*{a[1]}+{a[2]}*{a[2]})",
            ["pin"] = a => $"if({a[0]}-{a[1]},{a[0]},if({a[1]}-{a[2]},{a[2]},{a[1]}))",
            ["sqrt"] = a => $"sqrt({a[0]})",
        };

    private readonly IReadOnlyDictionary<ShapeShade, char> _shades = new Dictionary<ShapeShade, char>
    {
        [ShapeShade.Darken] = 'H',
        [ShapeShade.DarkenLess] = 'I',
        [ShapeShade.Lighten] = 'J',
        [ShapeShade.LightenLess] = 'K',
    };

    private readonly IReadOnlyDictionary<string, string> _sizes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["l"] = "0",
        ["t"] = "0",
        ["r"] = "logwidth",
        ["b"] = "logheight",
        ["w"] = "logwidth",
        ["h"] = "logheight",
        ["hc"] = "logwidth/2",
        ["vc"] = "logheight/2",
        ["ss"] = "min(logwidth,logheight)",
        ["ls"] = "max(logwidth,logheight)",
    };

    public PresetFormulaTranslator() : this(new PresetShapeCatalog())
    {
    }

    public PresetFormulaTranslator(IPresetShapeCatalog catalog)
        : this(catalog, new PresetGeometryEngine(catalog, new DrawingBuiltIns(), new GuideOperators()), new DrawingBuiltIns())
    {
    }

    public string TypePrefix => "ooxml-";

    public PresetGeometry? Preset(string? type, IReadOnlyList<double> modifiers, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        if (type is null || !type.StartsWith(TypePrefix, StringComparison.Ordinal)) return null;
        var name = type[TypePrefix.Length..];
        if (!engine.Knows(name)) return null;
        var defaults = engine.Defaults(name);
        return new PresetGeometry(name)
        {
            Adjustments = [.. defaults.Select((adjustment, index) => index < modifiers.Count ? adjustment with { Value = modifiers[index] } : adjustment)],
            ReferenceWidth = width > 0 ? width : 1,
            ReferenceHeight = height > 0 ? height : 1,
        };
    }

    public bool Adjusted(PresetGeometry geometry) => engine.Adjusted(geometry);

    public FormulaGeometry? Translate(PresetGeometry geometry, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (catalog.Find(geometry.Name) is not { } definition) return null;
        var w = Math.Max(1, Math.Round(width));
        var h = Math.Max(1, Math.Round(height));
        var session = new Session(_operators, _sizes, builtIns);
        var modifiers = new List<double>();
        for (var index = 0; index < definition.Adjustments.Count; index++)
        {
            var adjustment = definition.Adjustments[index];
            var given = geometry.Adjustments.FirstOrDefault(value => value.Name == adjustment.Name)
                ?? (definition.Adjustments.Count == 1 ? geometry.Adjustments.FirstOrDefault() : null);
            modifiers.Add(given?.Value ?? new DrawingGuideScope(1, 1, builtIns, new GuideOperators()).Evaluate(adjustment));
            session.Bind(adjustment.Name, "$" + index.ToString(CultureInfo.InvariantCulture));
        }
        foreach (var guide in definition.Guides) session.Define(guide);
        var path = new StringBuilder();
        foreach (var figure in definition.Paths)
        {
            if (!figure.Filled) path.Append("F ");
            if (!figure.Stroked) path.Append("S ");
            if (_shades.TryGetValue(figure.Shade, out var shade)) path.Append(shade).Append(' ');
            var writer = new PathWriter(session, path, figure.Width is > 0 ? figure.Width : null, figure.Height is > 0 ? figure.Height : null);
            foreach (var command in figure.Commands) command.Accept(writer);
            path.Append("N ");
        }
        return new FormulaGeometry(path.ToString().TrimEnd())
        {
            Type = TypePrefix + geometry.Name,
            ViewWidth = w,
            ViewHeight = h,
            Modifiers = modifiers,
            Equations = session.Equations,
            Handles = [.. definition.Handles.Select(handle => Handle(session, handle))],
            ReferenceWidth = w,
            ReferenceHeight = h,
        };
    }

    private EnhancedHandle Handle(Session session, PresetHandle handle)
    {
        var attributes = new List<KeyValuePair<string, string>>
        {
            new("handle-position", session.Reference(handle.Position.X) + " " + session.Reference(handle.Position.Y)),
        };
        void Range(PresetHandleAxis? axis, string minimum, string maximum)
        {
            if (axis is null) return;
            attributes.Add(new(minimum, session.Reference(axis.Minimum)));
            attributes.Add(new(maximum, session.Reference(axis.Maximum)));
        }
        if (handle.Polar) Range(handle.First, "handle-radius-range-minimum", "handle-radius-range-maximum");
        else
        {
            Range(handle.First, "handle-range-x-minimum", "handle-range-x-maximum");
            Range(handle.Second, "handle-range-y-minimum", "handle-range-y-maximum");
        }
        return new EnhancedHandle(attributes);
    }

    private sealed class Session(
        IReadOnlyDictionary<string, Func<string[], string>> operators, IReadOnlyDictionary<string, string> sizes, IDrawingBuiltIns builtIns)
    {
        private readonly Dictionary<string, string> _references = new(StringComparer.Ordinal);
        private readonly List<EnhancedEquation> _equations = [];

        public IReadOnlyList<EnhancedEquation> Equations => _equations;

        public void Bind(string name, string reference) => _references[name] = reference;

        public void Define(DrawingGuide guide)
        {
            var arguments = guide.Arguments.Select(Operand).ToList();
            while (arguments.Count < 3) arguments.Add("0");
            var formula = operators.TryGetValue(guide.Operator, out var translate) ? translate([.. arguments]) : "0";
            _references[guide.Name] = Add(formula);
        }

        public string Reference(string token)
        {
            if (_references.TryGetValue(token, out var known)) return known;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return token;
            if (sizes.TryGetValue(token, out var size)) return _references[token] = Add(size);
            if (token.Contains("cd", StringComparison.Ordinal) && builtIns.TryValue(token, 1, 1, out var angle)) return Number(angle);
            var divided = token.StartsWith("ssd", StringComparison.Ordinal) ? ("min(logwidth,logheight)", token[3..])
                : token.StartsWith("wd", StringComparison.Ordinal) ? ("logwidth", token[2..])
                : token.StartsWith("hd", StringComparison.Ordinal) ? ("logheight", token[2..])
                : (null, null);
            return divided.Item1 is null ? "0" : _references[token] = Add($"{divided.Item1}/{divided.Item2}");
        }

        public string Scaled(string token, string extent, double? pathExtent)
        {
            var reference = Reference(token);
            if (pathExtent is not { } basis) return reference;
            return Add($"{Operand(token)}*{extent}/{Number(basis)}");
        }

        public string Degrees(string token) =>
            double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var literal)
                ? Number(literal / 60000)
                : Add($"{Operand(token)}/60000");

        public (string Start, string Sweep) StretchedAngles(string start, string sweep, string scaleX, string scaleY)
        {
            var first = Operand(start);
            var turn = Operand(sweep);
            string Visual(string angle) => Add($"atan2({scaleY}*sin({angle}*pi/10800000),{scaleX}*cos({angle}*pi/10800000))*180/pi");
            var from = Visual(first);
            var to = Visual($"({first}+{turn})");
            var difference = Add($"{to}-{from}");
            var swept = Add($"if({turn},if(-{difference},{difference}+360,if({difference},{difference},360)),if(-{turn},if({difference},{difference}-360,if(-{difference},{difference},-360)),0))");
            return (from, swept);
        }

        public string Number(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);

        private string Operand(string token)
        {
            var reference = Reference(token);
            return reference.StartsWith('-') ? $"({reference})" : reference;
        }

        private string Add(string formula)
        {
            var name = "f" + _equations.Count.ToString(CultureInfo.InvariantCulture);
            _equations.Add(new EnhancedEquation(name, formula));
            return "?" + name;
        }
    }

    private sealed class PathWriter(Session session, StringBuilder path, double? pathWidth, double? pathHeight) : IPresetPathVisitor
    {
        private string X(string token) => session.Scaled(token, "logwidth", pathWidth);

        private string Y(string token) => session.Scaled(token, "logheight", pathHeight);

        private void Write(char command, params string[] values)
        {
            path.Append(command).Append(' ');
            foreach (var value in values) path.Append(value).Append(' ');
        }

        public void VisitMove(PresetMove command) => Write('M', X(command.To.X), Y(command.To.Y));

        public void VisitLine(PresetLine command) => Write('L', X(command.To.X), Y(command.To.Y));

        public void VisitArc(PresetArc command)
        {
            if (pathWidth is null && pathHeight is null)
            {
                Write('G', X(command.RadiusX), Y(command.RadiusY), session.Degrees(command.Start), session.Degrees(command.Sweep));
                return;
            }
            var (start, sweep) = session.StretchedAngles(command.Start, command.Sweep,
                pathWidth is { } width ? $"logwidth/{session.Number(width)}" : "1", pathHeight is { } height ? $"logheight/{session.Number(height)}" : "1");
            Write('G', X(command.RadiusX), Y(command.RadiusY), start, sweep);
        }

        public void VisitQuadratic(PresetQuadratic command) =>
            Write('Q', X(command.Control.X), Y(command.Control.Y), X(command.To.X), Y(command.To.Y));

        public void VisitCubic(PresetCubic command) =>
            Write('C', X(command.First.X), Y(command.First.Y), X(command.Second.X), Y(command.Second.Y), X(command.To.X), Y(command.To.Y));

        public void VisitClose(PresetClose command) => Write('Z');
    }
}
