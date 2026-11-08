using System.Text;

namespace PulseOffice.Drawing.Shapes.Geometry;

public sealed class EnhancedPathState(GeometryPathBuilder builder, EnhancedScope scope)
{
    public GeometryPathBuilder Builder { get; } = builder;

    public EnhancedScope Scope { get; } = scope;

    public bool QuadrantHorizontal { get; set; } = true;
}

public interface IEnhancedPathCommand
{
    int Arity { get; }

    void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated);
}

public interface IEnhancedPathInterpreter
{
    ShapeOutline Outline(FormulaGeometry geometry, double logicalWidth, double logicalHeight);

    bool Evaluates(FormulaGeometry geometry);
}

public sealed class EnhancedPathInterpreter(IEnhancedFormulaParser parser, IReadOnlyDictionary<char, IEnhancedPathCommand> commands) : IEnhancedPathInterpreter
{
    public EnhancedPathInterpreter() : this(new EnhancedFormulaParser(), new EnhancedPathCommands().All)
    {
    }

    public bool Evaluates(FormulaGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        try
        {
            return Outline(geometry, geometry.ViewWidth, geometry.ViewHeight).Figures.Count > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public ShapeOutline Outline(FormulaGeometry geometry, double logicalWidth, double logicalHeight)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var equations = new Dictionary<string, IEnhancedExpression>(StringComparer.Ordinal);
        foreach (var equation in geometry.Equations) equations[equation.Name] = parser.Parse(equation.Formula);
        var scope = new EnhancedScope(equations, geometry.Modifiers, geometry.ViewLeft, geometry.ViewTop,
            geometry.ViewWidth, geometry.ViewHeight, logicalWidth, logicalHeight);
        var builder = new GeometryPathBuilder();
        var state = new EnhancedPathState(builder, scope);
        IEnhancedPathCommand? command = null;
        var pending = new List<double>();
        var repeated = false;
        foreach (var token in Tokens(geometry.Path))
        {
            if (token.Length == 1 && commands.TryGetValue(token[0], out var next))
            {
                if (command is { Arity: 0 }) command.Apply(state, [], false);
                command = next;
                pending.Clear();
                repeated = false;
                continue;
            }
            if (command is null) continue;
            pending.Add(parser.Parse(token).Evaluate(scope));
            if (pending.Count < command.Arity) continue;
            command.Apply(state, pending, repeated);
            pending.Clear();
            repeated = true;
        }
        if (command is { Arity: 0 }) command.Apply(state, [], false);
        return builder.Build(geometry.ViewLeft, geometry.ViewTop, geometry.ViewWidth, geometry.ViewHeight);
    }

    private IEnumerable<string> Tokens(string path)
    {
        var current = new StringBuilder();
        foreach (var character in path)
        {
            if (char.IsWhiteSpace(character) || character == ',')
            {
                if (current.Length > 0) yield return current.ToString();
                current.Clear();
                continue;
            }
            var startsCommand = char.IsLetter(character) && current.Length > 0 && !char.IsLetter(current[^1]) && current[0] is not ('?' or '$');
            var startsNumber = (char.IsDigit(character) || character is '-' or '+' or '.') && current.Length > 0 && char.IsLetter(current[0]) && current.Length == 1;
            if (startsCommand || startsNumber || (character is '?' or '$' && current.Length > 0))
            {
                yield return current.ToString();
                current.Clear();
            }
            current.Append(character);
        }
        if (current.Length > 0) yield return current.ToString();
    }
}

public sealed class EnhancedPathCommands
{
    public IReadOnlyDictionary<char, IEnhancedPathCommand> All { get; } = new Dictionary<char, IEnhancedPathCommand>
    {
        ['M'] = new MoveCommand(),
        ['L'] = new LineCommand(),
        ['C'] = new CubicCommand(),
        ['Q'] = new QuadraticCommand(),
        ['Z'] = new CloseCommand(),
        ['N'] = new EndCommand(),
        ['F'] = new NoFillCommand(),
        ['S'] = new NoStrokeCommand(),
        ['T'] = new AngleEllipseCommand(connect: true),
        ['U'] = new AngleEllipseCommand(connect: false),
        ['A'] = new ArcCommand(connect: true, clockwise: false),
        ['B'] = new ArcCommand(connect: false, clockwise: false),
        ['W'] = new ArcCommand(connect: true, clockwise: true),
        ['V'] = new ArcCommand(connect: false, clockwise: true),
        ['X'] = new QuadrantCommand(horizontalFirst: true),
        ['Y'] = new QuadrantCommand(horizontalFirst: false),
        ['G'] = new ArcAngleCommand(),
        ['H'] = new ShadeCommand(ShapeShade.Darken),
        ['I'] = new ShadeCommand(ShapeShade.DarkenLess),
        ['J'] = new ShadeCommand(ShapeShade.Lighten),
        ['K'] = new ShadeCommand(ShapeShade.LightenLess),
    };

    private sealed class MoveCommand : IEnhancedPathCommand
    {
        public int Arity => 2;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated)
        {
            if (repeated) state.Builder.Line(values[0], values[1]);
            else state.Builder.Move(values[0], values[1]);
        }
    }

    private sealed class LineCommand : IEnhancedPathCommand
    {
        public int Arity => 2;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) => state.Builder.Line(values[0], values[1]);
    }

    private sealed class CubicCommand : IEnhancedPathCommand
    {
        public int Arity => 6;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) =>
            state.Builder.Cubic(values[0], values[1], values[2], values[3], values[4], values[5]);
    }

    private sealed class QuadraticCommand : IEnhancedPathCommand
    {
        public int Arity => 4;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) =>
            state.Builder.Quadratic(values[0], values[1], values[2], values[3]);
    }

    private sealed class CloseCommand : IEnhancedPathCommand
    {
        public int Arity => 0;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) => state.Builder.Close();
    }

    private sealed class EndCommand : IEnhancedPathCommand
    {
        public int Arity => 0;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated)
        {
            state.Builder.End();
            state.Builder.Style(filled: true, stroked: true);
        }
    }

    private sealed class NoFillCommand : IEnhancedPathCommand
    {
        public int Arity => 0;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) => state.Builder.Fill(false);
    }

    private sealed class NoStrokeCommand : IEnhancedPathCommand
    {
        public int Arity => 0;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) => state.Builder.Stroke(false);
    }

    private sealed class ShadeCommand(ShapeShade shade) : IEnhancedPathCommand
    {
        public int Arity => 0;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) => state.Builder.Shade(shade);
    }

    private sealed class AngleEllipseCommand(bool connect) : IEnhancedPathCommand
    {
        private const double Degree = Math.PI / 180;

        public int Arity => 6;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated)
        {
            var sweep = values[5] - values[4];
            if (sweep <= 0) sweep += 360;
            state.Builder.Ellipse(values[0], values[1], values[2], values[3], -values[4] * Degree, -sweep * Degree, connect || repeated);
        }
    }

    private sealed class ArcCommand(bool connect, bool clockwise) : IEnhancedPathCommand
    {
        private const double FullTurn = 2 * Math.PI;

        public int Arity => 8;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated)
        {
            var centerX = (values[0] + values[2]) / 2;
            var centerY = (values[1] + values[3]) / 2;
            var radiusX = Math.Abs(values[2] - values[0]) / 2;
            var radiusY = Math.Abs(values[3] - values[1]) / 2;
            if (radiusX <= 0 || radiusY <= 0) return;
            var start = Math.Atan2((values[5] - centerY) / radiusY, (values[4] - centerX) / radiusX);
            var end = Math.Atan2((values[7] - centerY) / radiusY, (values[6] - centerX) / radiusX);
            var sweep = end - start;
            if (clockwise)
            {
                while (sweep <= 0) sweep += FullTurn;
            }
            else
            {
                while (sweep >= 0) sweep -= FullTurn;
            }
            state.Builder.Ellipse(centerX, centerY, radiusX, radiusY, start, sweep, connect || repeated);
        }
    }

    private sealed class QuadrantCommand(bool horizontalFirst) : IEnhancedPathCommand
    {
        private const double Kappa = 0.5522847498307936;

        public int Arity => 2;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated)
        {
            if (!repeated) state.QuadrantHorizontal = horizontalFirst;
            var from = state.Builder.Current;
            var (x, y) = (values[0], values[1]);
            if (state.QuadrantHorizontal)
                state.Builder.Cubic(from.X + Kappa * (x - from.X), from.Y, x, y - Kappa * (y - from.Y), x, y);
            else
                state.Builder.Cubic(from.X, from.Y + Kappa * (y - from.Y), x - Kappa * (x - from.X), y, x, y);
            state.QuadrantHorizontal = !state.QuadrantHorizontal;
        }
    }

    private sealed class ArcAngleCommand : IEnhancedPathCommand
    {
        private const double Degree = Math.PI / 180;

        private readonly EllipticArc _arc = new();

        public int Arity => 4;

        public void Apply(EnhancedPathState state, IReadOnlyList<double> values, bool repeated) =>
            _arc.Append(state.Builder, values[0], values[1], values[2] * Degree, values[3] * Degree);
    }
}
