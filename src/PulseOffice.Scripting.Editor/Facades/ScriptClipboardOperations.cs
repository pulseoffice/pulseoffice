using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Scripting.Editor.Facades;

public interface IScriptClipboardOperations
{
    Task RunAsync(string titleKey, Func<Task> operation);
}

public sealed class ScriptClipboardOperations(
    IStatusBarService status,
    ITextCatalog texts,
    ILogger<ScriptClipboardOperations> logger) : IScriptClipboardOperations
{
    public async Task RunAsync(string titleKey, Func<Task> operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleKey);
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Macro clipboard operation {Operation} was cancelled", titleKey);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            logger.LogError(error, "Macro clipboard operation {Operation} failed", titleKey);
            status.Report($"{texts[titleKey]}: {texts["status.command.failed"]}", NotificationSeverity.Error);
        }
    }
}
