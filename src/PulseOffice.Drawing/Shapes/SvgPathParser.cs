using System.Globalization;
using System.Text.RegularExpressions;

namespace PulseOffice.Drawing.Shapes;

public interface ISvgPathParser
{
    ShapeOutline Parse(string data);
}

public sealed partial class SvgPathParser : ISvgPathParser
{
    [GeneratedRegex(@"[a-zA-Z]|[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex Tokens();

    public ShapeOutline Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return ShapeOutline.Empty;

        var tokens = Tokens().Matches(data).Select(match => match.Value).ToList();
        var figures = new List<ShapeFigure>();
        var segments = new List<ShapeSegment>();

        var command = '\0';
        var index = 0;
        double x = 0, y = 0, startX = 0, startY = 0;
        double lastControlX = 0, lastControlY = 0;
        var lastWasCurve = false;
        var afterClose = false;

        void Flush()
        {
            if (segments.Count > 1) figures.Add(new ShapeFigure([.. segments]));
            segments.Clear();
        }

        while (index < tokens.Count)
        {
            if (char.IsLetter(tokens[index][0]))
            {
                command = tokens[index][0];
                index++;

                if (command is 'Z' or 'z')
                {
                    segments.Add(CloseSegment.Instance);
                    Flush();
                    x = startX;
                    y = startY;
                    lastWasCurve = false;
                    afterClose = true;
                    continue;
                }
            }

            var relative = char.IsLower(command);
            var upper = char.ToUpperInvariant(command);

            if (afterClose && upper != 'M') segments.Add(new MoveSegment(new ShapePoint(x, y)));
            afterClose = false;

            bool Flag()
            {
                if (index >= tokens.Count) return false;
                var token = tokens[index];
                if (token.Length > 1 && token[0] is '0' or '1' && token[1] != '.')
                {
                    tokens[index] = token[1..];
                    return token[0] == '1';
                }

                index++;
                return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value != 0;
            }

            double Next()
            {
                if (index >= tokens.Count) return 0;
                if (!double.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return 0;
                index++;
                return double.IsFinite(value) ? value : 0;
            }

            switch (upper)
            {
                case 'M':
                {
                    var nx = Next(); var ny = Next();
                    x = relative ? x + nx : nx;
                    y = relative ? y + ny : ny;
                    Flush();
                    segments.Add(new MoveSegment(new ShapePoint(x, y)));
                    startX = x; startY = y;
                    command = relative ? 'l' : 'L';
                    lastWasCurve = false;
                    break;
                }

                case 'L':
                {
                    var nx = Next(); var ny = Next();
                    x = relative ? x + nx : nx;
                    y = relative ? y + ny : ny;
                    segments.Add(new LineSegment(new ShapePoint(x, y)));
                    lastWasCurve = false;
                    break;
                }

                case 'H':
                {
                    var nx = Next();
                    x = relative ? x + nx : nx;
                    segments.Add(new LineSegment(new ShapePoint(x, y)));
                    lastWasCurve = false;
                    break;
                }

                case 'V':
                {
                    var ny = Next();
                    y = relative ? y + ny : ny;
                    segments.Add(new LineSegment(new ShapePoint(x, y)));
                    lastWasCurve = false;
                    break;
                }

                case 'C':
                {
                    var x1 = Next(); var y1 = Next();
                    var x2 = Next(); var y2 = Next();
                    var nx = Next(); var ny = Next();

                    if (relative) { x1 += x; y1 += y; x2 += x; y2 += y; nx += x; ny += y; }

                    segments.Add(new CubicSegment(
                        new ShapePoint(x1, y1), new ShapePoint(x2, y2), new ShapePoint(nx, ny)));

                    lastControlX = x2; lastControlY = y2;
                    x = nx; y = ny;
                    lastWasCurve = true;
                    break;
                }

                case 'S':
                {
                    var x2 = Next(); var y2 = Next();
                    var nx = Next(); var ny = Next();

                    if (relative) { x2 += x; y2 += y; nx += x; ny += y; }

                    var x1 = lastWasCurve ? 2 * x - lastControlX : x;
                    var y1 = lastWasCurve ? 2 * y - lastControlY : y;

                    segments.Add(new CubicSegment(
                        new ShapePoint(x1, y1), new ShapePoint(x2, y2), new ShapePoint(nx, ny)));

                    lastControlX = x2; lastControlY = y2;
                    x = nx; y = ny;
                    lastWasCurve = true;
                    break;
                }

                case 'Q' or 'T':
                {
                    double qx, qy;

                    if (upper == 'Q')
                    {
                        qx = Next(); qy = Next();
                        if (relative) { qx += x; qy += y; }
                    }
                    else
                    {
                        qx = lastWasCurve ? 2 * x - lastControlX : x;
                        qy = lastWasCurve ? 2 * y - lastControlY : y;
                    }

                    var nx = Next(); var ny = Next();
                    if (relative) { nx += x; ny += y; }

                    segments.Add(new CubicSegment(
                        new ShapePoint(x + 2.0 / 3 * (qx - x), y + 2.0 / 3 * (qy - y)),
                        new ShapePoint(nx + 2.0 / 3 * (qx - nx), ny + 2.0 / 3 * (qy - ny)),
                        new ShapePoint(nx, ny)));

                    lastControlX = qx; lastControlY = qy;
                    x = nx; y = ny;
                    lastWasCurve = true;
                    break;
                }

                case 'A':
                {
                    var rx = Next(); var ry = Next();
                    var rotation = Next();
                    var large = Flag();
                    var sweep = Flag();
                    var nx = Next(); var ny = Next();

                    if (relative) { nx += x; ny += y; }

                    segments.Add(new ArcSegment(new ShapePoint(nx, ny), Math.Abs(rx), Math.Abs(ry), sweep) { LargeArc = large, Rotation = rotation });
                    x = nx; y = ny;
                    lastWasCurve = false;
                    break;
                }

                default:
                    index++;
                    break;
            }
        }

        Flush();
        return new ShapeOutline(figures);
    }
}
