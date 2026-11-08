using System.Text.Json.Nodes;
using PulseOffice.Agent.Hosting;

namespace PulseOffice.Agent.Tools;

public interface IAgentCommandPolicy
{
    bool IsForbidden(string id);

    AgentAccessLevel AccessOf(AgentCommandInfo command);

    bool IsIrreversible(AgentCommandInfo command);

    AgentConfirmationKind? ConfirmationOf(AgentCommandInfo command);
}

public sealed class AgentCommandPolicy : IAgentCommandPolicy
{
    private readonly IReadOnlySet<string> _forbidden = new HashSet<string>(StringComparer.Ordinal)
    {
        "shell.tools.options",
        "shell.file.quit",
        "shell.tools.recordMacro",
    };

    private readonly IReadOnlyList<string> _forbiddenFragments = [".macro.", ".security", ".signature", ".password", ".options"];

    private readonly IReadOnlyList<string> _fileFragments =
        [".export", ".print", ".send", ".share", ".publish", ".open", ".save", ".import", ".insertFile", "FromFile", ".link", "imageLink", ".externalEdit", ".remote", ".scan",
         ".refresh", ".provider", ".streams", ".externalLinks", ".xmlSource", ".updateLinks"];

    private readonly IReadOnlyList<string> _irreversibleFragments =
        [".delete", ".remove", ".close", ".send", ".share", ".publish", ".print", ".clearAll", ".discard", ".revert", ".reload"];

    public bool IsForbidden(string id) =>
        _forbidden.Contains(id) || _forbiddenFragments.Any(fragment => id.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public AgentAccessLevel AccessOf(AgentCommandInfo command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.CategoryKey == "file" || command.RunsDocumentQueries || _fileFragments.Any(fragment => command.Id.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            ? AgentAccessLevel.Files
            : AgentAccessLevel.Edit;
    }

    public bool IsIrreversible(AgentCommandInfo command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.OutsideHistory || _irreversibleFragments.Any(fragment => command.Id.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public AgentConfirmationKind? ConfirmationOf(AgentCommandInfo command) =>
        command.RunsDocumentQueries ? AgentConfirmationKind.DocumentQueries
        : IsIrreversible(command) ? AgentConfirmationKind.Irreversible
        : null;
}

public sealed class CommandToolSource(IAgentCommands commands, IAgentCommandPolicy policy, IAgentModuleNames modules, Security.IAgentPreferences preferences) : IAgentToolSource
{
    public IEnumerable<AgentTool> Tools =>
    [
        new ListCommandsTool(commands, policy, modules, preferences),
        new RunCommandTool(commands, policy, preferences),
    ];
}

internal sealed class ListCommandsTool(IAgentCommands commands, IAgentCommandPolicy policy, IAgentModuleNames modules, Security.IAgentPreferences preferences) : AgentTool
{
    public override string Name => "list_commands";

    public override string Description =>
        "Lists the PulseOffice commands (the same ones of ribbon, menus and command palette) that are enabled in the current context, " +
        "with id, title, description, category and module. Optional 'search' filters by text.";

    public override AgentAccessLevel Access => AgentAccessLevel.Read;

    public override JsonObject InputSchema() => new SchemaBuilder()
        .String("search", "Optional words to look for in id, title, description and category.")
        .Boolean("includeDisabled", "Also list the commands present but disabled in the current context.")
        .Build();

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var terms = (arguments.OptionalText("search") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var listed = await commands.ListAsync(arguments.Flag("includeDisabled", false), ct).ConfigureAwait(true);

        var result = new JsonArray();
        foreach (var command in listed.Where(command => !policy.IsForbidden(command.Id) && Matches(command, terms)))
            result.Add(new JsonObject
            {
                ["id"] = command.Id,
                ["title"] = command.Title,
                ["description"] = command.Description,
                ["category"] = command.Category,
                ["module"] = modules.Prefix(command.Module),
                ["enabled"] = command.IsEnabled,
                ["permission"] = policy.AccessOf(command).ToString().ToLowerInvariant(),
                ["allowed"] = policy.AccessOf(command) <= preferences.Level,
                ["confirmation"] = policy.ConfirmationOf(command) is not null,
            });

        return AgentToolResult.Json(result);
    }

    private static bool Matches(AgentCommandInfo command, string[] terms) =>
        terms.All(term =>
            command.Id.Contains(term, StringComparison.OrdinalIgnoreCase)
            || command.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
            || command.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) == true
            || command.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
}

internal sealed class RunCommandTool(IAgentCommands commands, IAgentCommandPolicy policy, Security.IAgentPreferences preferences) : AgentTool
{
    public override string Name => "run_command";

    public override string Description =>
        "Runs a PulseOffice command by id (see list_commands) on the active document, as if the user chose it. " +
        "Commands that open a dialog wait for the user. Commands that delete, close, send or print, those that undo cannot revert (protection, recording changes) and those that run the document's database queries ask the user to confirm.";

    public override AgentAccessLevel Access => AgentAccessLevel.Edit;

    public override AgentToolAnnotations Annotations => new(false, true);

    public override JsonObject InputSchema() => new SchemaBuilder().String("id", "Command id, for example ledger.format.bold.", required: true).Build();

    public override async Task<AgentConfirmationNeed?> ConfirmationAsync(AgentArguments arguments, CancellationToken ct)
    {
        var command = await FindAsync(arguments.Text("id"), ct).ConfigureAwait(true);
        if (policy.AccessOf(command) > preferences.Level)
            throw new AgentArgumentException(
                $"The command '{command.Id}' needs the '{policy.AccessOf(command).ToString().ToLowerInvariant()}' permission, " +
                $"but PulseOffice allows only '{preferences.Level.ToString().ToLowerInvariant()}'.");
        return policy.ConfirmationOf(command) is { } kind ? new AgentConfirmationNeed(kind, $"{command.Title} ({command.Id})") : null;
    }

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var command = await FindAsync(arguments.Text("id"), ct).ConfigureAwait(true);
        var run = await commands.RunAsync(command.Id, command.Title, ct).ConfigureAwait(true);
        return run.Outcome switch
        {
            AgentCommandOutcome.Ran => AgentToolResult.Text(run.Reason is { Length: > 0 } said ? $"Ran {command.Id}: {said}" : $"Ran {command.Id}."),
            AgentCommandOutcome.Refused => AgentToolResult.Failure($"The command '{command.Id}' did not run: {run.Reason}"),
            AgentCommandOutcome.Disabled => AgentToolResult.Failure($"The command '{command.Id}' is disabled in the current context."),
            AgentCommandOutcome.NotFound => AgentToolResult.Failure($"Unknown command '{command.Id}'."),
            _ => AgentToolResult.Failure(run.Reason is { Length: > 0 } why ? $"The command '{command.Id}' failed: {why}" : $"The command '{command.Id}' failed."),
        };
    }

    private async Task<AgentCommandInfo> FindAsync(string id, CancellationToken ct)
    {
        if (policy.IsForbidden(id)) throw new AgentArgumentException($"The command '{id}' is not available to agents.");
        return (await commands.ListAsync(true, ct).ConfigureAwait(true)).FirstOrDefault(command => command.Id == id)
            ?? throw new AgentArgumentException($"Unknown or absent command '{id}': see list_commands.");
    }
}
