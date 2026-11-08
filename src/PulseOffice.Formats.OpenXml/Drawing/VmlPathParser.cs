using System.Globalization;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Formats.OpenXml;

public readonly record struct VmlCoordinateSpace(double OriginX, double OriginY, double Width, double Height)
{
    public ShapePoint Normalise(double x, double y) => new((x - OriginX) / Width, (y - OriginY) / Height);
}

public interface IVmlPathParser
{
    FreeformPath? Parse(string? path, VmlCoordinateSpace space, bool filled);
}

public sealed class VmlPathParser : IVmlPathParser
{
    private const double Kappa = 0.5522847498;

    public FreeformPath? Parse(string? path, VmlCoordinateSpace space, bool filled)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('@', StringComparison.Ordinal) || space.Width <= 0 || space.Height <= 0) return null;
        if (Tokens(path) is not { } tokens) return null;
        var builder = new OutlineBuilder(space);
        var index = 0;
        string? command = null;
        while (index < tokens.Count)
        {
            if (tokens[index].Command is { } named)
            {
                command = named;
                index++;
                if (!builder.Apply(named)) return null;
                continue;
            }
            if (command is null) return null;
            var arity = Arity(command);
            if (arity == 0 || index + arity > tokens.Count || tokens.Skip(index).Take(arity).Any(token => token.Command is not null)) return null;
            var values = tokens.Skip(index).Take(arity).Select(token => token.Value).ToArray();
            index += arity;
            if (!builder.Operand(command, values)) return null;
        }
        return builder.Build(filled);
    }

    private static int Arity(string command) => command switch
    {
        "m" or "l" or "t" or "r" or "qx" or "qy" => 2,
        "c" or "v" => 6,
        "wa" or "wr" or "at" or "ar" => 8,
        _ => 0,
    };

    private static List<(string? Command, double Value)>? Tokens(string path)
    {
        var tokens = new List<(string? Command, double Value)>();
        var position = 0;
        var expectingValue = false;
        while (position < path.Length)
        {
            var character = path[position];
            if (char.IsLetter(character))
            {
                if (expectingValue) tokens.Add((null, 0));
                var start = position;
                while (position < path.Length && char.IsLetter(path[position])) position++;
                foreach (var command in Commands(path[start..position]))
                {
                    if (command is null) return null;
                    tokens.Add((command, 0));
                }
                expectingValue = false;
                continue;
            }
            if (character == ',')
            {
                if (expectingValue || tokens.Count == 0 || tokens[^1].Command is not null) tokens.Add((null, 0));
                expectingValue = true;
                position++;
                continue;
            }
            if (char.IsWhiteSpace(character))
            {
                position++;
                continue;
            }
            var begin = position;
            if (character is '-' or '+') position++;
            while (position < path.Length && (char.IsDigit(path[position]) || path[position] == '.')) position++;
            if (position == begin || !double.TryParse(path[begin..position], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
                return null;
            tokens.Add((null, value));
            expectingValue = false;
        }
        return tokens;
    }

    private static IEnumerable<string?> Commands(string letters)
    {
        var position = 0;
        while (position < letters.Length)
        {
            var pair = position + 1 < letters.Length ? letters.Substring(position, 2) : null;
            if (pair is "nf" or "ns" or "wa" or "wr" or "at" or "ar" or "qx" or "qy")
            {
                yield return pair;
                position += 2;
                continue;
            }
            var single = letters[position].ToString();
            yield return single is "m" or "l" or "c" or "x" or "e" or "t" or "r" or "v" ? single : null;
            position++;
        }
    }

    private sealed class OutlineBuilder(VmlCoordinateSpace space)
    {
        private readonly List<PathFigure> _figures = [];
        private List<PathNode> _nodes = [];
        private double _x;
        private double _y;
        private bool _closed;
        private bool _quadrantAlongX;

        public bool Apply(string command)
        {
            switch (command)
            {
                case "x":
                    _closed = true;
                    Flush();
                    return true;
                case "e":
                    Flush();
                    return true;
                case "qx":
                    _quadrantAlongX = true;
                    return true;
                case "qy":
                    _quadrantAlongX = false;
                    return true;
                default:
                    return true;
            }
        }

        public bool Operand(string command, double[] values)
        {
            switch (command)
            {
                case "m": Move(values[0], values[1]); break;
                case "t": Move(_x + values[0], _y + values[1]); break;
                case "l": Line(values[0], values[1]); break;
                case "r": Line(_x + values[0], _y + values[1]); break;
                case "c": Cubic(values[0], values[1], values[2], values[3], values[4], values[5]); break;
                case "v": Cubic(_x + values[0], _y + values[1], _x + values[2], _y + values[3], _x + values[4], _y + values[5]); break;
                case "qx" or "qy": Quadrant(values[0], values[1]); break;
                default: return Arc(command, values);
            }
            return true;
        }

        public FreeformPath? Build(bool filled)
        {
            Flush();
            if (_figures.Count == 0 || _figures.SelectMany(figure => figure.Nodes).Any(node => !Finite(node))) return null;
            return new FreeformPath(_figures) { Filled = filled };
        }

        private bool Finite(PathNode node) =>
            new[] { node.Point, node.Before ?? node.Point, node.After ?? node.Point }.All(point => double.IsFinite(point.X) && double.IsFinite(point.Y));

        private void Move(double x, double y)
        {
            Flush();
            _nodes.Add(new PathNode(space.Normalise(x, y)));
            (_x, _y) = (x, y);
        }

        private void Line(double x, double y)
        {
            Ensure();
            _nodes.Add(new PathNode(space.Normalise(x, y)));
            (_x, _y) = (x, y);
        }

        private void Cubic(double x1, double y1, double x2, double y2, double x, double y)
        {
            Ensure();
            _nodes[^1] = _nodes[^1] with { After = space.Normalise(x1, y1), Kind = PathNodeKind.Smooth };
            _nodes.Add(new PathNode(space.Normalise(x, y)) { Before = space.Normalise(x2, y2), Kind = PathNodeKind.Smooth });
            (_x, _y) = (x, y);
        }

        private void Quadrant(double x, double y)
        {
            var (fromX, fromY) = (_x, _y);
            if (_quadrantAlongX)
                Cubic(fromX + Kappa * (x - fromX), fromY, x, y - Kappa * (y - fromY), x, y);
            else
                Cubic(fromX, fromY + Kappa * (y - fromY), x - Kappa * (x - fromX), y, x, y);
            _quadrantAlongX = !_quadrantAlongX;
        }

        private bool Arc(string command, double[] values)
        {
            var (left, top, right, bottom) = (values[0], values[1], values[2], values[3]);
            var radiusX = Math.Abs(right - left) / 2;
            var radiusY = Math.Abs(bottom - top) / 2;
            if (radiusX <= 0 || radiusY <= 0) return true;
            var centreX = (left + right) / 2;
            var centreY = (top + bottom) / 2;
            var start = Math.Atan2((values[5] - centreY) / radiusY, (values[4] - centreX) / radiusX);
            var end = Math.Atan2((values[7] - centreY) / radiusY, (values[6] - centreX) / radiusX);
            var clockwise = command is "wa" or "wr";
            var sweep = clockwise ? end - start : start - end;
            while (sweep <= 0) sweep += 2 * Math.PI;
            var startX = centreX + radiusX * Math.Cos(start);
            var startY = centreY + radiusY * Math.Sin(start);
            if (command is "ar" or "wr" || _nodes.Count == 0) Move(startX, startY);
            else Line(startX, startY);
            var steps = (int)Math.Ceiling(sweep / (Math.PI / 2));
            var step = sweep / steps * (clockwise ? 1 : -1);
            var angle = start;
            for (var index = 0; index < steps; index++)
            {
                var next = angle + step;
                var handle = 4.0 / 3 * Math.Tan((next - angle) / 4);
                var (cos, sin) = (Math.Cos(angle), Math.Sin(angle));
                var (nextCos, nextSin) = (Math.Cos(next), Math.Sin(next));
                Cubic(centreX + radiusX * (cos - handle * sin), centreY + radiusY * (sin + handle * cos),
                    centreX + radiusX * (nextCos + handle * nextSin), centreY + radiusY * (nextSin - handle * nextCos),
                    centreX + radiusX * nextCos, centreY + radiusY * nextSin);
                angle = next;
            }
            return true;
        }

        private void Ensure()
        {
            if (_nodes.Count == 0) _nodes.Add(new PathNode(space.Normalise(_x, _y)));
        }

        private void Flush()
        {
            if (_nodes.Count > 1) _figures.Add(new PathFigure(_nodes) { Closed = _closed });
            _nodes = [];
            _closed = false;
        }
    }
}
