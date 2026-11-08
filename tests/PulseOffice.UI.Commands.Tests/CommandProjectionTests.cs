using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public class CommandProjectionTests
{
    private static CommandDefinition Command(
        string id, string title,
        string? menuPath = null, string? ribbonPath = null,
        CommandSurface surface = CommandSurface.Common,
        ModuleKind module = ModuleKind.Shell,
        SelectionKind selection = SelectionKind.None,
        DocumentState required = DocumentState.None,
        DocumentState forbidden = DocumentState.None)
    {
        Texts.Add($"command.{id}.title", title);

        return new CommandDefinition
        {
            Id = new CommandId(id),
            MenuPath = menuPath,
            RibbonPath = ribbonPath,
            Surface = surface,
            Module = module,
            RequiredSelection = selection,
            RequiredState = required,
            ForbiddenState = forbidden,
            Execute = (_, _) => Task.CompletedTask,
        };
    }

    private static readonly FakeTextCatalog Texts = new();

    private static CommandRegistry RegistryWith(params CommandDefinition[] commands)
    {
        var registry = new CommandRegistry(Texts);
        registry.RegisterRange(commands);
        return registry;
    }

    private static CommandContext Context(
        ModuleKind module = ModuleKind.Shell,
        SelectionKind selection = SelectionKind.None,
        DocumentState state = DocumentState.None) =>
        new(module, selection, state);

    [Fact]
    public void LeChiaviDiStrutturaSonoQuelleCheIMenuUsanoDavvero()
    {
        var comando = Command("import", "Importa", menuPath: "data/external", ribbonPath: "data/external") with
        {
            ContextPaths = ["cell/data"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
        };

        var registry = RegistryWith(comando);
        var chiavi = registry.StructureKeys();

        Assert.Equal(
            ["context.cell", "context.cell.data", "menu.data", "menu.data.external", "ribbon.data", "ribbon.data.external"],
            chiavi);

        var nodi = registry.BuildContextMenu(Context(), "cell");

        Assert.All(
            nodi.SelectMany(nodo => nodo.Children).Select(gruppo => "context.cell." + gruppo.Segment),
            chiave => Assert.Contains(chiave, chiavi));
    }

    [Fact]
    public void UnComandoDuplicatoEUnErrore()
    {
        var registry = RegistryWith(Command("a", "Primo"));
        Assert.Throws<InvalidOperationException>(() => registry.Register(Command("a", "Secondo")));
    }

    [Fact]
    public void IlMenuRispettaLaGerarchiaDelPercorso()
    {
        var registry = RegistryWith(
            Command("new", "Nuovo", menuPath: "File/Nuovo/Documento"),
            Command("open", "Apri", menuPath: "File/Apri"));

        var menu = registry.BuildMenu(Context());

        var file = Assert.Single(menu);
        Assert.Equal("File", file.Segment);
        Assert.True(file.IsGroup);

        Assert.Contains(file.Children, n => n.Segment == "Nuovo" && n.IsGroup);
        Assert.Contains(file.Children, n => n.Segment == "Apri" && n.IsGroup);
    }

    [Fact]
    public void UnComandoSenzaPercorsoNonCompareNelMenu()
    {
        var registry = RegistryWith(Command("hidden", "Nascosto", ribbonPath: "Home/Gruppo"));
        Assert.Empty(registry.BuildMenu(Context()));
    }

    [Fact]
    public void LeSuperficiFiltranoIComandi()
    {
        var registry = RegistryWith(
            Command("common", "Comune", menuPath: "File/Comune", ribbonPath: "Home/Gruppo"),
            Command("advanced", "Avanzato", menuPath: "File/Avanzato", surface: CommandSurface.Advanced));

        Assert.Equal(2, registry.Applicable(Context(), CommandSurface.Menu).Count());
        Assert.Single(registry.Applicable(Context(), CommandSurface.Ribbon));
    }

    [Fact]
    public void UnComandoDiUnAltroModuloNonEApplicabile()
    {
        var registry = RegistryWith(
            Command("sheets", "Solo fogli", menuPath: "Dati/Ordina", module: ModuleKind.Sheets));

        Assert.Empty(registry.Applicable(Context(ModuleKind.Documents)));
        Assert.Single(registry.Applicable(Context(ModuleKind.Sheets)));
    }

    [Fact]
    public void GliStatiRichiestiEProibitiSiCombinano()
    {
        var save = Command("save", "Salva", menuPath: "File/Salva",
            required: DocumentState.DocumentOpen, forbidden: DocumentState.ReadOnly);

        Assert.False(save.IsApplicable(Context()));
        Assert.True(save.IsApplicable(Context(state: DocumentState.DocumentOpen)));

        Assert.False(save.IsApplicable(
            Context(state: DocumentState.DocumentOpen | DocumentState.ReadOnly)));
    }

    [Fact]
    public void UnaSchedaSenzaComandiApplicabiliNonVieneProdotta()
    {
        var registry = RegistryWith(
            Command("chart", "Formato grafico", ribbonPath: "Grafico/Formato",
                    selection: SelectionKind.Chart, module: ModuleKind.Sheets));

        var projection = new RibbonProjection(registry);

        Assert.True(projection.Build(Context(ModuleKind.Sheets)).IsEmpty);

        var withChart = projection.Build(Context(ModuleKind.Sheets, SelectionKind.Chart));
        var tab = Assert.Single(withChart.Tabs);
        Assert.Equal("Grafico", tab.Title);
    }

    [Fact]
    public void UnaSchedaEContestualeSoloSeTuttiIComandiRichiedonoUnaSelezione()
    {
        var registry = RegistryWith(
            Command("bold", "Grassetto", ribbonPath: "Home/Carattere"),
            Command("cut", "Taglia", ribbonPath: "Home/Appunti", selection: SelectionKind.TextRange),
            Command("chartStyle", "Stile", ribbonPath: "Grafico/Stili",
                    selection: SelectionKind.Chart, module: ModuleKind.Sheets));

        var projection = new RibbonProjection(registry);
        var model = projection.Build(Context(ModuleKind.Sheets, SelectionKind.Chart | SelectionKind.TextRange));

        var home = model.Tabs.Single(t => t.Title == "Home");
        var chart = model.Tabs.Single(t => t.Title == "Grafico");

        Assert.False(home.IsContextual);
        Assert.True(chart.IsContextual);
    }

    [Fact]
    public void LaSchedaContestualeEreditaLAccentoDelModulo()
    {
        var registry = RegistryWith(
            Command("chartStyle", "Stile", ribbonPath: "Grafico/Stili",
                    selection: SelectionKind.Chart, module: ModuleKind.Sheets));

        var model = new RibbonProjection(registry).Build(
            Context(ModuleKind.Sheets, SelectionKind.Chart));

        Assert.Equal(ModuleIdentity.Sheets.AccentHex, Assert.Single(model.Tabs).AccentHex);
    }

    [Fact]
    public void LaRicercaRichiedeCheOgniTermineCorrisponda()
    {
        var registry = RegistryWith(
            Command("cf", "Formattazione condizionale", menuPath: "Formato/Condizionale",
                    surface: CommandSurface.Palette),
            Command("cell", "Formato celle", menuPath: "Formato/Celle",
                    surface: CommandSurface.Palette));

        var results = registry.Search("formato condizionale", Context()).ToList();

        Assert.Single(results);
        Assert.Equal("Formattazione condizionale", registry.TitleOf(results[0]));
    }

    [Fact]
    public void AParitaDiScorciatoiaVinceIlComandoDelModulo()
    {
        var registry = RegistryWith(
            Command("shellFind", "Trova", menuPath: "Modifica/Trova"),
            Command("sheetsFind", "Trova nel foglio", menuPath: "Modifica/Trova",
                    module: ModuleKind.Sheets));

        var shellCommand = registry.Find(new CommandId("shellFind"))!;
        var sheetsCommand = registry.Find(new CommandId("sheetsFind"))!;

        var registryWithGestures = new CommandRegistry(Texts);
        registryWithGestures.Register(shellCommand with { DefaultGesture = "Ctrl+F" });
        registryWithGestures.Register(sheetsCommand with { DefaultGesture = "Ctrl+F" });

        var gestures = registryWithGestures.ActiveGestures(Context(ModuleKind.Sheets));
        Assert.Equal("sheetsFind", gestures["Ctrl+F"].Id.Value);
    }
}
