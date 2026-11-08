using PulseOffice.Testing;
using Xunit;
using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class CompositionTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-composition-" + Guid.NewGuid().ToString("N"));

    public CompositionTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    [Fact]
    public Task Every_registered_service_resolves_with_validated_scopes_and_constructors() => Session.Dispatch(async () =>
    {
        var services = Register();
        await using var provider = services.BuildServiceProvider(ServiceConfiguration.ProviderOptions);

        var failures = new List<string>();
        foreach (var type in services.Select(descriptor => descriptor.ServiceType).Where(type => !type.ContainsGenericParameters).Distinct())
        {
            try
            {
                _ = provider.GetServices(type).ToList();
            }
            catch (Exception ex)
            {
                failures.Add($"{type.FullName}: {ex.Message}");
            }
        }

        Assert.Empty(failures);
    }, CancellationToken.None);

    [Fact]
    public Task Shell_commands_surfaces_and_the_three_modules_compose() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);

        var registry = new ShellComposition(provider).Compose();
        Assert.True(registry.All.Count > 1000);
        Assert.Empty(registry.UnplannedCommands(CommandSurface.Menu));
        Assert.Empty(registry.DeadLayoutRules(CommandSurface.Menu));
        Assert.Empty(registry.DeadLayoutRules(CommandSurface.Ribbon));
        Assert.Empty(registry.DeadLayoutRules(CommandSurface.ContextMenu));
        Assert.Empty(registry.ConflictingShortcuts());

        var window = provider.GetRequiredService<ShellWindow>();
        foreach (var module in new[] { ModuleKind.Shell, ModuleKind.Sheets, ModuleKind.Documents, ModuleKind.Presentations })
            window.UpdateForContext(new CommandContext(module, SelectionKind.None, DocumentState.None));
        window.Show();

        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        foreach (var module in new[] { "sheets", "documents", "presentations" })
        {
            var command = registry.Find(new CommandId($"shell.file.new.{module}"));
            Assert.NotNull(command);
            await executor.ExecuteAsync(command, workspace.Context);
        }

        Assert.Equal(3, workspace.Documents.Count);
        Assert.Equal(
            new HashSet<ModuleKind> { ModuleKind.Documents, ModuleKind.Presentations, ModuleKind.Sheets },
            workspace.Documents.Select(document => document.Module).ToHashSet());

        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_disposizione_dell_interfaccia_si_sceglie_nelle_impostazioni_da_due_miniature() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var settings = provider.GetRequiredService<PulseOffice.Settings.ISettingsService>();
        var definition = settings.Find(ShellSettingKeys.InterfaceMode)!;
        var texts = provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        var field = provider.GetRequiredService<PulseOffice.UI.Controls.Settings.ISettingEditorFactory>().Create(
            new PulseOffice.UI.Controls.Settings.SettingEditorContext(definition, settings, texts,
                provider.GetRequiredService<PulseOffice.UI.Icons.IIconCatalog>(),
                PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact),
                value => settings.Set(definition.Key, value)));

        var gallery = Assert.IsType<PulseOffice.UI.Controls.Galleries.ThumbnailGallery<PulseOffice.UI.Controls.Forms.InterfaceModeChoice>>(field.View);
        Assert.Equal(["ribbon", "compact"], definition.Choices.Select(choice => choice.Value));
        Assert.Equal(2, gallery.Items.Count);
        var window = new Avalonia.Controls.Window { Content = gallery, Width = 600, Height = 300 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        gallery.TileOf(1)!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        window.Close();
        Assert.Equal("compact", settings.Get<string>(ShellSettingKeys.InterfaceMode));
        Assert.Null(registry.Find(new CommandId("shell.tools.userInterface")));
        Assert.Null(registry.Find(new CommandId("shell.view.theme")));
    }, CancellationToken.None);

    [Theory]
    [InlineData(typeof(PulseOffice.Sheets.Queries.IQueryRefresher))]
    [InlineData(typeof(PulseOffice.Sheets.Queries.IPivotSourceRefresher))]
    [InlineData(typeof(PulseOffice.Sheets.UI.LedgerExternalDataCommands))]
    [InlineData(typeof(PulseOffice.Documents.UI.Commands.FolioMailMergeCommands))]
    public Task Without_the_external_query_gate_composition_fails_instead_of_allowing(Type consumer) => Session.Dispatch(() =>
    {
        var services = Register();
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(PulseOffice.Data.IExternalQueryGate));
        services.RemoveAll<PulseOffice.Data.IExternalQueryGate>();

        Assert.ThrowsAny<InvalidOperationException>(() =>
        {
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            provider.GetRequiredService(consumer);
        });
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Theory]
    [InlineData(ModuleKind.Sheets, "ledger.macro.editor")]
    [InlineData(ModuleKind.Documents, "folio.macro.editor")]
    [InlineData(ModuleKind.Presentations, "deck.macro.editor")]
    public Task La_scheda_Strumenti_ha_il_gruppo_delle_macro_nei_tre_moduli(ModuleKind module, string editorCommand) => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();

        var tabs = registry.BuildRibbon(new CommandContext(module, SelectionKind.None, DocumentState.DocumentOpen));
        var texts = provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        var tools = Assert.Single(tabs, tab => tab.Segment == texts["ribbon.tools"]);
        var macros = Assert.Single(tools.Children, group => group.Segment == texts["ribbon.tools.macros"]);
        var commands = macros.Children.Select(child => child.Command?.Id.Value).ToList();

        Assert.Contains(editorCommand, commands);
        Assert.Contains("shell.tools.recordMacro", commands);
    }, CancellationToken.None);

    [Fact]
    public Task I_comandi_che_modificano_si_spengono_in_sola_lettura_nei_tre_moduli() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var modifying = new System.Text.RegularExpressions.Regex(@"\.(insert|format|object|paragraph|slide|table|text|style|data|sheet|fill|border|number|merge|font)\.");
        var reading = new System.Text.RegularExpressions.Regex(@"(findReplace|numberRecognition|\.select[A-Z][a-z]*|openPackaged|saveBackground|style\.panel|statistics|hyperlink\.open)$");

        var open = registry.All
            .Where(command => command.Module is ModuleKind.Sheets or ModuleKind.Presentations or ModuleKind.Documents)
            .Where(command => modifying.IsMatch(command.Id.Value) && !reading.IsMatch(command.Id.Value))
            .Where(command => (command.ForbiddenState & DocumentState.ReadOnly) == 0)
            .Select(command => command.Id.Value)
            .ToList();

        Assert.Empty(open);
    }, CancellationToken.None);

    [Fact]
    public Task In_Deck_Invio_apre_la_casella_e_Esc_conserva_il_testo_scritto() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var target = provider.GetRequiredService<PulseOffice.Presentations.UI.IActiveDeckContext>().Target!;
        var slide = target.CurrentSlide()!;
        var subtitle = slide.Elements.OfType<PulseOffice.Presentations.TextElement>().First(element => element.Text.Role == PulseOffice.Text.Rich.PlaceholderRole.Subtitle);
        target.Select([subtitle]);
        ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)workspace.Active!.View).FocusDocument();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Enter, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("Uno");
        window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var edited = target.CurrentSlide()!.Find(subtitle.Id) as PulseOffice.Presentations.TextElement;
        Assert.Equal("Uno", edited?.Text.PlainText);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Un_documento_nuovo_riceve_il_fuoco_della_tastiera_in_ogni_modulo() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        foreach (var module in new[] { "documents", "sheets", "presentations", "documents" })
        {
            await executor.ExecuteAsync(registry.Find(new CommandId($"shell.file.new.{module}"))!, workspace.Context);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var focused = window.FocusManager?.GetFocusedElement() as Avalonia.Visual;
            Assert.NotNull(focused);
            Assert.True(ReferenceEquals(focused, workspace.Active!.View) || Avalonia.VisualTree.VisualExtensions.IsVisualAncestorOf(workspace.Active.View, focused), module);
        }
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_ricerca_dei_comandi_trova_i_nuovi_documenti_con_le_parole_di_tutti_i_giorni() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var texts = provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var original = texts.Language;
        try
        {
            foreach (var (language, query, id) in new[]
            {
                (PulseOffice.Localization.UiLanguage.Italian, "nuova presentazione", "shell.file.new.presentations"),
                (PulseOffice.Localization.UiLanguage.Italian, "foglio di calcolo", "shell.file.new.sheets"),
                (PulseOffice.Localization.UiLanguage.English, "new presentation", "shell.file.new.presentations"),
                (PulseOffice.Localization.UiLanguage.German, "neue Präsentation", "shell.file.new.presentations"),
                (PulseOffice.Localization.UiLanguage.Spanish, "nueva hoja", "shell.file.new.sheets"),
                (PulseOffice.Localization.UiLanguage.French, "nouveau texte", "shell.file.new.documents"),
            })
            {
                texts.Use(language);
                Assert.Equal(id, registry.Search(query, workspace.Context).First().Id.Value);
            }
        }
        finally
        {
            texts.Use(original);
        }
        await Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task La_barra_di_stato_mostra_solo_il_documento_attivo_e_il_guscio_cambia_lingua_a_caldo() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var status = provider.GetRequiredService<PulseOffice.UI.Controls.Services.IStatusBarService>();
        bool Shows(string id) => status.Segments.Any(segment => segment.Id == id && segment.IsVisible && segment.Text.Length > 0);

        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(Shows("ledger.zoom"));

        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.documents"))!, workspace.Context);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(Shows("ledger.zoom"));

        var texts = provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        var original = texts.Language;
        texts.Use(original == PulseOffice.Localization.UiLanguage.German ? PulseOffice.Localization.UiLanguage.French : PulseOffice.Localization.UiLanguage.German);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var titleBar = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<PulseOffice.UI.Controls.TitleBar>().Single();
        Assert.Equal(texts["titlebar.search"], Avalonia.Automation.AutomationProperties.GetName(titleBar.CommandSearch!));
        Assert.Contains(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(titleBar.CommandSearch!).OfType<Avalonia.Controls.TextBlock>(), block => block.Text == texts["titlebar.search"]);
        texts.Use(original);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_sillabazione_automatica_di_Folio_usa_i_dizionari() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);

        Assert.IsType<PulseOffice.Documents.Layout.Inline.DictionaryHyphenation>(provider.GetRequiredService<PulseOffice.Documents.Layout.Inline.IHyphenationPoints>());
    }, CancellationToken.None);

    [Fact]
    public Task Informazioni_su_PulseOffice_apre_una_finestra_con_versione_e_crediti() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var host = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<PulseOffice.UI.Controls.DialogHost>().Single();

        var showing = executor.ExecuteAsync(registry.Find(new CommandId("shell.help.about"))!, workspace.Context);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(host.IsDialogOpen);
        var view = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<PulseOffice.Shell.About.AboutView>().Single();
        var lines = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<Avalonia.Controls.TextBlock>().Select(block => block.Text ?? string.Empty).ToList();
        Assert.Contains(lines, line => line.Contains("Twemoji", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(".NET", StringComparison.Ordinal));

        host.Close(null);
        await showing;
        Assert.False(host.IsDialogOpen);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task I_pannelli_di_Deck_e_il_pannello_laterale_cambiano_lingua_a_caldo() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        await executor.ExecuteAsync(registry.Find(new CommandId("shell.view.sidebar"))!, workspace.Context);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var texts = provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        var original = texts.Language;
        var other = original == PulseOffice.Localization.UiLanguage.French ? PulseOffice.Localization.UiLanguage.Spanish : PulseOffice.Localization.UiLanguage.French;
        IEnumerable<string> Shown() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TextBlock>().Select(block => block.Text ?? string.Empty);
        try
        {
            texts.Use(other);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Contains(texts["deck.interactions"].ToUpperInvariant(), Shown());
            Assert.Contains(texts["deck.slides"].ToUpperInvariant(), Shown());
            var side = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<PulseOffice.UI.Controls.SidePanelView>().Single();
            Assert.True(side.IsOpen);
            Assert.Contains(Shown(), line => line == texts["navigator.panel.empty"] || line == texts["navigator.panel.go"]);
        }
        finally
        {
            texts.Use(original);
        }
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Ogni_pulsante_visibile_ha_un_nome_per_i_lettori_di_schermo() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var anonymous = new List<string>();
        foreach (var module in new[] { "documents", "sheets", "presentations" })
        {
            await executor.ExecuteAsync(registry.Find(new CommandId($"shell.file.new.{module}"))!, workspace.Context);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            foreach (var button in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.Control>()
                .Where(control => control is Avalonia.Controls.Button or Avalonia.Controls.Primitives.ToggleButton or Avalonia.Controls.ComboBox or Avalonia.Controls.TextBox)
                .Where(control => control.IsEffectivelyVisible && control.Focusable && control.TemplatedParent is null))
            {
                if (button is Avalonia.Controls.ContentControl { Content: string { Length: > 0 } }) continue;
                if (!string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(button))) continue;
                anonymous.Add($"{module}: {button.GetType().Name} {string.Join(">", Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(button).Take(6).Select(a => a.GetType().Name))} {(button as Avalonia.Controls.ContentControl)?.Content?.GetType().Name} {(button as Avalonia.Controls.TextBox)?.PlaceholderText} {button.Name}");
            }
        }
        Assert.True(anonymous.Count == 0, string.Join("\n", anonymous.Distinct()));

        var last = workspace.Active;
        await executor.ExecuteAsync(registry.Find(new CommandId("shell.window.nextDocument"))!, workspace.Context);
        Assert.Same(workspace.Documents[0], workspace.Active);
        await executor.ExecuteAsync(registry.Find(new CommandId("shell.window.previousDocument"))!, workspace.Context);
        Assert.Same(last, workspace.Active);
        window.Close();
    }, CancellationToken.None);

    private sealed class CountingQuitTarget : IQuitTarget
    {
        public int Quits { get; private set; }

        public void Quit() => Quits++;
    }

    [Fact]
    public Task In_Ledger_il_testo_passato_alla_barra_della_formula_ferma_l_uscita() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)workspace.Active!.View).FocusDocument();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("42");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var formulaBar = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(workspace.Active.View)
            .OfType<Avalonia.Controls.TextBox>().Single(box => box.Name == "FormulaBar");
        formulaBar.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(workspace.Active.IsModified);

        var quit = provider.GetRequiredService<QuitCoordinator>();
        var target = new CountingQuitTarget();
        var request = new System.ComponentModel.CancelEventArgs();

        Assert.True(quit.Intercept(request, target));
        Assert.True(request.Cancel);
        Assert.True(workspace.Active.IsModified);
        Assert.Equal(0, target.Quits);
        window.Close();
    }, CancellationToken.None);

    private sealed class RecordingFileDialogs : PulseOffice.Platform.IFileDialogService
    {
        public string? StartFolder { get; private set; }

        public Task<PulseOffice.Platform.ChosenFile?> OpenFileAsync(string title, IReadOnlyList<PulseOffice.Platform.FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<PulseOffice.Platform.ChosenFile?>(null);

        public Task<PulseOffice.Platform.ChosenFile?> OpenFileInAsync(string title, IReadOnlyList<PulseOffice.Platform.FileFilter> filters, string startFolder, CancellationToken ct = default)
        {
            StartFolder = startFolder;
            return Task.FromResult<PulseOffice.Platform.ChosenFile?>(null);
        }

        public Task<PulseOffice.Platform.ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<PulseOffice.Platform.FileFilter> filters, CancellationToken ct = default) =>
            Task.FromResult<PulseOffice.Platform.ChosenFile?>(null);
    }

    [Fact]
    public Task Apri_un_modello_parte_dalla_cartella_dei_modelli_e_Nuovo_da_modello_si_trova_cercando_modello() => Session.Dispatch(async () =>
    {
        var dialogs = new RecordingFileDialogs();
        var services = Register();
        services.AddSingleton<PulseOffice.Platform.IFileDialogService>(dialogs);
        await using var provider = services.BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();

        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.openTemplate"))!, workspace.Context);

        Assert.Equal(provider.GetRequiredService<IUserDataPaths>().Configuration("templates"), dialogs.StartFolder);

        var texts = provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        Assert.Contains("modello", texts["command.shell.file.templates.title"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(texts["command.shell.file.templates.title"], texts["template.save.explanation"], StringComparison.Ordinal);
    }, CancellationToken.None);

    [Theory]
    [InlineData(PulseOffice.Localization.UiLanguage.Italian, "Misura della diapositiva")]
    [InlineData(PulseOffice.Localization.UiLanguage.English, "slide dimensions")]
    [InlineData(PulseOffice.Localization.UiLanguage.German, "Folienmaß")]
    [InlineData(PulseOffice.Localization.UiLanguage.Spanish, "medida de la diapositiva")]
    [InlineData(PulseOffice.Localization.UiLanguage.French, "dimensions de la diapositive")]
    public Task La_palette_trova_Formato_diapositiva_dai_sinonimi_nelle_cinque_lingue(PulseOffice.Localization.UiLanguage language, string query) => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var texts = provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        texts.Use(language);
        var palette = provider.GetRequiredService<ICommandPaletteSource>();

        var deck = await NewDeckAsync(provider, registry);
        var results = palette.Sections(query, deck).Single().Items;

        Assert.Equal("deck.slideSize", results[0].Command.Id.Value);
        Assert.NotNull(registry);
    }, CancellationToken.None);

    [Fact]
    public Task La_palette_vuota_mette_in_testa_i_comandi_scelti_di_recente() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var palette = provider.GetRequiredService<ICommandPaletteSource>();
        var deck = await NewDeckAsync(provider, registry);

        palette.Chosen(registry.Find(new CommandId("deck.slideSize"))!);
        var sections = palette.Sections(string.Empty, deck);

        Assert.Equal("deck.slideSize", sections[0].Items[0].Command.Id.Value);
        Assert.True(sections.Count > 3);
        Assert.Equal(sections.Count, sections.Select(section => section.Title).Distinct().Count());
    }, CancellationToken.None);

    [Fact]
    public Task La_barra_di_stato_mostra_lo_zoom_del_documento_attivo() => Session.Dispatch(async () =>
    {
        await using var provider = Register().BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var zoom = provider.GetRequiredService<IStatusZoomSource>();

        Assert.Null(zoom.Percent(new CommandContext(ModuleKind.Shell, SelectionKind.None, DocumentState.None)));

        var deck = await NewDeckAsync(provider, registry);
        Assert.Equal("deck.zoom.set", zoom.Command(deck)?.Id.Value);
        Assert.True(zoom.Percent(deck) > 0);
    }, CancellationToken.None);

    private static async Task<CommandContext> NewDeckAsync(ServiceProvider provider, CommandRegistry registry)
    {
        provider.GetRequiredService<ShellWindow>().Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return workspace.Context;
    }

    private static Microsoft.Extensions.DependencyInjection.ServiceCollection Register() =>
        ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable());
}
