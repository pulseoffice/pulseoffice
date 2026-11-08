using Avalonia.Input;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record NudgeSteps(double Small, double Large);

public readonly record struct ObjectNudge(int Across, int Down, bool Large)
{
    public (double X, double Y) Distance(NudgeSteps steps)
    {
        var step = Large ? steps.Large : steps.Small;
        return (Across * step, Down * step);
    }
}

public interface IObjectNudgeKeys
{
    ObjectNudge? Read(Key key, KeyModifiers modifiers);
}

public sealed class ObjectNudgeKeys : IObjectNudgeKeys
{
    public ObjectNudge? Read(Key key, KeyModifiers modifiers)
    {
        if (modifiers is not (KeyModifiers.None or KeyModifiers.Shift)) return null;
        var large = modifiers == KeyModifiers.Shift;
        return key switch
        {
            Key.Left => new ObjectNudge(-1, 0, large),
            Key.Right => new ObjectNudge(1, 0, large),
            Key.Up => new ObjectNudge(0, -1, large),
            Key.Down => new ObjectNudge(0, 1, large),
            _ => null,
        };
    }
}
