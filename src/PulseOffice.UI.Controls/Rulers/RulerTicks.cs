namespace PulseOffice.UI.Controls.Rulers;

public enum RulerTickKind { Minor, Half, Whole, Label }

public sealed record RulerTick(double Points, RulerTickKind Kind, int Value);

public sealed class RulerTicks(double minimumGap)
{
    private static readonly int[] Subdivisions = [4, 2, 1];

    public IReadOnlyList<RulerTick> Plan(RulerModel model, double visibleFrom, double visibleTo, double labelExtent)
    {
        var unitPixels = model.UnitPoints * model.Scale;
        if (unitPixels <= 0) return [];
        var labelEvery = unitPixels < Math.Max(22, labelExtent * 1.6) ? 2 : 1;
        var clearance = Math.Max(minimumGap, labelExtent + 2);
        var parts = Subdivisions.FirstOrDefault(parts => unitPixels / parts >= clearance, 1);
        var step = model.UnitPoints / parts;
        var first = (int)Math.Ceiling((Math.Max(0, visibleFrom) - model.ActiveStart) / step);
        var last = (int)Math.Floor((Math.Min(model.Length, visibleTo) - model.ActiveStart) / step);
        var ticks = new List<RulerTick>();
        for (var index = first; index <= last; index++)
        {
            var points = model.ActiveStart + index * step;
            if (index % parts == 0)
            {
                var value = Math.Abs(index / parts);
                ticks.Add(new RulerTick(points, value != 0 && value % labelEvery == 0 ? RulerTickKind.Label : RulerTickKind.Whole, value));
                continue;
            }
            var quarter = index % parts * 4 / parts;
            ticks.Add(new RulerTick(points, quarter == 2 ? RulerTickKind.Half : RulerTickKind.Minor, 0));
        }
        return ticks;
    }
}
