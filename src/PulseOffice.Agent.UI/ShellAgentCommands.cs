using PulseOffice.Agent.Hosting;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;

namespace PulseOffice.Agent.UI;

public sealed class ShellAgentCommands(
    IDocumentWorkspace workspace,
    CommandRegistry registry,
    Func<CommandExecutor> executor,
    IAgentUiThread ui,
    IAgentOperationGate gate,
    ITextCatalog texts) : IAgentCommands
{
    private readonly IReadOnlySet<string> _outsideHistory = new HashSet<string>(StringComparer.Ordinal)
    {
        "shell.edit.undo", "shell.edit.redo", "shell.edit.repeat",
    };

    public Task<IReadOnlyList<AgentCommandInfo>> ListAsync(bool includeDisabled, CancellationToken ct) => ui.InvokeAsync(() =>
    {
        var context = workspace.Context;
        var listed = registry.All
            .Where(command => command.IsPresent(context))
            .Select(command => new AgentCommandInfo(
                command.Id.Value, registry.TitleOf(command), registry.DescriptionOf(command), registry.CategoryOf(command),
                registry.CategoryKeyOf(command), command.Module, command.IsEnabled(context), command.OutsideHistory, command.RunsDocumentQueries))
            .Where(command => includeDisabled || command.IsEnabled)
            .ToList();
        return Task.FromResult<IReadOnlyList<AgentCommandInfo>>(listed);
    });

    public Task<AgentCommandRun> RunAsync(string id, string undoLabel, CancellationToken ct) =>
        ui.InvokeAsync(() => gate.EditAsync(() => RunHereAsync(id, undoLabel, ct), ct));

    private async Task<AgentCommandRun> RunHereAsync(string id, string undoLabel, CancellationToken ct)
    {
        if (registry.Find(new CommandId(id)) is not { } command) return new AgentCommandRun(AgentCommandOutcome.NotFound);
        var context = workspace.Context;
        if (!command.IsEnabled(context)) return new AgentCommandRun(AgentCommandOutcome.Disabled);

        var step = workspace.Active is { } document && !_outsideHistory.Contains(id)
            ? document.History.BeginLabelledStep(texts.Format("agent.history", undoLabel))
            : null;
        CommandOutcome outcome;
        try
        {
            outcome = await executor().ExecuteAsync(command, context, ct).ConfigureAwait(true);
        }
        finally
        {
            step?.Dispose();
        }

        return outcome.Kind switch
        {
            CommandOutcomeKind.Ran => new AgentCommandRun(AgentCommandOutcome.Ran, outcome.Notes.Count == 0 ? null : string.Join(" ", outcome.Notes.Distinct())),
            CommandOutcomeKind.NotApplicable => new AgentCommandRun(AgentCommandOutcome.Disabled, outcome.Reason),
            CommandOutcomeKind.Failed => new AgentCommandRun(AgentCommandOutcome.Failed, outcome.Reason),
            _ => new AgentCommandRun(AgentCommandOutcome.Refused, outcome.Reason),
        };
    }
}
