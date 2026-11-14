using Avalonia.Controls;
using PulseOffice.Desktop.Composition;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Printing;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class TemplateSaveTests(HeadlessDesktopSession headless) : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-templates-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, AgentDocumentHoldTests.Inert>();

    private sealed class Answers(params object?[] replies) : IDialogService
    {
        private readonly Queue<object?> _replies = new(replies);

        public List<Control> Shown { get; } = [];

        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            Shown.Add(content);
            return Task.FromResult((TResult?)_replies.Dequeue());
        }

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result) { }
    }

    private sealed class Writer : IDocumentOperations
    {
        public List<string> Written { get; } = [];

        public ModuleKind Module => ModuleKind.Sheets;

        public string NativeExtension => "ods";

        public string OfficeExtension => "xlsx";

        public string TemplateExtension => "ots";

        public string? OfficeTemplateExtension => null;

        public string SuggestedName(OpenDocument document) => "Budget";

        public IPrintableDocument? BuildPrintable(OpenDocument document) => null;

        public PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document) => null;

        public bool WriteTo(OpenDocument document, string path)
        {
            Written.Add(path);
            File.WriteAllText(path, "new");
            return true;
        }

        public string? RenderHtml(OpenDocument document) => null;

        public PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document) => null;
    }

    private sealed class Library(string folder) : ITemplateLibrary
    {
        public string Folder => folder;

        public IReadOnlyList<string> Extensions => [".ots"];

        public IReadOnlyList<TemplateEntry> Entries() => [];

        public IDocumentOperations? ModuleFor(string path) => null;

        public bool IsTemplate(string path) => true;

        public IPrintableDocument? FirstPages(TemplateEntry entry) => null;
    }

    private (Writer Writer, Answers Dialogs, CommandDefinition Save) Build(params object?[] replies)
    {
        var workspace = new DocumentWorkspace();
        workspace.Open(new OpenDocument(ModuleKind.Sheets, "Budget", new Panel(), new UndoStack()));
        var writer = new Writer();
        var dialogs = new Answers(replies);
        var commands = new ShellTemplateCommands(
            workspace, new DocumentOperationsCatalog([writer]), Fake<IDocumentOpenRouter>(), Fake<PulseOffice.Platform.IFileDialogService>(), dialogs,
            Fake<IStatusBarService>(), new TextCatalog(UiLanguage.English), new Library(_folder),
            Fake<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>(), Fake<PulseOffice.UI.Controls.Printing.IPageCanvasFactory>());
        return (writer, dialogs, commands.GetCommands().Single(command => command.Id.Value == "shell.file.saveAsTemplate"));
    }

    [Fact]
    public Task An_existing_template_is_kept_when_the_replacement_is_declined() => headless.Session.Dispatch(async () =>
    {
        var existing = Path.Combine(_folder, "Budget.ots");
        await File.WriteAllTextAsync(existing, "old");
        var (writer, dialogs, save) = Build(new Dictionary<string, object> { ["name"] = "Budget" }, false);

        await save.Execute(CommandContext.Empty, CancellationToken.None);

        Assert.Empty(writer.Written);
        Assert.Equal("old", await File.ReadAllTextAsync(existing));
        Assert.IsType<ChoicePromptView>(dialogs.Shown[1]);
    }, CancellationToken.None);

    [Fact]
    public Task An_existing_template_is_replaced_only_after_confirmation() => headless.Session.Dispatch(async () =>
    {
        var existing = Path.Combine(_folder, "Budget.ots");
        await File.WriteAllTextAsync(existing, "old");
        var (writer, _, save) = Build(new Dictionary<string, object> { ["name"] = "Budget" }, true);

        await save.Execute(CommandContext.Empty, CancellationToken.None);

        Assert.Equal([existing], writer.Written);
    }, CancellationToken.None);

    [Fact]
    public Task A_new_template_name_is_written_without_asking() => headless.Session.Dispatch(async () =>
    {
        var (writer, dialogs, save) = Build(new Dictionary<string, object> { ["name"] = "Nuovo" });

        await save.Execute(CommandContext.Empty, CancellationToken.None);

        Assert.Equal([Path.Combine(_folder, "Nuovo.ots")], writer.Written);
        Assert.Single(dialogs.Shown);
    }, CancellationToken.None);
}
