using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.Logging;

namespace PulseOffice.Shell;

public sealed class ShellWindowCloseGate(
    QuitCoordinator quit,
    IUnsavedChangesGuard guard,
    Func<IShellWindowHost> host,
    ILogger<ShellWindowCloseGate> logger)
{
    private readonly HashSet<IShellWindowHandle> _confirmed = new(ReferenceEqualityComparer.Instance);

    public void Intercept(CancelEventArgs args, IShellWindowHandle window, IQuitTarget closeWindow)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(closeWindow);

        if (host().Windows.Count <= 1)
        {
            quit.Intercept(args, closeWindow);
            return;
        }

        if (_confirmed.Remove(window)) return;

        var documents = window.Workspace.Documents.ToList();
        foreach (var document in documents) document.CommitPendingEdits();
        if (!documents.Any(document => document.IsModified)) return;

        args.Cancel = true;
        _ = ConfirmThenCloseAsync(window, documents, closeWindow);
    }

    private async Task ConfirmThenCloseAsync(IShellWindowHandle window, IReadOnlyList<OpenDocument> documents, IQuitTarget closeWindow)
    {
        bool allowed;
        try
        {
            allowed = await guard.ConfirmCloseAsync(documents).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogError(ex, "Window close confirmation failed");
            return;
        }

        if (!allowed) return;
        _confirmed.Add(window);
        closeWindow.Quit();
    }
}

public sealed class CloseWindowTarget(Window window) : IQuitTarget
{
    public void Quit() => window.Close();
}

public sealed class ApplicationQuitTarget : IQuitTarget
{
    public void Quit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => lifetime.TryShutdown());
    }
}

public interface IShellWindowWiring
{
    void Attach(ShellWindow window);
}

public sealed class ShellWindowWiring(IDocumentArrivals arrivals, IExchangeDragDrop drops, ShellWindowCloseGate closing) : IShellWindowWiring
{
    public void Attach(ShellWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        arrivals.AcceptDrops(window);
        drops.AcceptDrops(window);
        var target = new CloseWindowTarget(window);
        window.Closing += (_, args) => closing.Intercept(args, window, target);
    }
}
