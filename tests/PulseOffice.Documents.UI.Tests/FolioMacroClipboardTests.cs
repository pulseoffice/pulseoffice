using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.UI.Scripting;
using PulseOffice.Exchange;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioMacroClipboardTests
{
    [Fact]
    public Task UnIncollaDaMacroCheFallisceLoDiceInBarraDiStato() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var status = scene.Services.GetRequiredService<IStatusBarService>();
        var host = new FolioScriptHost(DispatchProxy.Create<IObjectExchange, BrokenExchange>(), scene.Services.GetRequiredService<IScriptClipboardOperations>());

        host.Paste();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var transient = status.Segments.SingleOrDefault(segment => segment.Id == "shell.transient");
        Assert.NotNull(transient);
        Assert.True(transient.IsVisible);
        var texts = scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        Assert.Equal($"{texts["command.folio.clipboard.paste.title"]}: {texts["status.command.failed"]}", transient.Text);
        return Task.CompletedTask;
    });

    [Fact]
    public async Task LoperazioneCheRiesceNonScriveNulla()
    {
        var operations = new ScriptClipboardOperations(
            DispatchProxy.Create<IStatusBarService, BrokenExchange>(),
            new PulseOffice.Localization.TextCatalog(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ScriptClipboardOperations>.Instance);

        var error = await Record.ExceptionAsync(() => operations.RunAsync("command.folio.clipboard.copy.title", () => Task.CompletedTask));

        Assert.Null(error);
    }

    [Fact]
    public void UnIncollaDaMacroFinisceDentroLaMacro()
    {
        var exchange = DispatchProxy.Create<IObjectExchange, SlowExchange>();
        var operations = new ScriptClipboardOperations(
            DispatchProxy.Create<IStatusBarService, BrokenExchange>(),
            new PulseOffice.Localization.TextCatalog(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ScriptClipboardOperations>.Instance);
        var facade = new AppFacade(new DirectDispatcher(), null!, null!, new FolioScriptHost(exchange, operations), null!);

        facade.Paste();

        Assert.True(((SlowExchange)(object)exchange).Pasted);
    }

    private sealed class DirectDispatcher : IScriptDispatcher
    {
        public T Read<T>(Func<T> read) => read();

        public void Change(Action change) => change();

        public void Show(Action show) => show();

        public T Write<T>(Func<T> write) => write();
    }

    public class SlowExchange : DispatchProxy
    {
        public bool Pasted { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(Task<bool>))
                return Task.Run(async () =>
                {
                    await Task.Delay(150).ConfigureAwait(false);
                    Pasted = true;
                    return true;
                });
            throw new InvalidOperationException(targetMethod?.Name);
        }
    }

    public class BrokenExchange : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(Task<bool>)) return Task.FromException<bool>(new IOException("clipboard unavailable"));
            if (targetMethod?.ReturnType == typeof(Task<ExchangePackage?>)) return Task.FromException<ExchangePackage?>(new IOException("clipboard unavailable"));
            throw new InvalidOperationException(targetMethod?.Name);
        }
    }
}
