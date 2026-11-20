using Avalonia.Input;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Shortcuts;

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
    ObjectNudge? Read(ModuleKind module, Key key, KeyModifiers modifiers);
}

public interface IObjectNudgeBindings
{
    string? GestureOf(ModuleKind module, ObjectNudgeCommand command);
}

public sealed class FactoryObjectNudgeBindings : IObjectNudgeBindings
{
    public string? GestureOf(ModuleKind module, ObjectNudgeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Gesture;
    }
}

public sealed class RegistryObjectNudgeBindings(CommandRegistry registry, IObjectNudgeCommands nudges) : IObjectNudgeBindings
{
    public string? GestureOf(ModuleKind module, ObjectNudgeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return registry.Find(nudges.IdOf(module, command.Name)) is { } registered ? registered.DefaultGesture : command.Gesture;
    }
}

public sealed class ObjectNudgeKeys(IObjectNudgeCommands nudges, IObjectNudgeBindings bindings, IShortcutCapture capture) : IObjectNudgeKeys
{
    private const ChordModifiers FineAliases = ChordModifiers.Control | ChordModifiers.Alt | ChordModifiers.Super;

    private readonly GestureChordParser _parser = new();

    public ObjectNudgeKeys()
        : this(new ObjectNudgeCommands(), new FactoryObjectNudgeBindings(), new ShortcutCapture())
    {
    }

    public ObjectNudge? Read(ModuleKind module, Key key, KeyModifiers modifiers)
    {
        if (capture.Chord(key, modifiers) is not { } chord) return null;
        return Bound(module, chord) ?? (Alias(chord) is { } alias ? Bound(module, alias) : null);
    }

    private ObjectNudge? Bound(ModuleKind module, GestureChord chord)
    {
        var pressed = chord.Canonical();
        foreach (var command in nudges.Table)
            if (_parser.Parse(bindings.GestureOf(module, command)) is { } bound
                && string.Equals(bound.Canonical(), pressed, StringComparison.OrdinalIgnoreCase))
                return command.Nudge;
        return null;
    }

    private static GestureChord? Alias(GestureChord chord)
    {
        var held = chord.Modifiers & FineAliases;
        if (chord.Modifiers.HasFlag(ChordModifiers.Primary)) return null;
        if (held is not (ChordModifiers.Control or ChordModifiers.Alt or ChordModifiers.Super)) return null;
        return chord with { Modifiers = (chord.Modifiers & ~held) | ChordModifiers.Primary };
    }
}
