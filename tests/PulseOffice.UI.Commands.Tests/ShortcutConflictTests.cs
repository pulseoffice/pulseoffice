using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public class ShortcutConflictTests
{
    private static readonly FakeTextCatalog Texts = new();

    private static CommandDefinition Command(string id, string gesture, ModuleKind module) => new()
    {
        Id = new CommandId(id),
        MenuPath = "file",
        Module = module,
        DefaultGesture = gesture,
        Execute = (_, _) => Task.CompletedTask,
    };

    private static IReadOnlyList<string> Conflicts(params CommandDefinition[] commands)
    {
        var registry = new CommandRegistry(Texts);
        registry.RegisterRange(commands);
        return registry.ConflictingShortcuts();
    }

    [Fact]
    public void UnComandoDiModuloCheCopreUnoDelGuscioEUnConflitto()
    {
        var conflicts = Conflicts(
            Command("shell.palette", "Ctrl+Shift+P", ModuleKind.Shell),
            Command("sheet.preview", "Ctrl+Shift+P", ModuleKind.Sheets));

        Assert.Equal(["Ctrl+Shift+P: sheet.preview, shell.palette"], conflicts);
    }

    [Fact]
    public void DueComandiDelloStessoModuloSonoUnConflitto()
    {
        Assert.Single(Conflicts(
            Command("sheet.macro", "Ctrl+Alt+M", ModuleKind.Sheets),
            Command("sheet.comment", "Ctrl+Alt+M", ModuleKind.Sheets)));
    }

    [Fact]
    public void ModuliDiversiNonSonoMaiAttiviInsieme()
    {
        Assert.Empty(Conflicts(
            Command("sheet.bold", "Ctrl+B", ModuleKind.Sheets),
            Command("deck.bold", "Ctrl+B", ModuleKind.Presentations)));
    }

    [Fact]
    public void LOrdineDeiModificatoriNonConta()
    {
        Assert.Single(Conflicts(
            Command("a", "Ctrl+Shift+K", ModuleKind.Sheets),
            Command("b", "shift+ctrl+k", ModuleKind.Sheets)));
    }

    [Fact]
    public void LoStessoTastoScrittoInDueModiEUnConflitto()
    {
        Assert.Single(Conflicts(
            Command("deck.zoomOut", "Ctrl+Minus", ModuleKind.Presentations),
            Command("deck.softHyphen", "Ctrl+OemMinus", ModuleKind.Presentations)));
        Assert.Single(Conflicts(
            Command("a", "Cmd+S", ModuleKind.Sheets),
            Command("b", "Ctrl+S", ModuleKind.Sheets)));
    }

    [Fact]
    public void IlNomeAvaloniaDelTastoMenoSiLegge()
    {
        var chord = new PulseOffice.UI.Commands.Shortcuts.GestureChordParser().Parse("Ctrl+Shift+OemMinus");

        Assert.Equal("Ctrl+Shift+Minus", chord?.Canonical());
    }
}
