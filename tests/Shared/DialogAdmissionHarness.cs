using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;

namespace PulseOffice.Testing;

public class ReportedMessages : System.Reflection.DispatchProxy
{
    public List<string> Messages { get; } = [];

    protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IStatusBarService.Report) && args is [string message, ..]) Messages.Add(message);
        return targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void) ? Activator.CreateInstance(type) : null;
    }
}

public sealed class DialogAdmissionHarness : IDisposable
{
    private readonly Window _window;

    public DialogAdmissionHarness(ModuleKind module, params IDocumentStateWitness[] witnesses)
    {
        Texts = new TextCatalog(UiLanguage.English);
        Status = System.Reflection.DispatchProxy.Create<IStatusBarService, ReportedMessages>();
        Workspace = new DocumentWorkspace();
        Document = new OpenDocument(module, "Target", new Panel(), new PulseOffice.Document.Commands.UndoStack());
        Workspace.Open(Document);
        var admission = new WorkspaceDialogAdmission(Workspace, witnesses, Status, Texts,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceDialogAdmission>.Instance);
        Host = new DialogHost();
        Host.Use(new IconCatalog(), Texts);
        Host.AdmitResultsThrough(() => admission);
        _window = new Window { Width = 900, Height = 700, Content = Host };
        _window.Show();
    }

    public TextCatalog Texts { get; }

    public IStatusBarService Status { get; }

    public IReadOnlyList<string> Reported => ((ReportedMessages)(object)Status).Messages;

    public DocumentWorkspace Workspace { get; }

    public OpenDocument Document { get; }

    public DialogHost Host { get; }

    public async Task<string?> AnswerAfter(Action meanwhile)
    {
        var shown = Host.ShowAsync<string>(new TextBlock { Text = "dialog" }, new DialogOptions { TitleKey = "prompt.cancel" });
        Dispatcher.UIThread.RunJobs();
        meanwhile();
        Host.Close("applied");
        Dispatcher.UIThread.RunJobs();
        return await shown;
    }

    public void Dispose() => _window.Close();
}
