using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Shapes;

public sealed record ObjectNudgeCommand(string Name, ObjectNudge Nudge, string Gesture);

public interface IObjectNudgeCommands
{
    IReadOnlyList<ObjectNudgeCommand> Table { get; }

    CommandId IdOf(ModuleKind module, string name);

    IReadOnlyList<CommandDefinition> For(ModuleKind module, Func<bool> enabled, Func<ObjectNudge, bool> apply);
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

    public CommandId IdOf(ModuleKind module, string name) => new($"{PrefixOf(module)}.object.nudge.{name}");

    private static string PrefixOf(ModuleKind module) => module switch
    {
        ModuleKind.Sheets => "ledger",
        ModuleKind.Presentations => "deck",
        ModuleKind.Documents => "folio",
        _ => module.ToString().ToLowerInvariant(),
    };

    public IReadOnlyList<CommandDefinition> For(ModuleKind module, Func<bool> enabled, Func<ObjectNudge, bool> apply)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        ArgumentNullException.ThrowIfNull(apply);
        return [.. Table.Select(entry => new CommandDefinition
        {
            Id = IdOf(module, entry.Name),
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
