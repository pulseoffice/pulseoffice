using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Commands.Shortcuts;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public sealed class PaletteSearchTests
{
    private readonly FakeTextCatalog _texts = new();

    private CommandDefinition Command(string id, string title, string? keywords = null, string? menuPath = null)
    {
        _texts.Add($"command.{id}.title", title);
        if (keywords is not null) _texts.Add($"command.{id}.keywords", keywords);

        return new CommandDefinition
        {
            Id = new CommandId(id),
            MenuPath = menuPath,
            Surface = CommandSurface.Palette,
            Module = ModuleKind.Presentations,
            Execute = (_, _) => Task.CompletedTask,
        };
    }

    private CommandRegistry Registry(params CommandDefinition[] commands)
    {
        var registry = new CommandRegistry(_texts);
        registry.RegisterRange(commands);
        return registry;
    }

    private static CommandContext Deck() => new(ModuleKind.Presentations, SelectionKind.None, DocumentState.DocumentOpen);

    [Fact]
    public void Un_sinonimo_di_piu_parole_trova_il_comando_anche_con_le_preposizioni()
    {
        var registry = Registry(
            Command("deck.slideSize", "Formato diapositiva…", "misura della diapositiva, proporzioni"),
            Command("deck.slide.add", "Nuova diapositiva"));

        var results = registry.Search("Misura della diapositiva", Deck()).ToList();

        Assert.Equal("deck.slideSize", results[0].Id.Value);
    }

    [Fact]
    public void La_ricerca_ignora_maiuscole_e_accenti()
    {
        var registry = Registry(Command("deck.present", "Présentation à l'écran"));

        Assert.Single(registry.Search("presentation a l'ecran", Deck()));
        Assert.Single(registry.Search("ÉCRAN", Deck()));
    }

    [Fact]
    public void La_categoria_e_la_radice_del_menu_tradotta()
    {
        _texts.Add("menu.format", "Formato");
        var registry = Registry(Command("deck.slideSize", "Formato diapositiva…", menuPath: "format/slide"));

        var presented = registry.Present(registry.Find(new CommandId("deck.slideSize"))!);

        Assert.Equal("Formato", presented.Category);
        Assert.Equal("format", registry.CategoryKeyOf(presented.Command));
    }

    [Theory]
    [InlineData(true, "Ctrl+Shift+P", new[] { "⇧", "⌘", "P" })]
    [InlineData(false, "Ctrl+Shift+P", new[] { "Ctrl", "Shift", "P" })]
    [InlineData(false, "Ctrl+Plus", new[] { "Ctrl", "+" })]
    public void La_scorciatoia_si_scompone_in_tasti(bool macOS, string gesture, string[] keys) =>
        Assert.Equal(keys, GestureNotation.ForSystem(macOS).Keys(gesture));
}
