using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ShellScannerCommandsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-scan-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class Folders(string root) : ITemporaryFiles
    {
        public string NewFolder(string prefix) => Directory.CreateTempSubdirectory(Path.Combine(root, prefix)[(root.Length + 1)..]).FullName;
    }

    private sealed class Scanner(Action during) : IScannerService, IScannerServiceProvider
    {
        public IScannerService Current => this;

        public string DisplayName => "prova";

        public bool IsAvailable => true;

        public Task<OperationResult<IReadOnlyList<ScannerDevice>>> DevicesAsync(CancellationToken ct = default) =>
            Task.FromResult(OperationResult<IReadOnlyList<ScannerDevice>>.Ok([]));

        public async Task<OperationResult<string>> ScanAsync(ScanRequest request, CancellationToken ct = default)
        {
            during();
            await File.WriteAllBytesAsync(request.TargetPath, [1, 2, 3], ct);
            return OperationResult<string>.Ok(request.TargetPath);
        }
    }

    private sealed class NoSettings : ISettingsService
    {
        public IReadOnlyCollection<SettingDefinition> Definitions => [];

        public SettingDefinition? Find(string key) => null;

        public T Get<T>(string key) => default!;

        public SettingScope EffectiveScope(string key) => SettingScope.User;

        public bool IsLocked(string key) => false;

        public void Set(string key, object? value, SettingScope scope = SettingScope.User) { }

        public void Reset(string key, SettingScope scope = SettingScope.User) { }

        public IEnumerable<SettingDefinition> Search(string query) => [];

        public event EventHandler<SettingChangedEventArgs>? Changed { add { } remove { } }
    }

    private sealed class Operations : IDocumentOperations
    {
        public List<OpenDocument> Inserted { get; } = [];

        public ModuleKind Module => ModuleKind.Documents;

        public string SuggestedName(OpenDocument document) => document.Title;

        public IPrintableDocument? BuildPrintable(OpenDocument document) => null;

        public PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document) => null;

        public bool InsertImage(OpenDocument document, byte[] data, string name)
        {
            Inserted.Add(document);
            return true;
        }

        public string NativeExtension => "odt";

        public string OfficeExtension => "docx";

        public bool WriteTo(OpenDocument document, string path) => false;

        public string? RenderHtml(OpenDocument document) => null;

        public PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document) => null;

        public string TemplateExtension => "ott";

        public string? OfficeTemplateExtension => null;
    }

    private static OpenDocument Document(string title) =>
        new(ModuleKind.Documents, title, new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());

    [Fact]
    public async Task L_immagine_scansionata_va_nel_documento_da_cui_si_e_chiesta_la_scansione()
    {
        var workspace = new DocumentWorkspace();
        var first = Document("primo");
        var second = Document("secondo");
        workspace.Open(first);
        workspace.Open(second);
        workspace.Activate(first);
        var operations = new Operations();
        var status = new StatusBarService(TimeProvider.System, new NotificationCenter(TimeProvider.System), new NotificationHistory(TimeProvider.System));
        var commands = new ShellScannerCommands(workspace, new DocumentOperationsCatalog([operations]), new Scanner(() => workspace.Activate(second)),
            new NoSettings(), null!, status, new TextCatalog(UiLanguage.Italian), null!, new Folders(_folder));

        await commands.GetCommands().Single(command => command.Id.Value == "shell.scan.request").Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.Equal([first], operations.Inserted);
    }
}
