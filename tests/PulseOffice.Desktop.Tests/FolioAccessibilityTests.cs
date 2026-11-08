using PulseOffice.Testing;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class FolioAccessibilityTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-folio-a11y-" + Guid.NewGuid().ToString("N"));

    public FolioAccessibilityTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private static string? NameOf(Control control) =>
        ControlAutomationPeer.CreatePeerForElement(control) is { } peer ? peer.GetName() : null;

    private static IEnumerable<string> Unnamed(Visual root) =>
        root.GetVisualDescendants().OfType<Control>()
            .Where(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control is not ScrollViewer && control is not ScrollBar)
            .Where(control => string.IsNullOrWhiteSpace(NameOf(control)))
            .Select(control => control.GetType().Name + " in " + string.Join("/", control.GetVisualAncestors().OfType<Control>().Take(4).Select(a => a.GetType().Name)));

    private sealed class Folio(ServiceProvider provider, CommandRegistry registry, ShellWindow window) : IAsyncDisposable
    {
        public ShellWindow Window => window;

        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public PulseOffice.Documents.UI.FolioView View => window.GetVisualDescendants().OfType<PulseOffice.Documents.UI.FolioView>().First();

        public PulseOffice.Documents.UI.Editing.FolioEditor Editor => View.Editor;

        public Control? Focused => window.FocusManager?.GetFocusedElement() as Control;

        public Task Start(string id) =>
            Service<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId(id))!, Service<IDocumentWorkspace>().Context);

        public void Key(Key key, RawInputModifiers modifiers = RawInputModifiers.None, PhysicalKey physical = PhysicalKey.None)
        {
            window.KeyPress(key, modifiers, physical, null);
            window.KeyRelease(key, modifiers, physical, null);
            Dispatcher.UIThread.RunJobs();
        }

        public string Spoken => NameOf(Focused!) ?? string.Empty;

        public async ValueTask DisposeAsync()
        {
            window.Close();
            await provider.DisposeAsync();
        }
    }

    private static async Task<Folio> Open()
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var folio = new Folio(provider, registry, window);
        await folio.Start("shell.file.new.documents");
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return folio;
    }

    [Fact]
    public Task Ogni_controllo_raggiungibile_da_tastiera_di_Folio_e_dei_dialoghi_principali_ha_un_nome() => _session.Dispatch(async () =>
    {
        await using var folio = await Open();
        Assert.Empty(Unnamed(folio.Window).Distinct());
        foreach (var id in new[] { "folio.format.character", "folio.format.paragraph", "folio.insert.table", "folio.insert.fieldDialog", "folio.insert.hyperlink", "folio.style.edit", "folio.style.new" })
        {
            var running = folio.Start(id);
            Dispatcher.UIThread.RunJobs();
            folio.Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var unnamed = Unnamed(folio.Window).Distinct().ToList();
            Assert.True(unnamed.Count == 0, id + ": " + string.Join("; ", unnamed));
            folio.Key(Key.Escape, physical: PhysicalKey.Escape);
            for (var spin = 0; spin < 1000 && !running.IsCompleted; spin++)
            {
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
            Assert.True(running.IsCompleted, id + ": the dialog did not close");
            await running;
            Assert.Contains(folio.Focused!.GetSelfAndVisualAncestors(), visual => visual is PulseOffice.Documents.UI.FolioView);
        }
    }, CancellationToken.None);

    [Fact]
    public Task I_righelli_hanno_un_nome_accessibile() => _session.Dispatch(async () =>
    {
        await using var folio = await Open();
        var texts = folio.Service<PulseOffice.Localization.ITextCatalog>();
        Assert.Equal(texts["ruler.horizontal"], NameOf(folio.View.Rulers.Horizontal));
        Assert.Equal(texts["ruler.vertical"], NameOf(folio.View.Rulers.Vertical));
    }, CancellationToken.None);

    [Fact]
    public Task Il_documento_annuncia_pagina_riga_colonna_cella_e_formato() => _session.Dispatch(async () =>
    {
        await using var folio = await Open();
        var texts = folio.Service<PulseOffice.Localization.ITextCatalog>();
        var surface = texts["folio.surface.name"];
        folio.Editor.Type("Ciao");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(texts.Format("folio.surface.position", surface, 1, 1, 1, 5), folio.Spoken);
        folio.Editor.SplitParagraph();
        folio.Editor.Type("ab");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(texts.Format("folio.surface.position", surface, 1, 1, 2, 3), folio.Spoken);

        folio.Key(Key.B, RawInputModifiers.Meta, PhysicalKey.B);
        var help = Avalonia.Automation.AutomationProperties.GetHelpText(folio.Focused!);
        Assert.Contains(texts["folio.surface.bold"], help, StringComparison.Ordinal);

        folio.Editor.SplitParagraph();
        folio.Editor.Insert((writer, at) => writer.InsertTable(folio.Editor.Document, folio.Editor.History, at, 2, 2), "insert-table");
        var table = folio.Editor.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>().First();
        var first = new PulseOffice.Documents.Editing.DocumentPosition(table.Rows[0].Cells[0].Content.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Id, 0);
        folio.Editor.Select(first, first);
        Dispatcher.UIThread.RunJobs();
        Assert.EndsWith(texts.Format("folio.surface.cell", 1, 1), folio.Spoken, StringComparison.Ordinal);
        folio.Key(Key.Tab, physical: PhysicalKey.Tab);
        Assert.EndsWith(texts.Format("folio.surface.cell", 1, 2), folio.Spoken, StringComparison.Ordinal);
    }, CancellationToken.None);

    [Fact]
    public Task Maiusc_F10_apre_il_menu_contestuale_al_cursore_con_il_fuoco_sulla_prima_voce() => _session.Dispatch(async () =>
    {
        await using var folio = await Open();
        folio.Editor.Type("Ciao");
        Dispatcher.UIThread.RunJobs();
        folio.Key(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10);
        Dispatcher.UIThread.RunJobs();
        var menu = folio.View.GetVisualDescendants().OfType<Control>().Select(control => control.ContextMenu).OfType<ContextMenu>().FirstOrDefault(open => open.IsOpen);
        Assert.NotNull(menu);
        Assert.IsType<MenuItem>(folio.Focused);
        for (var step = 0; step < 4; step++)
        {
            folio.Key(Key.Down, physical: PhysicalKey.ArrowDown);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<string>(Assert.IsType<MenuItem>(folio.Focused).Header);
        }
        folio.Key(Key.Escape, physical: PhysicalKey.Escape);
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu!.IsOpen);
        Assert.Contains(folio.Focused!.GetSelfAndVisualAncestors(), visual => visual is PulseOffice.Documents.UI.FolioView);
    }, CancellationToken.None);

    [Fact]
    public Task Gli_oggetti_si_selezionano_e_si_scorrono_da_tastiera() => _session.Dispatch(async () =>
    {
        await using var folio = await Open();
        var texts = folio.Service<PulseOffice.Localization.ITextCatalog>();
        var objects = folio.Service<PulseOffice.Documents.UI.Editing.IFolioObjectEditing>();
        var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2));
        objects.InsertTextFrame(folio.Editor, size);
        folio.Editor.SelectObject(null);
        objects.InsertTextFrame(folio.Editor, size);
        folio.Editor.SelectObject(null);
        folio.View.FocusDocument();
        Dispatcher.UIThread.RunJobs();
        var all = folio.Editor.Document.Objects.All;

        folio.Key(Key.O, RawInputModifiers.Meta | RawInputModifiers.Alt | RawInputModifiers.Shift, PhysicalKey.O);
        Assert.Equal(all[0].Id, folio.Editor.SelectedObjectId);
        Assert.Equal(texts.Format("folio.surface.object", texts["folio.surface.name"], texts["folio.surface.kind.frame"]), folio.Spoken);
        folio.Key(Key.Tab, physical: PhysicalKey.Tab);
        Assert.Equal(all[1].Id, folio.Editor.SelectedObjectId);
        folio.Key(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab);
        Assert.Equal(all[0].Id, folio.Editor.SelectedObjectId);
        folio.Key(Key.Escape, physical: PhysicalKey.Escape);
        Assert.Null(folio.Editor.SelectedObjectId);
    }, CancellationToken.None);

    [Fact]
    public Task F6_gira_fra_ribbon_documento_e_barra_di_stato_di_Folio() => _session.Dispatch(async () =>
    {
        await using var folio = await Open();
        var visited = new List<string>();
        for (var step = 0; step < 6; step++)
        {
            folio.Key(Key.F6, physical: PhysicalKey.F6);
            var focused = folio.Focused!;
            visited.Add(focused.GetSelfAndVisualAncestors().Any(visual => visual is PulseOffice.Documents.UI.FolioView) ? "document"
                : focused.GetSelfAndVisualAncestors().Any(visual => visual is PulseOffice.UI.Controls.Ribbon.RibbonTabStrip) ? "ribbon"
                : focused.GetSelfAndVisualAncestors().Any(visual => visual.GetType().Name == "StatusBarView") ? "status"
                : focused.GetType().Name);
            Assert.False(string.IsNullOrWhiteSpace(NameOf(focused)), focused.GetType().Name);
        }
        Assert.Contains("ribbon", visited);
        Assert.Contains("document", visited);
        Assert.Contains("status", visited);
    }, CancellationToken.None);
}
