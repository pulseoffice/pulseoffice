using System.Globalization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Scripting.Editor.Facades;

public interface IScriptDispatcher
{
    T Read<T>(Func<T> read);

    void Change(Action change);

    void Show(Action show);

    T Write<T>(Func<T> write);

    void Complete(Func<Task> change) => Write(change).GetAwaiter().GetResult();
}

public sealed class UiThreadScriptDispatcher : IScriptDispatcher
{
    public T Read<T>(Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        return Avalonia.Threading.Dispatcher.UIThread.CheckAccess()
            ? read()
            : Avalonia.Threading.Dispatcher.UIThread.Invoke(read);
    }

    public void Change(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) change();
        else Avalonia.Threading.Dispatcher.UIThread.Invoke(change);
    }

    public void Show(Action show) => Change(show);

    public T Write<T>(Func<T> write) => Read(write);

    public void Complete(Func<Task> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) _ = change();
        else Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(change).GetAwaiter().GetResult();
    }
}

public sealed class ReadOnlyGuardedDispatcher(IScriptDispatcher inner, Func<bool> readOnly) : IScriptDispatcher
{
    public T Read<T>(Func<T> read) => inner.Read(read);

    public void Change(Action change) => inner.Change(() =>
    {
        Refuse();
        change();
    });

    public void Show(Action show) => inner.Show(show);

    public T Write<T>(Func<T> write) => inner.Write(() =>
    {
        Refuse();
        return write();
    });

    public void Complete(Func<Task> change) => inner.Complete(() =>
    {
        Refuse();
        return change();
    });

    private void Refuse()
    {
        if (readOnly()) throw new InvalidOperationException("The document is read-only.");
    }
}

public abstract class ScriptFacade(IScriptDispatcher dispatcher)
{
    protected T Read<T>(Func<T> read) => dispatcher.Read(read);

    protected void Change(Action change) => dispatcher.Change(change);

    protected void Show(Action show) => dispatcher.Show(show);

    protected T Write<T>(Func<T> write) => dispatcher.Write(write);

    protected void Complete(Func<Task> change) => dispatcher.Complete(change);
}

public interface IScriptHost
{
    string ModuleName { get; }

    bool HasClipboard { get; }

    Task Copy();

    Task Cut();

    Task Paste();

    Task PasteValues();
}

public interface IScriptCommands
{
    bool Run(string command);
}

public sealed class AppFacade(
    IScriptDispatcher dispatcher,
    IScriptOutput output,
    IStatusBarService status,
    IScriptHost host,
    IScriptCommands commands) : ScriptFacade(dispatcher)
{
    public string Name => "PulseOffice";

    public string Module => host.ModuleName;

    public string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public string Version =>
        typeof(AppFacade).Assembly.GetName().Version?.ToString() ?? "0.0";

    public void Log(object? message) => output.Write(Render(message));

    public void Status(object? message) =>
        Show(() => status.Report(Render(message)));

    public bool HasClipboard => Read(() => host.HasClipboard);

    public void Copy() => Show(() => _ = host.Copy());

    public void Cut() => Complete(host.Cut);

    public void Paste() => Complete(host.Paste);

    public void PasteValues() => Complete(host.PasteValues);

    public bool Run(string command) => Read(() => commands.Run(command));

    private string Render(object? message) => message switch
    {
        null => string.Empty,
        double number => number.ToString("0.############", CultureInfo.CurrentCulture),
        System.Collections.IEnumerable list and not string =>
            string.Join(", ", list.Cast<object?>().Select(Render)),
        _ => message.ToString() ?? string.Empty,
    };
}
