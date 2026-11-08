using System.Globalization;
using System.Text;

namespace PulseOffice.Drawing.Shapes;

public interface IShapePathWriter
{
    string Write(ShapeOutline outline);
}

public sealed class ShapePathWriter : IShapePathWriter
{
    private readonly PathReplay _replay = new();

    public string Write(ShapeOutline outline)
    {
        var sink = new SvgPathSink();
        _replay.Replay(outline, sink);
        return sink.ToString();
    }

    private sealed class SvgPathSink : PathSink
    {
        private readonly StringBuilder _builder = new();

        public override void MoveTo(ShapePoint to) => Command('M').Append(Pair(to));

        public override void LineTo(ShapePoint to) => Command('L').Append(Pair(to));

        public override void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to) =>
            Command('C').Append(Pair(first)).Append(' ').Append(Pair(second)).Append(' ').Append(Pair(to));

        public override void ArcTo(ShapePoint from, ArcSegment arc) =>
            Command('A').Append(Number(arc.RadiusX)).Append(' ').Append(Number(arc.RadiusY))
                .Append(' ').Append(Number(arc.Rotation)).Append(arc.LargeArc ? " 1 " : " 0 ").Append(arc.Clockwise ? '1' : '0').Append(' ').Append(Pair(arc.To));

        public override void Close() => Command('Z');

        public override string ToString() => _builder.ToString();

        private StringBuilder Command(char letter) => (_builder.Length > 0 ? _builder.Append(' ') : _builder).Append(letter);

        private string Pair(ShapePoint point) => $"{Number(point.X)} {Number(point.Y)}";

        private string Number(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);
    }
}
