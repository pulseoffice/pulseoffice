using System.Text;
using PulseOffice.Formats;
using PulseOffice.Formats.Intake;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Shell;
using PulseOffice.Typography.Embedding;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class ModuleDocumentServiceTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-servizio-").FullName;
    private readonly Recents _recents = new();
    private readonly Dialogs _dialogs = new();
    private readonly DocumentWorkspace _workspace = new();
    private readonly NotificationCenter _notifications = new(TimeProvider.System);
    private readonly NotificationHistory _history = new(TimeProvider.System);
    private readonly InterfaceLock _lock = new();
    private readonly NoPasswords _passwords = new();

    public void Dispose()
    {
        _notifications.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private sealed class Probe
    {
        public string Text { get; set; } = string.Empty;

        public string? OpenedFrom { get; set; }

        public bool Loaded { get; set; }

        public IInterfaceLock? Watched { get; set; }

        public bool LockedWhileWriting { get; set; }

        public int WritingThread { get; set; }

        public IReadOnlyList<Diagnostic> Losses { get; set; } = [];
    }

    private sealed class Recents : IRecentDocuments
    {
        public List<string> Remembered { get; } = [];

        public event EventHandler? Changed { add { } remove { } }

        public IReadOnlyList<RecentDocument> Items => [];

        public void Remember(string path, string name) => Remembered.Add(path);

        public void Forget(string path) { }

        public void Clear() { }
    }

    private sealed class Dialogs : IFileDialogService
    {
        public string? Suggested { get; private set; }

        public IReadOnlyList<FileFilter> Filters { get; private set; } = [];

        public string? Target { get; set; }

        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) => Task.FromResult<ChosenFile?>(null);

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default)
        {
            Suggested = suggestedName;
            Filters = filters;
            if (Target is not { } target) return Task.FromResult<ChosenFile?>(null);
            return Task.FromResult<ChosenFile?>(new ChosenFile(target, Path.GetFileName(target))
            {
                OpenRead = _ => Task.FromResult<Stream>(File.OpenRead(target)),
                OpenWrite = _ => Task.FromResult<Stream>(File.Create(target)),
            });
        }
    }

    private sealed class NoFonts : IDocumentFontEmbedder
    {
        public Task<FontEmbeddingReport> PrepareAsync(object model, CancellationToken ct = default) => Task.FromResult(FontEmbeddingReport.Nothing);
    }

    private sealed class Unsigned : ISignedSaveGuard
    {
        public Task<bool> ConfirmOverwriteAsync(string path, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class NoPasswords : IDocumentPasswordGate
    {
        public Action? DuringCommit { get; set; }

        public Func<MemoryStream, (UnlockOutcome, UnlockedDocument?)>? Locked { get; set; }

        public int Asked { get; private set; }

        public bool IsProtected(string path) => false;

        public Task<(UnlockOutcome Outcome, UnlockedDocument? Document)> UnlockAsync(MemoryStream buffer, string documentName, CancellationToken ct)
        {
            Asked++;
            return Task.FromResult<(UnlockOutcome, UnlockedDocument?)>(Locked?.Invoke(buffer)
                ?? (UnlockOutcome.Unlocked, new UnlockedDocument(new MemoryStream(buffer.ToArray()), null)));
        }

        public Task<ProtectionChoice?> ChooseProtectionAsync(string documentName, CancellationToken ct) => Task.FromResult<ProtectionChoice?>(null);

        public bool CanProtect(MemoryStream package) => false;

        public MemoryStream Protect(MemoryStream package, PulseOffice.Formats.Packaging.Encryption.OdfProtection protection) => package;

        public async Task<bool> CommitAsync(MemoryStream package, PulseOffice.Formats.Packaging.Encryption.OdfProtection? protection, Func<MemoryStream, Task> commit)
        {
            package.Position = 0;
            DuringCommit?.Invoke();
            await commit(package).ConfigureAwait(false);
            return true;
        }
    }

    private sealed class ProbeFormats : IModuleFormats<Probe>, IModuleWriter<Probe>
    {
        public IReadOnlyList<FileFilter> ReadableFilters => [];

        public IReadOnlyList<FileFilter> WritableFilters => [new FileFilter("ODS", [".ods"]), new FileFilter("CSV", [".csv"]), new FileFilter("XLSX", [".xlsx"])];

        public string NativeExtension => ".ods";

        public string InterchangeExtension => ".xlsx";

        public bool CanRead(string path) => path.EndsWith(".ods", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ots", StringComparison.OrdinalIgnoreCase);

        public IModuleWriter<Probe>? WriterFor(string path) => CanRead(path) ? this : null;

        public IModuleWriter<Probe>? NativeWriter => this;

        public OperationResult<Probe> Read(string path, Stream stream) =>
            OperationResult<Probe>.Ok(new Probe { Text = new StreamReader(stream, Encoding.UTF8).ReadToEnd() });

        public FidelityReport CheckFidelity(Probe model) => new(model.Losses);

        public OperationResult<bool> Write(Probe model, Stream stream)
        {
            model.LockedWhileWriting = model.Watched?.IsLocked == true;
            model.WritingThread = Environment.CurrentManagedThreadId;
            stream.Write(Encoding.UTF8.GetBytes(model.Text));
            return OperationResult<bool>.Ok(true);
        }
    }

    private sealed class ProbeService(DocumentServiceContext context) : ModuleDocumentService<Probe>(new ProbeFormats(), context)
    {
        public override ModuleKind Module => ModuleKind.Sheets;

        protected override OpenDocument CreateDocument(Probe model, string name) =>
            new(ModuleKind.Sheets, name, new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack()) { Model = model };

        protected override void Stamp(Probe model, string fileName) { }

        protected override void Opened(Probe model, string path) => model.OpenedFrom = path;

        protected override void Loaded(Probe model) => model.Loaded = true;
    }

    private ProbeService Service(long maximumBytes = 1L << 30)
    {
        var texts = new FakeTextCatalog();
        texts.Add("status.saved.losses", "{0} salvato — {1} funzionalità non conservate dal formato: {2}");
        var status = new StatusBarService(TimeProvider.System, _notifications, _history);
        return new ProbeService(new DocumentServiceContext(
            _dialogs, new AtomicFileWriter(), new DocumentBuffer(new IncomingDocumentLimits { MaximumFileBytes = maximumBytes }),
            _workspace, status, texts, new DiagnosticTexts(texts), _recents, new Unsigned(), new NoFonts(), _passwords, _lock, new PulseOffice.Foundation.Identity.SystemUserIdentity()));
    }

    private OpenDocument Untitled(string title) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack()) { Model = new Probe { Text = "dati" } };

    [Fact]
    public async Task Opening_an_encrypted_template_as_new_goes_through_the_password_gate()
    {
        var template = Path.Combine(_folder, "cifrato.ots");
        await File.WriteAllTextAsync(template, "cifrato", TestContext.Current.CancellationToken);
        _passwords.Locked = _ => (UnlockOutcome.Unlocked, new UnlockedDocument(new MemoryStream(Encoding.UTF8.GetBytes("in chiaro")), null));

        var opened = await Service().OpenAsNewAsync(template, TestContext.Current.CancellationToken);

        Assert.True(opened);
        Assert.Equal(1, _passwords.Asked);
        Assert.Equal("in chiaro", ((Probe)Assert.Single(_workspace.Documents).Model!).Text);
    }

    [Fact]
    public async Task Cancelling_the_password_of_a_template_opens_nothing()
    {
        var template = Path.Combine(_folder, "cifrato.ots");
        await File.WriteAllTextAsync(template, "cifrato", TestContext.Current.CancellationToken);
        _passwords.Locked = _ => (UnlockOutcome.Cancelled, null);

        var opened = await Service().OpenAsNewAsync(template, TestContext.Current.CancellationToken);

        Assert.False(opened);
        Assert.Empty(_workspace.Documents);
    }

    [Fact]
    public async Task Il_salvataggio_scrive_fuori_dal_thread_chiamante_con_l_interfaccia_bloccata()
    {
        _dialogs.Target = Path.Combine(_folder, "grande.ods");
        var document = Untitled("grande");
        var probe = (Probe)document.Model!;
        probe.Watched = _lock;
        await Service().SaveAsAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(probe.LockedWhileWriting);
        Assert.False(_lock.IsLocked);
    }

    [Fact]
    public async Task Salva_una_copia_scrive_il_file_e_lascia_il_documento_modificato_sul_suo_percorso()
    {
        var original = Path.Combine(_folder, "originale.ods");
        _dialogs.Target = Path.Combine(_folder, "copia.ods");
        var document = Untitled("originale");
        document.RestoreLocation(original);
        document.MarkModified();

        var written = await Service().SaveCopyAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(written);
        Assert.Equal("dati", await File.ReadAllTextAsync(_dialogs.Target, TestContext.Current.CancellationToken));
        Assert.Equal(original, document.Path);
        Assert.True(document.IsModified);
    }

    [Fact]
    public async Task Salva_con_nome_ricorda_il_file_fra_i_recenti()
    {
        _dialogs.Target = Path.Combine(_folder, "conti.ods");
        var document = Untitled("conti");
        await Service().SaveAsAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal("dati", await File.ReadAllTextAsync(_dialogs.Target, TestContext.Current.CancellationToken));
        Assert.Contains(_dialogs.Target, _recents.Remembered);
        Assert.Equal(_dialogs.Target, document.Path);
    }

    [Fact]
    public async Task Salva_in_formato_Office_propone_il_nome_e_il_filtro_OOXML()
    {
        var target = Path.Combine(_folder, "conti.xlsx");
        _dialogs.Target = target;
        var document = Untitled("conti");
        await Service().SaveInterchangeAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal("conti.xlsx", _dialogs.Suggested);
        Assert.Equal("XLSX", _dialogs.Filters[0].Name);
        Assert.Equal(target, document.Path);
        Assert.True(File.Exists(target));

        await Service().SaveAsAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal("ODS", _dialogs.Filters[0].Name);
    }

    [Fact]
    public async Task Salva_con_nome_non_raddoppia_l_estensione()
    {
        await Service().SaveAsAsync(Untitled("conti.ods"), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal("conti.ods", _dialogs.Suggested);
    }

    [Fact]
    public async Task La_rilettura_rispetta_il_limite_dei_documenti_in_ingresso()
    {
        var path = Path.Combine(_folder, "grande.ods");
        await File.WriteAllTextAsync(path, new string('x', 4096), TestContext.Current.CancellationToken);
        Assert.Null(await Service(maximumBytes: 1024).ReadAsync(path, TestContext.Current.CancellationToken));
        Assert.NotNull(await Service().ReadAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Un_file_illeggibile_non_fa_cadere_la_rilettura()
    {
        Assert.Null(await Service().ReadAsync(Path.Combine(_folder, "manca.ods"), TestContext.Current.CancellationToken));
        Assert.False(await Service().OpenAsNewAsync(Path.Combine(_folder, "manca.ots"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Un_modello_si_apre_come_documento_nuovo_con_gli_stessi_passi_dell_apertura()
    {
        var path = Path.Combine(_folder, "fattura.ots");
        await File.WriteAllTextAsync(path, "modello", TestContext.Current.CancellationToken);
        Assert.True(await Service().OpenAsNewAsync(path, TestContext.Current.CancellationToken));
        var document = Assert.Single(_workspace.Documents);
        var probe = Assert.IsType<Probe>(document.Model);
        Assert.Equal("fattura", document.Title);
        Assert.Null(document.Path);
        Assert.Equal(path, probe.OpenedFrom);
        Assert.True(probe.Loaded);
        Assert.Empty(_recents.Remembered);
    }

    [Fact]
    public async Task Il_salvataggio_con_perdite_dice_quali_funzionalita_si_perdono()
    {
        _dialogs.Target = Path.Combine(_folder, "relazione.ods");
        var document = Untitled("relazione");
        ((Probe)document.Model!).Losses = [Diagnostic.Warning("DOCX001", "Commenti risolti non conservati")];

        await Service().SaveAsAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Contains(_history.Entries, entry => entry.Notification.Message.Contains("Commenti risolti non conservati", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Le_perdite_elencate_non_raddoppiano_la_punteggiatura()
    {
        _dialogs.Target = Path.Combine(_folder, "bilancio.xlsx");
        var document = Untitled("bilancio");
        ((Probe)document.Model!).Losses =
        [
            Diagnostic.Warning("OOX1", "La pivot si perde."),
            Diagnostic.Warning("OOX2", "Il commento non porta la data."),
        ];

        await Service().SaveAsAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Contains(_history.Entries, entry => entry.Notification.Message.EndsWith("La pivot si perde; Il commento non porta la data.", StringComparison.Ordinal));
    }

    private sealed class Edit : PulseOffice.Document.Commands.IUndoableAction
    {
        public string Description => "edit";

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(PulseOffice.Document.Commands.IUndoableAction previous) => false;
    }

    [Fact]
    public async Task An_edit_made_while_the_file_is_being_written_stays_unsaved()
    {
        _dialogs.Target = Path.Combine(_folder, "corsa.ods");
        var document = Untitled("corsa");
        _passwords.DuringCommit = () => document.History.Push(new Edit());

        await Service().SaveAsAsync(document, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(_dialogs.Target, document.Path);
        Assert.True(document.IsModified);
    }
}
