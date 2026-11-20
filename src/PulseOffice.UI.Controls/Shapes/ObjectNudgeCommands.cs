using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record ObjectNudgeCommand(string Name, ObjectNudge Nudge, string Gesture);

public interface IObjectNudgeCommands
{
    IReadOnlyList<ObjectNudgeCommand> Table { get; }

    IReadOnlyList<CommandDefinition> For(ModuleKind module, string prefix, Func<bool> enabled, Func<ObjectNudge, bool> apply);
}

public sealed class ObjectNudgeCommands : IObjectNudgeCommands
{
    private static readonly (string Name, int Across, int Down, string Key)[] Directions =
        [("Left", -1, 0, "Left"), ("Right", 1, 0, "Right"), ("Up", 0, -1, "Up"), ("Down", 0, 1, "Down")];

    private static readonly (string Name, ObjectNudgeKind Kind, bool Fine, string Modifiers)[] Kinds =
        [("move", ObjectNudgeKind.Move, false, ""), ("moveFine", ObjectNudgeKind.Move, true, "Ctrl+"),
         ("resize", ObjectNudgeKind.Resize, false, "Shift+"), ("resizeFine", ObjectNudgeKind.Resize, true, "Ctrl+Shift+")];

    public IReadOnlyList<ObjectNudgeCommand> Table { get; } =
        [.. Kinds.SelectMany(kind => Directions.Select(direction => new ObjectNudgeCommand(
            kind.Name + direction.Name,
            new ObjectNudge(direction.Across, direction.Down, kind.Kind, kind.Fine),
            kind.Modifiers + direction.Key)))];

    public IReadOnlyList<CommandDefinition> For(ModuleKind module, string prefix, Func<bool> enabled, Func<ObjectNudge, bool> apply)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        ArgumentNullException.ThrowIfNull(apply);
        return [.. Table.Select(entry => new CommandDefinition
        {
            Id = new CommandId($"{prefix}.object.nudge.{entry.Name}"),
            Module = module,
            Surface = CommandSurface.Palette,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            DefaultGesture = entry.Gesture,
            GestureRoutedByView = true,
            EnabledWhen = _ => enabled(),
            Execute = (_, _) =>
            {
                apply(entry.Nudge);
                return Task.CompletedTask;
            },
        })];
    }
}
