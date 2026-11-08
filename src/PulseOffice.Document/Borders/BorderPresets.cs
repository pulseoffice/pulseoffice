namespace PulseOffice.Document.Borders;

public interface IBorderPreset
{
    string Key { get; }

    bool AppliesTo(BorderFrameScope scope);

    BorderFrame Apply(BorderFrame current, BorderFrameScope scope, BorderStroke stroke);
}

public abstract class BorderPreset(string key) : IBorderPreset
{
    public string Key { get; } = key;

    public virtual bool AppliesTo(BorderFrameScope scope) => true;

    public BorderFrame Apply(BorderFrame current, BorderFrameScope scope, BorderStroke stroke)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(stroke);
        return scope.Positions.Aggregate(current, (frame, position) => Change(frame, position, stroke));
    }

    protected abstract BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke);

    protected bool IsOuter(BorderPosition position) =>
        position is BorderPosition.Top or BorderPosition.Bottom or BorderPosition.Left or BorderPosition.Right;

    protected bool IsInside(BorderPosition position) =>
        position is BorderPosition.InsideHorizontal or BorderPosition.InsideVertical;
}

public sealed class NoBorderPreset() : BorderPreset("none")
{
    protected override BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke) => frame.With(position, null);
}

public sealed class OutlineBorderPreset() : BorderPreset("outline")
{
    protected override BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke) =>
        IsOuter(position) ? frame.With(position, stroke) : frame;
}

public sealed class AllBorderPreset() : BorderPreset("all")
{
    protected override BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke) =>
        IsOuter(position) || IsInside(position) ? frame.With(position, stroke) : frame;
}

public sealed class InsideBorderPreset() : BorderPreset("inside")
{
    public override bool AppliesTo(BorderFrameScope scope) => scope.IsRange;

    protected override BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke) =>
        IsInside(position) ? frame.With(position, stroke) : frame;
}

public sealed class ThickOutlineBorderPreset() : BorderPreset("thickOutline")
{
    private const double Factor = 2.5;

    protected override BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke) =>
        IsOuter(position) ? frame.With(position, stroke with { WidthPoints = stroke.WidthPoints * Factor })
        : IsInside(position) ? frame.With(position, stroke)
        : frame;
}

public sealed class SideBorderPreset(BorderPosition side, string key) : BorderPreset(key)
{
    protected override BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke) =>
        position == side ? frame.With(position, stroke) : frame;
}

public sealed class TopAndBottomBorderPreset() : BorderPreset("topBottom")
{
    protected override BorderFrame Change(BorderFrame frame, BorderPosition position, BorderStroke stroke) =>
        position is BorderPosition.Top or BorderPosition.Bottom ? frame.With(position, stroke) : frame;
}

public interface IBorderPresetCatalog
{
    IReadOnlyList<IBorderPreset> For(BorderFrameScope scope);
}

public sealed class BorderPresetCatalog : IBorderPresetCatalog
{
    private readonly IReadOnlyList<IBorderPreset> _presets =
    [
        new NoBorderPreset(),
        new OutlineBorderPreset(),
        new AllBorderPreset(),
        new InsideBorderPreset(),
        new ThickOutlineBorderPreset(),
        new SideBorderPreset(BorderPosition.Bottom, "bottom"),
        new SideBorderPreset(BorderPosition.Top, "top"),
        new SideBorderPreset(BorderPosition.Left, "left"),
        new SideBorderPreset(BorderPosition.Right, "right"),
        new TopAndBottomBorderPreset(),
    ];

    public IReadOnlyList<IBorderPreset> For(BorderFrameScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return [.. _presets.Where(preset => preset.AppliesTo(scope))];
    }
}

public interface IBorderFrameEditing
{
    BorderFrame Toggle(BorderFrame frame, BorderFrameScope scope, BorderPosition position, BorderStroke stroke);

    BorderFrame Restroke(BorderFrame frame, BorderStroke stroke);
}

public sealed class BorderFrameEditing : IBorderFrameEditing
{
    public BorderFrame Toggle(BorderFrame frame, BorderFrameScope scope, BorderPosition position, BorderStroke stroke)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(scope);
        if (!scope.Allows(position)) return frame;
        return frame.With(position, frame.Stroke(position) == stroke ? null : stroke);
    }

    public BorderFrame Restroke(BorderFrame frame, BorderStroke stroke)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return frame.Positions.ToList().Aggregate(frame, (current, position) => current.With(position, stroke));
    }
}
