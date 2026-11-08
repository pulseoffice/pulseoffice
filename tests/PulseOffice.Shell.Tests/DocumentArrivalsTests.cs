using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class DocumentArrivalsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulseoffice-arrivals-" + Guid.NewGuid().ToString("N"));

    public DocumentArrivalsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class Router : IDocumentOpenRouter
    {
        public List<string> Opened { get; } = [];

        public bool CanOpen(string path) => path.EndsWith(".odt", StringComparison.Ordinal);

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            Opened.Add(path);
            return Task.CompletedTask;
        }

        public Task OpenDialogAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default) => Task.FromResult(false);

        public Task EditAsync(string path, CancellationToken ct = default) => OpenAsync(path, ct);

        public OpenDocument? FindOpen(string path) => null;
    }

    [Fact]
    public async Task SiAprono_i_documenti_passati_all_avvio_e_si_ignorano_opzioni_e_file_mancanti()
    {
        var document = Path.Combine(_folder, "lettera.odt");
        var other = Path.Combine(_folder, "nota.txt");
        await File.WriteAllTextAsync(document, "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(other, "x", TestContext.Current.CancellationToken);
        var router = new Router();
        var arrivals = new DocumentArrivals(router, NullLogger<DocumentArrivals>.Instance);

        await arrivals.OpenAsync(["-psn_0_12345", document, Path.Combine(_folder, "manca.odt"), other, null, document], TestContext.Current.CancellationToken);

        Assert.Equal([Path.GetFullPath(document)], router.Opened);
    }
}

public sealed class DocumentArrivalsStartupTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-startup-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class Router : IDocumentOpenRouter
    {
        public List<string> Opened { get; } = [];

        public bool CanOpen(string path) => true;

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            Opened.Add(path);
            return Task.CompletedTask;
        }

        public Task OpenDialogAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default) => Task.FromResult(true);

        public Task EditAsync(string path, CancellationToken ct = default) => OpenAsync(path, ct);

        public OpenDocument? FindOpen(string path) => null;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Un_file_passato_all_avvio_e_consegnato_anche_dal_sistema_si_apre_una_volta_sola(bool systemFirst)
    {
        var template = Path.Combine(_folder, "modello.ots");
        await File.WriteAllTextAsync(template, "x", TestContext.Current.CancellationToken);
        var router = new Router();
        var arrivals = new DocumentArrivals(router, NullLogger<DocumentArrivals>.Instance);
        arrivals.ExpectStartup([template]);

        if (systemFirst) await arrivals.OpenAsync([template], TestContext.Current.CancellationToken);
        await arrivals.OpenStartupAsync(TestContext.Current.CancellationToken);
        if (!systemFirst) await arrivals.OpenAsync([template], TestContext.Current.CancellationToken);

        Assert.Single(router.Opened);
        await arrivals.OpenAsync([template], TestContext.Current.CancellationToken);
        Assert.Equal(2, router.Opened.Count);
    }
}

public sealed class DocumentOpenRouterTests
{
    private sealed class Opener : IWorkspaceOpener
    {
        public int Opened { get; private set; }

        public int OpenedAsNew { get; private set; }

        public IReadOnlyList<PulseOffice.Platform.FileFilter> ReadableFilters => [];

        public bool Handles(string path) => true;

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            Opened++;
            return Task.CompletedTask;
        }

        public Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default)
        {
            OpenedAsNew++;
            return Task.FromResult(true);
        }
    }

    private sealed class Templates : ITemplateLibrary
    {
        public string Folder => string.Empty;

        public IReadOnlyList<string> Extensions => [".ott"];

        public IReadOnlyList<TemplateEntry> Entries() => [];

        public IDocumentOperations? ModuleFor(string path) => null;

        public bool IsTemplate(string path) => path.EndsWith(".ott", StringComparison.Ordinal);

        public PulseOffice.Printing.IPrintableDocument? FirstPages(TemplateEntry entry) => null;
    }

    [Fact]
    public async Task Un_modello_aperto_dal_file_crea_un_documento_nuovo_e_si_modifica_solo_su_richiesta()
    {
        var workspace = new DocumentWorkspace();
        var opener = new Opener();
        var router = new DocumentOpenRouter([opener], new NoDialogs(), new FakeTextCatalog(), workspace, new Templates(), new PulseOffice.Platform.RuntimeHostSystem());

        await router.OpenAsync("/modelli/lettera.ott", TestContext.Current.CancellationToken);
        Assert.Equal((0, 1), (opener.Opened, opener.OpenedAsNew));

        await router.EditAsync("/modelli/lettera.ott", TestContext.Current.CancellationToken);
        Assert.Equal((1, 1), (opener.Opened, opener.OpenedAsNew));

        await router.OpenAsync("/documenti/lettera.odt", TestContext.Current.CancellationToken);
        Assert.Equal((2, 1), (opener.Opened, opener.OpenedAsNew));
    }

    private sealed class NoDialogs : PulseOffice.Platform.IFileDialogService
    {
        public Task<PulseOffice.Platform.ChosenFile?> OpenFileAsync(string title, IReadOnlyList<PulseOffice.Platform.FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<PulseOffice.Platform.ChosenFile?>(null);

        public Task<PulseOffice.Platform.ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<PulseOffice.Platform.FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<PulseOffice.Platform.ChosenFile?>(null);
    }

    [Fact]
    public async Task Riaprire_un_documento_gia_aperto_porta_alla_sua_scheda_invece_di_duplicarlo()
    {
        var workspace = new DocumentWorkspace();
        var path = Path.Combine(Path.GetTempPath(), "lettera.odt");
        var first = new OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Documents, "lettera", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        first.RestoreLocation(path);
        var second = new OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Documents, "altro", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        workspace.Open(first);
        workspace.Open(second);
        var opener = new Opener();
        var router = new DocumentOpenRouter([opener], new NoDialogs(), new FakeTextCatalog(), workspace, new Templates(), new PulseOffice.Platform.RuntimeHostSystem());

        await router.OpenAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(0, opener.Opened);
        Assert.Same(first, workspace.Active);
    }

    private sealed class SlowOpener : IWorkspaceOpener
    {
        public TaskCompletionSource Release { get; } = new();

        public int Opened { get; private set; }

        public IReadOnlyList<PulseOffice.Platform.FileFilter> ReadableFilters => [];

        public bool Handles(string path) => true;

        public async Task OpenAsync(string path, CancellationToken ct = default)
        {
            Opened++;
            await Release.Task;
        }

        public Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task Due_aperture_dello_stesso_file_mentre_la_prima_e_in_corso_ne_aprono_uno_solo()
    {
        var opener = new SlowOpener();
        var router = new DocumentOpenRouter([opener], new NoDialogs(), new FakeTextCatalog(), new DocumentWorkspace(), new Templates(), new PulseOffice.Platform.RuntimeHostSystem());
        var path = Path.Combine(Path.GetTempPath(), "lettera.odt");

        var first = router.OpenAsync(path, TestContext.Current.CancellationToken);
        var second = router.OpenAsync(path, TestContext.Current.CancellationToken);
        opener.Release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, opener.Opened);
    }

    private sealed class BrokenFirstRouter : IDocumentOpenRouter
    {
        public List<string> Opened { get; } = [];

        public bool CanOpen(string path) => true;

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            if (Opened.Count == 0 && path.EndsWith("rotto.odt", StringComparison.Ordinal)) throw new InvalidDataException("rotto");
            Opened.Add(path);
            return Task.CompletedTask;
        }

        public Task OpenDialogAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default) => Task.FromResult(true);

        public Task EditAsync(string path, CancellationToken ct = default) => OpenAsync(path, ct);

        public OpenDocument? FindOpen(string path) => null;
    }

    [Fact]
    public async Task Un_file_rotto_fra_quelli_arrivati_non_impedisce_di_aprire_gli_altri()
    {
        var folder = Directory.CreateTempSubdirectory("pulseoffice-broken-").FullName;
        try
        {
            var broken = Path.Combine(folder, "rotto.odt");
            var good = Path.Combine(folder, "buono.odt");
            await File.WriteAllTextAsync(broken, "x", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(good, "x", TestContext.Current.CancellationToken);
            var router = new BrokenFirstRouter();
            var arrivals = new DocumentArrivals(router, Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentArrivals>.Instance);

            await arrivals.OpenAsync([broken, good], TestContext.Current.CancellationToken);

            Assert.Equal([Path.GetFullPath(good)], router.Opened);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
