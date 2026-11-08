using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public sealed class GestureKeyTests
{
    private readonly GestureKey _key = new();

    [Fact]
    public void LOrdineDeiModificatoriNonConta() =>
        Assert.Equal(_key.Canonical("Ctrl+Shift+Z"), _key.Canonical("shift+ctrl+z"));

    [Fact]
    public void IlTastoPiuNonSiPerde() =>
        Assert.NotEqual(_key.Canonical("Ctrl++"), _key.Canonical("Ctrl"));

    [Fact]
    public void IlComandoDelModuloVinceAncheConModificatoriInAltroOrdine()
    {
        var registry = new CommandRegistry(new FakeTextCatalog());
        registry.Register(new CommandDefinition
        {
            Id = new CommandId("shell.a"), MenuPath = "file", DefaultGesture = "Ctrl+Shift+Z",
            Execute = (_, _) => Task.CompletedTask,
        });
        registry.Register(new CommandDefinition
        {
            Id = new CommandId("ledger.a"), MenuPath = "file", DefaultGesture = "Shift+Ctrl+Z", Module = ModuleKind.Sheets,
            Execute = (_, _) => Task.CompletedTask,
        });

        var gestures = registry.ActiveGestures(new CommandContext(ModuleKind.Sheets, SelectionKind.None, DocumentState.None));

        var only = Assert.Single(gestures);
        Assert.Equal("ledger.a", only.Value.Id.Value);
    }
}
