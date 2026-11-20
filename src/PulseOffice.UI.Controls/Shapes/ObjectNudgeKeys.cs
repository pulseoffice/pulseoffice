using Avalonia.Input;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record NudgeSteps(double Normal, double Fine);

public enum ObjectNudgeKind
{
    Move,
    Resize,
}

public readonly record struct ObjectNudge(int Across, int Down, ObjectNudgeKind Kind, bool Fine)
{
    public bool Resizes => Kind == ObjectNudgeKind.Resize;

    public (double X, double Y) Distance(NudgeSteps steps)
    {
        var step = Fine ? steps.Fine : steps.Normal;
        return (Across * step, Down * step);
    }

    public (double X, double Y, double Width, double Height) Apply(double x, double y, double width, double height, NudgeSteps steps, double minimum)
    {
        var (dx, dy) = Distance(steps);
        return Resizes
            ? (x, y, Math.Max(minimum, width + dx), Math.Max(minimum, height + dy))
            : (x + dx, y + dy, width, height);
    }
}

public interface IObjectNudgeKeys
{
    ObjectNudge? Read(Key key, KeyModifiers modifiers);
}

public sealed class ObjectNudgeKeys : IObjectNudgeKeys
{
    private const KeyModifiers FineModifiers = KeyModifiers.Control | KeyModifiers.Alt;

    public ObjectNudge? Read(Key key, KeyModifiers modifiers)
    {
        var fine = (modifiers & FineModifiers) != 0;
        if ((modifiers & FineModifiers) == FineModifiers) return null;
        var rest = modifiers & ~FineModifiers;
        if (rest is not (KeyModifiers.None or KeyModifiers.Shift)) return null;
        var kind = rest == KeyModifiers.Shift ? ObjectNudgeKind.Resize : ObjectNudgeKind.Move;
        return key switch
        {
            Key.Left => new ObjectNudge(-1, 0, kind, fine),
            Key.Right => new ObjectNudge(1, 0, kind, fine),
            Key.Up => new ObjectNudge(0, -1, kind, fine),
            Key.Down => new ObjectNudge(0, 1, kind, fine),
            _ => null,
        };
    }
}
