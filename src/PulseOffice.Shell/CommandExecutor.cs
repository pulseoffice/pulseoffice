using PulseOffice.UI.Controls.Notifications;
using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public sealed class CommandExecutor(
    IStatusBarService status,
    INotificationHistory history,
    CommandRegistry registry,
    ITextCatalog texts,
    ICommandRecorder recorder,
    ICommandEffectChecks effects,
    ILogger<CommandExecutor> logger)
{
    private static readonly IReadOnlySet<string> NotRepeatable = new HashSet<string>(StringComparer.Ordinal)
    {
        "shell.edit.undo", "shell.edit.redo", "shell.edit.repeat",
        "shell.view.commandPalette", "shell.tools.options", "shell.help.shortcuts", "shell.tools.recordMacro",
    };

    public CommandDefinition? LastRepeatable { get; private set; }

    public event EventHandler<CommandDefinition>? Executed;

    public void Execute(CommandDefinition command, CommandContext context) =>
    _ = ExecuteAsync(command, context);

    public async Task<CommandOutcome> ExecuteAsync(
        CommandDefinition command, CommandContext context, CancellationToken ct = default)
    {
        if (!command.IsApplicable(context))
        {
            logger.LogDebug("Command {Command} is not applicable to the current context", command.Id);
            return new CommandOutcome(CommandOutcomeKind.NotApplicable, $"{registry.TitleOf(command)}: {texts["status.command.notApplicable"]}");
        }

        var heard = new List<Notification>();
        void Listen(object? sender, EventArgs e)
        {
            if (history.Entries is [var latest, ..]) heard.Add(latest.Notification);
        }

        history.Changed += Listen;
        var checks = effects.Open(command);
        try
        {
            logger.LogDebug("Executing command {Command}", command.Id);
            await command.Execute(context, ct).ConfigureAwait(true);
            checks.Finish();
        }
        catch (OperationCanceledException)
        {
            checks.Finish();
            var message = $"{registry.TitleOf(command)}: {texts["status.command.cancelled"]}";
            status.Report(message);
            return new CommandOutcome(CommandOutcomeKind.Cancelled, message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command {Command} failed", command.Id);
            checks.Finish();
            var message = $"{registry.TitleOf(command)}: {texts["status.command.failed"]}";
            status.Report(message, NotificationSeverity.Error);
            return new CommandOutcome(CommandOutcomeKind.Failed, $"{message} ({ex.Message})");
        }
        finally
        {
            history.Changed -= Listen;
        }

        Executed?.Invoke(this, command);
        var messages = heard.Select(notification => notification.Message).ToList();
        var refusals = heard.Where(notification => notification.Severity is NotificationSeverity.Warning or NotificationSeverity.Error).ToList();
        if (refusals.Count > 0 || checks.Refused)
        {
            logger.LogDebug("Command {Command} was refused", command.Id);
            return new CommandOutcome(CommandOutcomeKind.Refused, string.Join(" ", refusals.Select(notification => notification.Message).Distinct()), messages);
        }

        if (!NotRepeatable.Contains(command.Id.Value))
        {
            LastRepeatable = command;
            recorder.Note(command);
        }

        return new CommandOutcome(CommandOutcomeKind.Ran, null, messages);
    }
}
