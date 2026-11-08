using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Commands;

[Flags]
public enum CommandSurface
{
    None = 0,
    Ribbon = 1 << 0,
    Menu = 1 << 1,
    ContextMenu = 1 << 2,
    Palette = 1 << 3,
    StatusBar = 1 << 4,

    Common = Ribbon | Menu | Palette,
    Advanced = Menu | Palette,
}

public readonly record struct CommandId(string Value)
{
    public override string ToString() => Value;
    public static implicit operator string(CommandId id) => id.Value;
}

public sealed record CommandDefinition
{
    public required CommandId Id { get; init; }

    public string TitleKey => $"command.{Id.Value}.title";

    public string DescriptionKey => $"command.{Id.Value}.description";

    public string KeywordsKey => $"command.{Id.Value}.keywords";

    public string? TitleOverride { get; init; }

    public string? IconKey { get; init; }

    public ModuleKind Module { get; init; } = ModuleKind.Shell;

    public CommandSurface Surface { get; init; } = CommandSurface.Common;

    public string? MenuPath { get; init; }

    public IReadOnlyList<string> ContextPaths { get; init; } = [];

    public string? RibbonPath { get; init; }

    public IReadOnlyDictionary<ModuleKind, string> RibbonPaths { get; init; } = new Dictionary<ModuleKind, string>();

    public string? RibbonPathIn(ModuleKind module) =>
        RibbonPaths.TryGetValue(module, out var path) ? path : RibbonPath;

    public string? DefaultGesture { get; init; }

    public SelectionKind RequiredSelection { get; init; } = SelectionKind.None;

    public DocumentState RequiredState { get; init; } = DocumentState.None;

    public DocumentState ForbiddenState { get; init; } = DocumentState.None;

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public Func<CommandContext, bool>? EnabledWhen { get; init; }

    public Func<CommandContext, bool>? CheckedWhen { get; init; }

    public RibbonControlSpec? RibbonControl { get; init; }

    public bool OutsideHistory { get; init; }

    public bool RunsDocumentQueries { get; init; }

    public required Func<CommandContext, CancellationToken, Task> Execute { get; init; }

    private const DocumentState PresenceStates = DocumentState.DocumentOpen;

    public bool IsPresent(CommandContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (Module != ModuleKind.Shell && Module != ctx.Module) return false;

        var required = RequiredState & PresenceStates;
        var forbidden = ForbiddenState & PresenceStates;

        return (ctx.State & required) == required && (ctx.State & forbidden) == 0;
    }

    public bool IsEnabled(CommandContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!IsPresent(ctx)) return false;
        if ((ctx.State & RequiredState) != RequiredState) return false;
        if ((ctx.State & ForbiddenState) != 0) return false;
        if (RequiredSelection != SelectionKind.None && (ctx.Selection & RequiredSelection) == 0) return false;

        return EnabledWhen?.Invoke(ctx) ?? true;
    }

    public bool IsApplicable(CommandContext ctx) => IsEnabled(ctx);
}
