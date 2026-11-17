using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CommandSweepShard1(HeadlessDesktopSession headless) : CommandSweepTests(headless)
{
    [Theory]
    [MemberData(nameof(Slices), 0)]
    public Task Ogni_comando_abilitato_fa_qualcosa_o_rifiuta_con_un_motivo(string module, string context, int part) => Sweep(module, context, part);
}

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CommandSweepShard2(HeadlessDesktopSession headless) : CommandSweepTests(headless)
{
    [Theory]
    [MemberData(nameof(Slices), 1)]
    public Task Ogni_comando_abilitato_fa_qualcosa_o_rifiuta_con_un_motivo(string module, string context, int part) => Sweep(module, context, part);
}

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CommandSweepShard3(HeadlessDesktopSession headless) : CommandSweepTests(headless)
{
    [Theory]
    [MemberData(nameof(Slices), 2)]
    public Task Ogni_comando_abilitato_fa_qualcosa_o_rifiuta_con_un_motivo(string module, string context, int part) => Sweep(module, context, part);
}

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CommandSweepShard4(HeadlessDesktopSession headless) : CommandSweepTests(headless)
{
    [Theory]
    [MemberData(nameof(Slices), 3)]
    public Task Ogni_comando_abilitato_fa_qualcosa_o_rifiuta_con_un_motivo(string module, string context, int part) => Sweep(module, context, part);
}

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CommandSweepShard5(HeadlessDesktopSession headless) : CommandSweepTests(headless)
{
    [Theory]
    [MemberData(nameof(Slices), 4)]
    public Task Ogni_comando_abilitato_fa_qualcosa_o_rifiuta_con_un_motivo(string module, string context, int part) => Sweep(module, context, part);
}

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CommandSweepShard6(HeadlessDesktopSession headless) : CommandSweepTests(headless)
{
    [Theory]
    [MemberData(nameof(Slices), 5)]
    public Task Ogni_comando_abilitato_fa_qualcosa_o_rifiuta_con_un_motivo(string module, string context, int part) => Sweep(module, context, part);
}

public abstract class CommandSweepTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-sweep-" + Guid.NewGuid().ToString("N"));

    protected CommandSweepTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class NoFileDialogs : IFileDialogService
    {
        public int Asked { get; private set; }

        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) => Cancel();

        public Task<ChosenFile?> OpenFileInAsync(string title, IReadOnlyList<FileFilter> filters, string startFolder, CancellationToken ct = default) => Cancel();

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) => Cancel();

        public Task<IReadOnlyList<ChosenFile>> OpenFilesAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default)
        {
            Asked++;
            return Task.FromResult<IReadOnlyList<ChosenFile>>([]);
        }

        private Task<ChosenFile?> Cancel()
        {
            Asked++;
            return Task.FromResult<ChosenFile?>(null);
        }
    }

    private sealed class NoPrinters : IPrintServiceProvider, IPrintService
    {
        public IPrintService Current => this;

        public PrintCapabilities Capabilities => new(false, false, false, false, false);

        public Task<IReadOnlyList<Printer>> PrintersAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Printer>>([]);

        public Task<OperationResult<bool>> PrintAsync(PrintRequest request, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<bool>.Fail(Diagnostic.Error("TEST001", "No printer in the sweep.")));
    }

    private sealed class NoShell : IShellOpener
    {
        public OperationResult<bool> OpenUrl(string url) => OperationResult<bool>.Fail(Diagnostic.Error("TEST002", "No browser in the sweep."));

        public OperationResult<bool> OpenFile(string path) => OperationResult<bool>.Fail(Diagnostic.Error("TEST002", "No viewer in the sweep."));
    }

    private sealed class NoMail : IMailComposerProvider, IFileSharingProvider
    {
        IMailComposer IMailComposerProvider.Current => new UnavailableMailComposer();

        IFileSharing IFileSharingProvider.Current => new UnavailableFileSharing();
    }

    private sealed class NoScanner : IScannerServiceProvider
    {
        public IScannerService Current => new UnavailableScannerService("sweep");
    }

    private sealed class NoVault : ISecretVaultProvider
    {
        public ISecretVault Current { get; } = new SessionSecretVault();
    }

    private sealed class NoQuit : IQuitTarget
    {
        public void Quit()
        {
        }
    }

    private readonly NoFileDialogs _fileDialogs = new();

    private sealed class NoUpdates : PulseOffice.Updates.IUpdateService
    {
        public bool AutomaticCheckIsDue => false;

        public Uri ReleasesPage { get; } = new("https://example.invalid/releases");

        public Task<PulseOffice.Updates.UpdateCheck> CheckAsync(CancellationToken ct) =>
            Task.FromResult(new PulseOffice.Updates.UpdateCheck(PulseOffice.Updates.UpdateCheckKind.UpToDate));

        public bool IsSkipped(PulseOffice.Updates.ReleaseVersion version) => false;

        public void Skip(PulseOffice.Updates.ReleaseVersion version)
        {
        }

        public Task<PulseOffice.Updates.VerifiedPackage> DownloadAsync(PulseOffice.Updates.UpdateCheck check, IProgress<double>? progress, CancellationToken ct) =>
            throw new InvalidOperationException("The sweep never downloads updates.");

        public Task InstallAfterExitAsync(PulseOffice.Updates.VerifiedPackage package, CancellationToken ct) =>
            throw new InvalidOperationException("The sweep never installs updates.");
    }

    private ServiceCollection Register()
    {
        var services = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable());
        services.AddSingleton<IFileDialogService>(_fileDialogs);
        services.AddSingleton<IPrintServiceProvider>(new NoPrinters());
        services.AddSingleton<IShellOpener>(new NoShell());
        var mail = new NoMail();
        services.AddSingleton<IMailComposerProvider>(mail);
        services.AddSingleton<IFileSharingProvider>(mail);
        services.AddSingleton<IScannerServiceProvider>(new NoScanner());
        services.AddSingleton<ISecretVaultProvider>(new NoVault());
        services.AddSingleton<IQuitTarget>(new NoQuit());
        services.AddSingleton<PulseOffice.Updates.IUpdateService>(new NoUpdates());
        return services;
    }

    public sealed record SweepRow(string Command, string Outcome, string Effect, string Detail);

    private static readonly IReadOnlySet<string> Unsafe = new HashSet<string>(StringComparer.Ordinal)
    {
        "shell.file.quit",
    };

    private const int Parts = 4;

    private static readonly IReadOnlySet<string> VolatileParts = new HashSet<string>(StringComparer.Ordinal) { "settings.xml" };

    private static readonly System.Text.RegularExpressions.Regex VolatileMeta = new(
        "<(meta:creation-date|dc:date|meta:editing-duration|meta:print-date)>[^<]*</\\1>", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string Diff(string expected, string actual)
    {
        var start = 0;
        while (start < expected.Length && start < actual.Length && expected[start] == actual[start]) start++;
        var from = Math.Max(0, start - 60);
        string Around(string text) => text.Length <= from ? string.Empty : text.Substring(from, Math.Min(200, text.Length - from));
        return $"at {start}: expected «{Around(expected)}» but was «{Around(actual)}»";
    }

    private static readonly CommandId RepeatCommand = new("shell.edit.repeat");

    private const int LargeParts = 12;

    private const int Shards = 6;

    private static readonly IReadOnlyDictionary<string, string> AlreadyInThatState = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ledger.view.zoomReset"] = "the zoom is already 100%",
        ["ledger.view.zoom100"] = "the zoom is already 100%",
        ["ledger.view.normal"] = "the view is already normal",
        ["ledger.data.sortAscending"] = "the selection is already in ascending order",
        ["ledger.format.optimalRowHeight"] = "the rows already have their optimal height",
        ["ledger.clipboard.pasteStyles"] = "plain text carries no formatting, as in other office suites",
        ["ledger.zoom.pageWidth"] = "the headless window already shows the page width",
        ["deck.zoom.width"] = "the headless window shows the slide at the same scale for fit and width",
        ["folio.edit.goToPage"] = "focuses the page field of the status bar, which the headless window does not activate",
        ["ledger.select.dataArea"] = "the selection already is the data area",
        ["ledger.text.katakana"] = "Latin text has no kana to convert, as in other office suites",
        ["ledger.text.hiragana"] = "Latin text has no kana to convert, as in other office suites",
        ["ledger.text.halfWidth"] = "the text is already half width",
        ["folio.view.singlePage"] = "the view already shows one page at a time",
        ["folio.view.printLayout"] = "the view is already the print layout",
        ["folio.view.selectionStandard"] = "the selection mode is already standard",
        ["deck.spelling.hyphenation"] = "a short word has no hyphenation points",
        ["folio.object.alignTop"] = "a new frame already sits at the top of its anchor",
        ["folio.object.wrap.parallel"] = "a new frame already wraps in parallel",
        ["folio.object.anchor.character"] = "a new frame is already anchored to the character",
        ["folio.page.portrait"] = "the page is already portrait",
        ["ledger.draw.polygon"] = "arms the tool for the next drag on the grid",
        ["ledger.draw.polygonFilled"] = "arms the tool for the next drag on the grid",
        ["ledger.draw.polygonConstrained"] = "arms the tool for the next drag on the grid",
        ["ledger.draw.polygonConstrainedFilled"] = "arms the tool for the next drag on the grid",
        ["ledger.draw.curve"] = "arms the tool for the next drag on the grid",
        ["ledger.draw.curveFilled"] = "arms the tool for the next drag on the grid",
        ["ledger.draw.freehand"] = "arms the tool for the next drag on the grid",
        ["ledger.draw.freehandFilled"] = "arms the tool for the next drag on the grid",
        ["folio.case.katakana"] = "Latin text has no kana to convert, as in other office suites",
        ["folio.case.hiragana"] = "Latin text has no kana to convert, as in other office suites",
        ["folio.case.halfWidth"] = "the text is already half width",
        ["folio.case.sentence"] = "the text already starts with a capital and goes on in lower case",
        ["folio.format.clear"] = "the text carries no direct formatting",
        ["folio.charStyle.none"] = "the text has no character style",
    };

    public sealed record SweepStage(ServiceProvider Provider, ShellWindow Window, IDocumentWorkspace Workspace)
    {
        public T Service<T>() where T : notnull => Provider.GetRequiredService<T>();

        public void Type(string text)
        {
            Window.KeyTextInput(text);
            Dispatcher.UIThread.RunJobs();
        }
    }

    public interface ISweepScenario
    {
        string Name { get; }

        IReadOnlySet<string> Modules { get; }

        int Parts { get; }

        Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry);

        bool Covers(CommandDefinition command, bool enabledInTypicalContext) =>
            command.Module != ModuleKind.Shell && !enabledInTypicalContext;
    }

    private interface IRefusalScenario : ISweepScenario
    {
        bool ISweepScenario.Covers(CommandDefinition command, bool enabledInTypicalContext) => command.Module != ModuleKind.Shell;
    }

    private sealed class TypicalScenario : ISweepScenario
    {
        public string Name => "typical";

        public IReadOnlySet<string> Modules { get; } = new HashSet<string> { "sheets", "documents", "presentations" };

        public int Parts => CommandSweepTests.LargeParts;

        public Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry) => Task.CompletedTask;

        public bool Covers(CommandDefinition command, bool enabledInTypicalContext) => true;
    }

    private sealed class ReadOnlyScenario : IRefusalScenario
    {
        public string Name => "readOnly";

        public IReadOnlySet<string> Modules { get; } = new HashSet<string> { "sheets", "documents", "presentations" };

        public int Parts => CommandSweepTests.Parts;

        public Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry)
        {
            stage.Workspace.Active!.IsReadOnly = true;
            Dispatcher.UIThread.RunJobs();
            return Task.CompletedTask;
        }
    }

    private sealed class ProtectedSheetScenario : IRefusalScenario
    {
        public string Name => "protectedSheet";

        public IReadOnlySet<string> Modules { get; } = new HashSet<string> { "sheets" };

        public int Parts => CommandSweepTests.LargeParts;

        public Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry)
        {
            var sheet = stage.Service<PulseOffice.Sheets.UI.IActiveSheetContext>();
            var worksheet = sheet.Workbook!.Sheets[sheet.SheetIndex];
            worksheet.Protection = worksheet.Protection with { IsEnabled = true };
            sheet.RequestRefresh();
            Dispatcher.UIThread.RunJobs();
            return Task.CompletedTask;
        }
    }

    private sealed class CellEditScenario : ISweepScenario
    {
        public string Name => "cellEdit";

        public IReadOnlySet<string> Modules { get; } = new HashSet<string> { "sheets" };

        public int Parts => CommandSweepTests.Parts;

        public Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry)
        {
            stage.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            stage.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            stage.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            stage.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            stage.Type("in modifica");
            return Task.CompletedTask;
        }
    }

    private sealed class SelectedObjectScenario(bool typing) : ISweepScenario
    {
        public string Name => typing ? "shapeText" : "object";

        public IReadOnlySet<string> Modules { get; } = typing
            ? new HashSet<string> { "sheets", "presentations" }
            : new HashSet<string> { "sheets", "documents", "presentations" };

        public int Parts => CommandSweepTests.Parts;

        public Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry)
        {
            var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(5), PulseOffice.Foundation.Units.Twip.FromCentimeters(3));
            switch (stage.Workspace.Active!.Module)
            {
                case ModuleKind.Sheets:
                    stage.Service<PulseOffice.Sheets.UI.ISheetImageInsertion>().InsertObject(
                        new PulseOffice.Drawing.Shapes.ShapeObject { Id = Guid.NewGuid().ToString("N"), Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle }, size, "sweep");
                    break;
                case ModuleKind.Documents:
                    var editor = stage.Service<PulseOffice.Documents.UI.IActiveFolioContext>().Target!.Editor;
                    stage.Service<PulseOffice.Documents.UI.Editing.IFolioObjectEditing>().InsertTextFrame(editor, size);
                    break;
                default:
                    var target = stage.Service<PulseOffice.Presentations.UI.IActiveDeckContext>().Target!;
                    var slide = target.CurrentSlide()!;
                    var element = new PulseOffice.Presentations.ObjectElement
                    {
                        Id = target.Deck.NextId(),
                        Bounds = new PulseOffice.Foundation.Units.DocumentRect(PulseOffice.Foundation.Units.Twip.FromCentimeters(3), PulseOffice.Foundation.Units.Twip.FromCentimeters(3), size.Width, size.Height),
                        ZOrder = slide.Elements.Count,
                        Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = target.Deck.NextId(), Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
                    };
                    target.Actions.InsertElement(slide, element);
                    target.Select([element]);
                    break;
            }
            Dispatcher.UIThread.RunJobs();
            ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)stage.Workspace.Active!.View).FocusDocument();
            Dispatcher.UIThread.RunJobs();
            if (!typing) return Task.CompletedTask;
            stage.Type("Forma");
            var editing = stage.Workspace.Active!.Module == ModuleKind.Sheets
                ? stage.Service<PulseOffice.Sheets.UI.IActiveSheetContext>().ShapeText?.IsActive == true
                : stage.Service<PulseOffice.Presentations.UI.IActiveDeckContext>().Target!.IsTyping();
            Assert.True(editing, "typing on the selected shape did not open its text");
            return Task.CompletedTask;
        }
    }

    private sealed class ProtectedSectionScenario : IRefusalScenario
    {
        public string Name => "protectedSection";

        public IReadOnlySet<string> Modules { get; } = new HashSet<string> { "documents" };

        public int Parts => CommandSweepTests.LargeParts;

        public Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry)
        {
            var editor = stage.Service<PulseOffice.Documents.UI.IActiveFolioContext>().Target!.Editor;
            var sections = stage.Service<PulseOffice.Documents.Editing.ISectionEditing>();
            editor.Change(() => sections.Wrap(editor.Document, editor.History, editor.ParagraphRange(),
                new PulseOffice.Documents.Editing.SectionSettings("Riservata", PulseOffice.Documents.Blocks.SectionColumns.Single, true, false)));
            Dispatcher.UIThread.RunJobs();
            return Task.CompletedTask;
        }
    }

    private sealed class PresentingScenario : ISweepScenario
    {
        public string Name => "presenting";

        public IReadOnlySet<string> Modules { get; } = new HashSet<string> { "presentations" };

        public int Parts => CommandSweepTests.Parts;

        public async Task ArrangeAsync(SweepStage stage, CommandExecutor executor, CommandRegistry registry)
        {
            await executor.ExecuteAsync(registry.Find(new CommandId("deck.present"))!, stage.Workspace.Context);
            Dispatcher.UIThread.RunJobs();
            stage.Window.Activate();
            ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)stage.Workspace.Active!.View).FocusDocument();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static readonly IReadOnlyList<ISweepScenario> Scenarios =
    [
        new TypicalScenario(),
        new SelectedObjectScenario(typing: false),
        new SelectedObjectScenario(typing: true),
        new CellEditScenario(),
        new ReadOnlyScenario(),
        new ProtectedSheetScenario(),
        new ProtectedSectionScenario(),
        new PresentingScenario(),
    ];

    public static TheoryData<string, string, int> Slices(int shard)
    {
        var data = new TheoryData<string, string, int>();
        var slice = 0;
        var assemblyModule = typeof(CommandSweepTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "PulseOffice.SweepModule").Value;
        foreach (var scenario in Scenarios)
            foreach (var module in new[] { "sheets", "documents", "presentations" }.Where(scenario.Modules.Contains).Where(module => module == assemblyModule))
                for (var part = 0; part < scenario.Parts; part++)
                    if (slice++ % Shards == shard) data.Add(module, scenario.Name, part);
        return data;
    }

    protected Task Sweep(string module, string context, int part) => Session.Dispatch(async () =>
    {
        var scenario = Scenarios.Single(candidate => candidate.Name == context);
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var panel = provider.GetRequiredService<PulseOffice.UI.Controls.ISidePanelHost>();
        var host = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<PulseOffice.UI.Controls.DialogHost>().Single();
        var kind = module switch { "sheets" => ModuleKind.Sheets, "documents" => ModuleKind.Documents, _ => ModuleKind.Presentations };

        var steps = new System.Diagnostics.Stopwatch();
        var prepareSteps = string.Empty;
        async Task Prepare(bool arrange = true)
        {
            steps.Restart();
            while (host.IsDialogOpen) { host.Close(null); Dispatcher.UIThread.RunJobs(); }
            if (panel.IsOpen) panel.Toggle();
            foreach (var open in workspace.Documents.ToList()) workspace.Close(open);
            Dispatcher.UIThread.RunJobs();
            var closed = steps.ElapsedMilliseconds;
            await executor.ExecuteAsync(registry.Find(new CommandId($"shell.file.new.{module}"))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)workspace.Active!.View).FocusDocument();
            Dispatcher.UIThread.RunJobs();
            var created = steps.ElapsedMilliseconds;
            foreach (var line in module == "sheets" ? new[] { "10", "20", "=A1+A2", "Testo DI prova" } : module == "documents" ? new[] { "Uno due tre" } : [])
            {
                window.KeyTextInput(line);
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();
            }
            if (module == "sheets")
            {
                for (var step = 0; step < 4; step++) window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                for (var step = 0; step < 3; step++) window.KeyPress(Key.Down, RawInputModifiers.Shift, PhysicalKey.ArrowDown, null);
            }
            else if (module == "documents")
            {
                window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
                window.KeyPress(Key.End, RawInputModifiers.Shift, PhysicalKey.End, null);
            }
            Dispatcher.UIThread.RunJobs();
            var typed = steps.ElapsedMilliseconds;
            if (arrange) await scenario.ArrangeAsync(new SweepStage(provider, window, workspace), executor, registry);
            prepareSteps = $"{closed}/{created - closed}/{typed - created}/{steps.ElapsedMilliseconds - typed}";
        }

        var clipboard = new ClipboardProbe(provider.GetRequiredService<PulseOffice.Exchange.IExchangeClipboard>());
        async Task<string> Clipboard()
        {
            var content = await clipboard.GetAsync();
            return $"{content.Text}|{content.Html?.Length}|{content.Native?.Length}|{content.Png?.Length}";
        }

        string Frame()
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            if (frame is null) return string.Empty;
            using var stream = new MemoryStream();
            frame.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
        }

        string Objects()
        {
            var sheetContext = provider.GetRequiredService<PulseOffice.Sheets.UI.IActiveSheetContext>();
            var folio = provider.GetRequiredService<PulseOffice.Documents.UI.IActiveFolioContext>().Target?.Editor;
            var deck = provider.GetRequiredService<PulseOffice.Presentations.UI.IActiveDeckContext>().Target;
            return string.Join("|",
                sheetContext.HasTarget ? sheetContext.SelectedObject?.Id : null,
                sheetContext.ShapeText?.IsActive,
                folio?.SelectedObjectId,
                deck?.Selected()?.Id,
                deck?.IsTyping());
        }

        var phases = new System.Diagnostics.Stopwatch();
        var timings = new List<string>();
        long prepareMs = 0, beforeMs = 0;
        void Trace(SweepRow row, TimeSpan elapsed) =>
            timings.Add($"{module}\t{context}\t{part}\t{row.Command}\t{row.Outcome}\t{prepareMs}\t{beforeMs}\t{elapsed.TotalMilliseconds:0}\t{phases.ElapsedMilliseconds}\t{prepareSteps}");

        string Fingerprint()
        {
            var active = workspace.Active;
            var frame = Frame();
            var current = workspace.Context;
            var sheetContext = provider.GetRequiredService<PulseOffice.Sheets.UI.IActiveSheetContext>();
            return string.Join("\n",
                workspace.Documents.Count,
                active?.IsReadOnly,
                active?.History.CanRedo,
                host.IsDialogOpen,
                panel.IsOpen,
                window.FocusManager?.GetFocusedElement()?.GetType().FullName,
                sheetContext.HasTarget ? sheetContext.Selection.ToString() : null,
                Objects(),
                string.Join(",", registry.All.Where(command => command.Id != RepeatCommand).Select(command => (command.IsEnabled(current) ? "+" : "-") + (command.CheckedWhen?.Invoke(current) is true ? "!" : ""))),
                frame);
        }

        IReadOnlyDictionary<string, string> Content()
        {
            var model = workspace.Active?.Model;
            using var stream = new MemoryStream();
            var written = model switch
            {
                PulseOffice.Sheets.Workbook workbook => provider.GetRequiredService<PulseOffice.Formats.ISpreadsheetFormatRegistry>() is var sheets
                    ? sheets.WriterFor(sheets.Native)!.Write(workbook, stream) : null,
                PulseOffice.Documents.TextDocument text => provider.GetRequiredService<PulseOffice.Formats.ITextDocumentFormatRegistry>() is var texts
                    ? texts.WriterFor("content" + texts.Native.Extensions[0])!.Write(text, stream) : null,
                PulseOffice.Presentations.Presentation deck => provider.GetRequiredService<PulseOffice.Formats.IPresentationFormatRegistry>() is var decks
                    ? decks.WriterFor("content" + decks.Native.Extensions[0])!.Write(deck, stream) : null,
                _ => null,
            };
            if (written is null) return new Dictionary<string, string>();
            if (!written.Succeeded) return new Dictionary<string, string> { ["write"] = string.Join(" ", written.Diagnostics.Select(note => note.Message)) };
            stream.Position = 0;
            using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
            var parts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries.Where(entry => !VolatileParts.Contains(entry.FullName) && !entry.FullName.StartsWith("Thumbnails/", StringComparison.Ordinal)))
            {
                using var reader = new StreamReader(entry.Open());
                var text = reader.ReadToEnd();
                parts[entry.FullName] = entry.FullName == "meta.xml" ? VolatileMeta.Replace(text, string.Empty) : text;
            }
            return parts;
        }

        OpenDocument? prepared = null;
        object? preparedPoint = null;
        string? preparedPrint = null;
        IReadOnlyDictionary<string, string>? preparedContent = null;
        var undoChecked = 0;
        var undoUnreachable = 0;
        var undoFailures = new List<string>();
        var restores = 0;
        var rebuilds = 0;

        async Task Rebuild()
        {
            await Prepare();
            rebuilds++;
            prepared = workspace.Active;
            preparedPoint = prepared?.History.StatePoint;
            preparedPrint = Fingerprint();
            preparedContent = null;
        }

        bool Restore()
        {
            while (host.IsDialogOpen) { host.Close(null); Dispatcher.UIThread.RunJobs(); }
            if (panel.IsOpen) panel.Toggle();
            if (prepared is null || workspace.Documents.Count != 1 || !ReferenceEquals(workspace.Active, prepared)) return false;
            var history = prepared.History;
            for (var guard = 0; guard < 64 && !ReferenceEquals(history.StatePoint, preparedPoint) && history.CanUndo; guard++)
            {
                history.Undo();
                Dispatcher.UIThread.RunJobs();
            }
            if (!ReferenceEquals(history.StatePoint, preparedPoint)) return false;
            history.DiscardRedo();
            ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)prepared.View).FocusDocument();
            Dispatcher.UIThread.RunJobs();
            if (Fingerprint() != preparedPrint) return false;
            restores++;
            return true;
        }

        await Prepare(arrange: false);
        var typical = registry.All.Where(command => command.IsEnabled(workspace.Context)).Select(command => command.Id).ToHashSet();
        await Rebuild();
        var createCommand = executor.LastRepeatable?.Id;
        var pristine = true;
        var restorable = false;
        var rows = new List<SweepRow>();
        var ids = registry.All
            .Where(command => command.Module == kind || command.Module == ModuleKind.Shell)
            .Where(command => scenario.Covers(command, typical.Contains(command.Id)))
            .Select(command => command.Id)
            .Where((_, index) => index % scenario.Parts == part)
            .ToList();
        foreach (var id in ids)
        {
            if (Unsafe.Contains(id.Value)) continue;
            phases.Restart();
            if (id == RepeatCommand ? !pristine || executor.LastRepeatable?.Id != createCommand : !pristine && (!restorable || !Restore())) await Rebuild();
            pristine = true;
            prepareMs = phases.ElapsedMilliseconds;
            var command = registry.Find(id)!;
            var context = workspace.Context;
            if (!command.IsEnabled(context)) continue;
            pristine = false;
            restorable = false;

            var document = workspace.Active;
            var changes = 0;
            void Count(object? sender, EventArgs e) => changes++;
            var notices = 0;
            void Notice(object? sender, PulseOffice.UI.Controls.Notifications.NotificationChange e) => notices++;
            var notifications = provider.GetRequiredService<PulseOffice.UI.Controls.Notifications.INotificationCenter>();
            notifications.Changed += Notice;
            var outsideHistory = 0;
            void Outside(object? sender, EventArgs e) => outsideHistory++;
            var pointBefore = document?.History.StatePoint;
            if (document is not null && preparedContent is null && ReferenceEquals(pointBefore, preparedPoint)) preparedContent = Content();
            if (document is not null) { document.History.Changed += Count; document.History.ChangedOutsideHistory += Outside; }
            var before = Frame();
            var checkedBefore = command.CheckedWhen?.Invoke(context);
            var documents = workspace.Documents.Count;
            var asked = _fileDialogs.Asked;
            var panelBefore = panel.IsOpen;
            var focusBefore = window.FocusManager?.GetFocusedElement();
            var sheet = provider.GetRequiredService<PulseOffice.Sheets.UI.IActiveSheetContext>();
            var selectionBefore = sheet.HasTarget ? sheet.Selection.ToString() : null;
            var objectsBefore = Objects();
            await clipboard.SetAsync(new ClipboardContent { Text = "sweep" });
            var clipped = await Clipboard();
            beforeMs = phases.ElapsedMilliseconds - prepareMs;
            var waited = new List<string>();
            var running = executor.ExecuteAsync(command, context);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!running.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                Dispatcher.UIThread.RunJobs();
                if (host.IsDialogOpen)
                {
                    waited.Add("dialog");
                    host.Close(null);
                }
                else if (watch.Elapsed > TimeSpan.FromSeconds(1) && panel.IsOpen)
                {
                    waited.Add("panel");
                    panel.Toggle();
                }
                else if (watch.Elapsed > TimeSpan.FromSeconds(1))
                {
                    waited.Add("escape");
                    window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                    Thread.Sleep(50);
                }
                else Thread.Sleep(2);
                Dispatcher.UIThread.RunJobs();
            }

            if (document is not null) { document.History.Changed -= Count; document.History.ChangedOutsideHistory -= Outside; }
            notifications.Changed -= Notice;
            while (host.IsDialogOpen) { waited.Add("dialog"); host.Close(null); Dispatcher.UIThread.RunJobs(); }

            Assert.True(window.PlatformImpl is not null, $"{id} closed the window");
            if (!running.IsCompleted)
            {
                rows.Add(new SweepRow(id.Value, "Hang", string.Join("+", waited.Distinct()), ""));
                Trace(rows[^1], watch.Elapsed);
                continue;
            }

            var outcome = await running;
            restorable = waited.Count == 0;
            var effects = waited.Distinct().ToList();
            if (changes > 0) effects.Add("history");
            if (workspace.Documents.Count != documents || !ReferenceEquals(workspace.Active, document)) effects.Add("workspace");
            if (outcome.Notes.Count > 0) effects.Add("message");
            if (notices > 0) effects.Add("notification");
            if (_fileDialogs.Asked != asked) effects.Add("file");
            if (panel.IsOpen != panelBefore) effects.Add("panel");
            Dispatcher.UIThread.RunJobs();
            if (!ReferenceEquals(window.FocusManager?.GetFocusedElement(), focusBefore)) effects.Add("focus");
            if ((sheet.HasTarget ? sheet.Selection.ToString() : null) != selectionBefore) effects.Add("selection");
            if (Objects() != objectsBefore) effects.Add("objects");
            if (await Clipboard() != clipped) effects.Add("clipboard");
            if (command.CheckedWhen?.Invoke(workspace.Context) != checkedBefore) effects.Add("checked");
            if (effects.Count == 0 && Frame() != before) effects.Add("screen");
            if (effects.Count == 0 && AlreadyInThatState.TryGetValue(id.Value, out var reason)) effects.Add(reason);
            rows.Add(new SweepRow(id.Value, outcome.Kind.ToString(), string.Join("+", effects), outcome.Reason ?? string.Empty));
            Trace(rows[^1], watch.Elapsed);
            if (document is not null && ReferenceEquals(workspace.Active, document) && workspace.Documents.Count == documents && !command.OutsideHistory && outsideHistory == 0
                && preparedContent is not null && ReferenceEquals(pointBefore, preparedPoint) && !ReferenceEquals(document.History.StatePoint, pointBefore))
                CheckUndo(id.Value, document);
        }

        void CheckUndo(string command, OpenDocument document)
        {
            while (host.IsDialogOpen) { host.Close(null); Dispatcher.UIThread.RunJobs(); }
            if (panel.IsOpen) panel.Toggle();
            Dispatcher.UIThread.RunJobs();
            var after = Content();
            var stack = document.History;
            var count = 0;
            for (; count < 64 && !ReferenceEquals(stack.StatePoint, preparedPoint) && stack.CanUndo; count++) stack.Undo();
            Dispatcher.UIThread.RunJobs();
            if (!ReferenceEquals(stack.StatePoint, preparedPoint)) { undoUnreachable++; return; }
            undoChecked++;
            Report("undo", Content(), preparedContent!);
            for (var step = 0; step < count && stack.CanRedo; step++) stack.Redo();
            Dispatcher.UIThread.RunJobs();
            Report("redo", Content(), after);

            void Report(string what, IReadOnlyDictionary<string, string> actual, IReadOnlyDictionary<string, string> expected)
            {
                var differing = actual.Keys.Union(expected.Keys).Where(key => actual.GetValueOrDefault(key) != expected.GetValueOrDefault(key)).ToList();
                if (differing.Count == 0) return;
                restorable = false;
                undoFailures.Add($"{command} [{module}/{context}]: {what} does not restore the content: " + string.Join("; ", differing.Select(key => $"{key} {Diff(expected.GetValueOrDefault(key) ?? "", actual.GetValueOrDefault(key) ?? "")}")));
            }
        }

        await clipboard.SetAsync(new ClipboardContent { Text = string.Empty });
        if (Environment.GetEnvironmentVariable("PULSEOFFICE_SWEEP_OUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, $"{module}-{context}-{part}.tsv"), rows.Select(row => $"{row.Command}\t{row.Outcome}\t{row.Effect}\t{row.Detail.Replace('\t', ' ').Replace('\n', ' ')}"));
            File.WriteAllLines(Path.Combine(output, $"{module}-{context}-{part}.timings.tsv"), timings.Append($"#\trestores\t{restores}\trebuilds\t{rebuilds}\tundoChecked\t{undoChecked}\tundoUnreachable\t{undoUnreachable}"));
        }

        Assert.True(rows.Count > 0 || scenario is not TypicalScenario, "the typical context runs no command");
        var failed = rows.Where(row => row.Outcome is "Failed" or "Hang").Select(row => $"{row.Command}: {row.Outcome} {row.Effect} {row.Detail}").ToList();
        var undoBroken = undoFailures;
        var silent = rows.Where(row => row.Outcome == "Ran" && row.Effect.Length == 0).Select(row => $"{row.Command}: no effect and no reason").ToList();
        Assert.True(failed.Count + silent.Count + undoBroken.Count == 0, string.Join("\n", failed.Concat(silent).Concat(undoBroken)));
        window.Close();
    }, CancellationToken.None);
}
