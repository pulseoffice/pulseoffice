using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands.Shortcuts;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public class ShortcutEditorLogicTests
{
    private readonly GestureChordParser _parser = new();

    private static CommandDefinition Command(string id, string? gesture, ModuleKind module, string menu = "file") => new()
    {
        Id = new CommandId(id),
        MenuPath = menu,
        Module = module,
        DefaultGesture = gesture,
        Surface = CommandSurface.Advanced,
        Execute = (_, _) => Task.CompletedTask,
    };

    private static CommandRegistry Registry(params CommandDefinition[] commands)
    {
        var registry = new CommandRegistry(new FakeTextCatalog());
        registry.RegisterRange(commands);
        return registry;
    }

    private static ShortcutDraft Draft(CommandRegistry registry, bool macOS = true) =>
        new ShortcutDraftFactory(ReservedShortcutTable.ForSystem(macOS)).Create(new ShortcutCatalog(registry).Entries());

    private GestureChord Chord(string text) => _parser.Parse(text)!;

    [Theory]
    [InlineData("ctrl+shift+p", "Ctrl+Shift+P")]
    [InlineData("Shift+Ctrl+p", "Ctrl+Shift+P")]
    [InlineData("Ctrl++", "Ctrl+Plus")]
    [InlineData("alt+f4", "Alt+F4")]
    [InlineData("Cmd+Option+0", "Ctrl+Alt+0")]
    [InlineData("Ctrl+Shift+;", "Ctrl+Shift+OemSemicolon")]
    [InlineData("Ctrl+Shift+>", "Ctrl+Shift+Period")]
    [InlineData("Ctrl+`", "Ctrl+OemTilde")]
    public void LaFormaCanonicaNonDipendeDallOrdineNeDalleMaiuscole(string text, string canonical) =>
        Assert.Equal(canonical, Chord(text).Canonical());

    [Fact]
    public void SuMacLeScorciatoieSiMostranoConISimboli()
    {
        var notation = GestureNotation.ForSystem(macOS: true);
        Assert.Equal("⌥⇧⌘V", notation.Display("Ctrl+Alt+Shift+V"));
        Assert.Equal("⌃⌘F", notation.Display("Control+Ctrl+F"));
    }

    [Fact]
    public void AltroveLeScorciatoieSiMostranoConINomi()
    {
        var notation = GestureNotation.ForSystem(macOS: false);
        Assert.Equal("Ctrl+Alt+Shift+V", notation.Display("Ctrl+Alt+Shift+V"));
        Assert.Equal("Ctrl++", notation.Display("Ctrl+Plus"));
    }

    [Fact]
    public void LeRiservateDipendonoDalSistema()
    {
        var mac = ReservedShortcutTable.ForSystem(macOS: true);
        var pc = ReservedShortcutTable.ForSystem(macOS: false);

        Assert.True(mac.IsReserved(Chord("Ctrl+Q")));
        Assert.True(mac.IsReserved(Chord("Ctrl+H")));
        Assert.False(pc.IsReserved(Chord("Ctrl+Q")));
        Assert.True(pc.IsReserved(Chord("Alt+F4")));
        Assert.True(pc.IsReserved(Chord("Super+E")));
        Assert.False(mac.IsReserved(Chord("Ctrl+B")));
    }

    [Fact]
    public void IlCatalogoPortaTitoloPercorsoEPredefinita()
    {
        var registry = Registry(Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets, "format/font"));
        registry.Rebind(new CommandId("ledger.bold"), "Ctrl+G");

        var entry = Assert.Single(new ShortcutCatalog(registry).Entries());

        Assert.Equal("Ctrl+G", entry.Gesture);
        Assert.Equal("Ctrl+B", entry.FactoryGesture);
        Assert.Equal("format", entry.MenuRoot);
        Assert.Equal(ModuleKind.Sheets, entry.Module);
    }

    [Fact]
    public void LeCategorieComprendonoModuliEMenuRicavatiDaiPercorsi()
    {
        var registry = Registry(
            Command("shell.open", "Ctrl+O", ModuleKind.Shell, "file"),
            Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets, "format/font"),
            Command("deck.fit", "Ctrl+0", ModuleKind.Presentations, "view/zoom"));
        var entries = new ShortcutCatalog(registry).Entries();

        var categories = new ShortcutCategories(registry, new FakeTextCatalog()).Build(entries);

        Assert.IsType<AllShortcutsCategory>(categories[0]);
        Assert.Equal(
            ["module.shell", "module.sheets", "module.presentations"],
            categories.OfType<ModuleShortcutCategory>().Select(category => category.Key));
        Assert.Equal(
            ["file", "format", "view"],
            categories.OfType<MenuShortcutCategory>().Select(category => category.Root).Order());
        Assert.True(categories.OfType<MenuShortcutCategory>().Single(c => c.Root == "format").Contains(entries.Single(e => e.Id == "ledger.bold")));
    }

    [Fact]
    public void LaRicercaTrovaPerNomePercorsoEScorciatoia()
    {
        var search = new ShortcutSearch(GestureNotation.ForSystem(macOS: true));
        var entry = new ShortcutEntry("ledger.bold", ModuleKind.Sheets, "Grassetto", "Formato › Carattere", "format", ["bold"], "Ctrl+B", "Ctrl+B");

        Assert.True(search.Matches(entry, "Ctrl+B", "grass"));
        Assert.True(search.Matches(entry, "Ctrl+B", "carattere bold"));
        Assert.True(search.Matches(entry, "Ctrl+B", "⌘B"));
        Assert.False(search.Matches(entry, "Ctrl+B", "corsivo"));
        Assert.True(search.Matches("Ctrl+B", Chord("cmd+b")));
        Assert.False(search.Matches("Ctrl+Shift+B", Chord("Ctrl+B")));
    }

    [Fact]
    public void UnTastoSenzaModificatoreORiservatoVieneRifiutato()
    {
        var draft = Draft(Registry(Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets)));

        Assert.Equal(ChordProblem.NeedsModifier, draft.Check("ledger.bold", Chord("Shift+B")).Problem);
        Assert.Equal(ChordProblem.Reserved, draft.Check("ledger.bold", Chord("Ctrl+Q")).Problem);
        Assert.True(draft.Check("ledger.bold", Chord("F9")).IsAccepted);
        Assert.False(draft.Assign("ledger.bold", Chord("Ctrl+Q"), reassign: true));
    }

    [Fact]
    public void IConflittiSeguonoLaStessaRegolaDelControlloDAvvio()
    {
        var draft = Draft(Registry(
            Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets),
            Command("deck.bold", "Ctrl+B", ModuleKind.Presentations),
            Command("shell.find", "Ctrl+F", ModuleKind.Shell),
            Command("ledger.other", null, ModuleKind.Sheets)));

        Assert.Empty(draft.ConflictsOf("ledger.bold"));
        Assert.Equal(["shell.find"], draft.Check("ledger.other", Chord("Ctrl+F")).Conflicts.Select(entry => entry.Id));
        Assert.Equal(["ledger.bold"], draft.Check("ledger.other", Chord("Ctrl+B")).Conflicts.Select(entry => entry.Id));
    }

    [Fact]
    public void RiassegnareTogliendoLaScorciatoiaAllAltroComando()
    {
        var draft = Draft(Registry(
            Command("shell.find", "Ctrl+F", ModuleKind.Shell),
            Command("ledger.other", null, ModuleKind.Sheets)));

        Assert.False(draft.Assign("ledger.other", Chord("Ctrl+F"), reassign: false));
        Assert.True(draft.Assign("ledger.other", Chord("Ctrl+F"), reassign: true));

        Assert.Null(draft.GestureOf("shell.find"));
        Assert.Equal("Ctrl+F", draft.GestureOf("ledger.other"));
        Assert.Equal(2, draft.Edits().Count);
    }

    [Fact]
    public void RipristinareRiportaAllaPredefinitaETogliereLaSvuota()
    {
        var registry = Registry(Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets));
        registry.Rebind(new CommandId("ledger.bold"), "Ctrl+G");
        var draft = Draft(registry);

        Assert.True(draft.IsCustomized("ledger.bold"));
        draft.RestoreDefault("ledger.bold");
        Assert.Equal("Ctrl+B", draft.GestureOf("ledger.bold"));
        Assert.False(draft.IsCustomized("ledger.bold"));

        draft.Remove("ledger.bold");
        Assert.Null(draft.GestureOf("ledger.bold"));

        draft.RestoreAll();
        Assert.False(draft.HasCustomizations);
    }

    [Fact]
    public void LePersonalizzazioniSiSalvanoSoloSeDiversaDallaPredefinitaESiRileggono()
    {
        var registry = Registry(
            Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets),
            Command("ledger.italic", "Ctrl+I", ModuleKind.Sheets));
        var store = new MemoryStore();
        var customization = new ShortcutCustomization(registry, store);

        customization.Apply(new Dictionary<string, string?> { ["ledger.bold"] = "Ctrl+Alt+B", ["ledger.italic"] = null });

        Assert.Equal("Ctrl+Alt+B", store.Saved["ledger.bold"]);
        Assert.Null(store.Saved["ledger.italic"]);

        customization.Apply(new Dictionary<string, string?> { ["ledger.bold"] = "Ctrl+B" });
        Assert.False(store.Saved.ContainsKey("ledger.bold"));

        var fresh = Registry(
            Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets),
            Command("ledger.italic", "Ctrl+I", ModuleKind.Sheets));
        var applied = new ShortcutCustomization(fresh, store).ApplySaved();

        Assert.Equal(1, applied);
        Assert.Null(fresh.Find(new CommandId("ledger.italic"))!.DefaultGesture);
        Assert.Equal("Ctrl+I", fresh.FactoryGestureOf(new CommandId("ledger.italic")));
    }

    [Fact]
    public void UnaPersonalizzazioneIllegibileOSconosciutaSiIgnora()
    {
        var registry = Registry(Command("ledger.bold", "Ctrl+B", ModuleKind.Sheets));
        var store = new MemoryStore();
        store.Save(new Dictionary<string, string?> { ["ledger.bold"] = "Ctrl+Bogus+", ["missing"] = "Ctrl+K" });

        Assert.Equal(0, new ShortcutCustomization(registry, store).ApplySaved());
        Assert.Equal("Ctrl+B", registry.Find(new CommandId("ledger.bold"))!.DefaultGesture);
    }

    private sealed class MemoryStore : IShortcutBindingStore
    {
        public Dictionary<string, string?> Saved { get; private set; } = [];

        public IReadOnlyDictionary<string, string?> Load() => new Dictionary<string, string?>(Saved);

        public void Save(IReadOnlyDictionary<string, string?> overrides) => Saved = new Dictionary<string, string?>(overrides);
    }

    [Fact]
    public void UnaScorciatoiaConPuntoEVirgolaSiMostraEConfliggeSoloConSeStessa()
    {
        var draft = Draft(Registry(
            Command("ledger.insert.currentTime", "Ctrl+Shift+;", ModuleKind.Sheets),
            Command("ledger.empty", null, ModuleKind.Sheets),
            Command("shell.empty", null, ModuleKind.Shell)));

        Assert.Equal("⇧⌘;", GestureNotation.ForSystem(macOS: true).Display("Ctrl+Shift+;"));
        Assert.Empty(draft.ConflictsOf("ledger.insert.currentTime"));
        Assert.Empty(draft.ConflictsOf("ledger.empty"));
    }
}
