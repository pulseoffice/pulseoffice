using Microsoft.Extensions.Logging;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioEditingKeys
{
    bool Undo(FolioEditor editor);

    bool Redo(FolioEditor editor);

    Task CopyAsync();

    Task CutAsync();

    Task PasteAsync();
}

public sealed class FolioEditingKeys(
    ICommandEffectChecks effects,
    IObjectExchange exchange,
    IStatusBarService status,
    ITextCatalog texts,
    ILogger<FolioEditingKeys> logger) : IFolioEditingKeys
{
    private readonly CommandDefinition _undo = Step("shell.edit.undo", ModuleKind.Shell);

    private readonly CommandDefinition _redo = Step("shell.edit.redo", ModuleKind.Shell);

    private readonly CommandDefinition _cut = Step("folio.clipboard.cut", ModuleKind.Documents);

    private readonly CommandDefinition _copy = Step("folio.clipboard.copy", ModuleKind.Documents);

    private readonly CommandDefinition _paste = Step("folio.clipboard.paste", ModuleKind.Documents);

    public bool Undo(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (editor.ReadOnly() || !editor.History.CanUndo) return false;
        return Guarded(_undo, editor.History.Undo);
    }

    public bool Redo(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (editor.ReadOnly() || !editor.History.CanRedo) return false;
        return Guarded(_redo, editor.History.Redo);
    }

    public Task CopyAsync() => GuardedAsync(_copy, () => exchange.CopyAsync(ModuleKind.Documents));

    public Task CutAsync() => GuardedAsync(_cut, () => exchange.CutAsync(ModuleKind.Documents));

    public Task PasteAsync() => GuardedAsync(_paste, () => exchange.PasteAsync(ModuleKind.Documents));

    private bool Guarded(CommandDefinition step, Action move)
    {
        var scope = effects.Open(step);
        try
        {
            move();
        }
        finally
        {
            scope.Finish();
        }
        return !scope.Refused;
    }

    private async Task GuardedAsync(CommandDefinition step, Func<Task> run)
    {
        var scope = effects.Open(step);
        try
        {
            await run().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Keyboard {Command} was cancelled", step.Id);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            logger.LogError(error, "Keyboard {Command} failed", step.Id);
            status.Report($"{texts[$"command.{step.Id.Value}.title"]}: {texts["status.command.failed"]}", NotificationSeverity.Error);
        }
        finally
        {
            scope.Finish();
        }
    }

    private static CommandDefinition Step(string id, ModuleKind module) =>
        new() { Id = new CommandId(id), Module = module, Execute = (_, _) => Task.CompletedTask };
}
