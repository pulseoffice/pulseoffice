using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Commands.Shortcuts;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class CommandPaletteViewTests
{
    private static CommandPresentation Item(string id, string title, string? gesture = null, string? category = null) =>
        new(new CommandDefinition { Id = new CommandId(id), Execute = (_, _) => Task.CompletedTask }, title, null, null, gesture)
        {
            Category = category,
        };

    private static readonly IGestureNotation Notation = GestureNotation.ForSystem(macOS: false);

    private static (CommandPaletteView Palette, Window Window) Open(Func<string, IReadOnlyList<CommandPaletteSection>> sections)
    {
        var palette = new CommandPaletteView(sections, new IconCatalog(), Notation, "Cerca", "Nessun comando corrisponde");
        var window = new Window { Content = palette, Width = 700, Height = 520 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (palette, window);
    }

    [Fact]
    public Task Senza_risultati_la_tavolozza_lo_dice() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (palette, window) = Open(_ => []);

        Assert.True(palette.ShowsNoMatches);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task A_tavolozza_vuota_i_recenti_stanno_in_testa_e_le_frecce_saltano_le_intestazioni() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (palette, window) = Open(query => query.Length > 0
            ? [new CommandPaletteSection(null, [Item("b", "Bordi")])]
            :
            [
                new CommandPaletteSection("Recenti", [Item("a", "Apri")]),
                new CommandPaletteSection("Formato", [Item("b", "Bordi"), Item("c", "Colore")]),
            ]);

        Assert.IsType<CommandPaletteHeader>(palette.Entries[0]);
        Assert.Equal("a", palette.Selected?.Command.Id.Value);

        palette.Move(1);
        Assert.Equal("b", palette.Selected?.Command.Id.Value);

        palette.Move(-1);
        Assert.Equal("a", palette.Selected?.Command.Id.Value);

        palette.Query = "bor";
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(palette.Entries, entry => entry is CommandPaletteHeader);
        Assert.Equal("b", palette.Selected?.Command.Id.Value);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_scorciatoia_e_disegnata_in_tasti_e_Invio_sceglie() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (palette, window) = Open(_ => [new CommandPaletteSection(null, [Item("p", "Stampa…", "Ctrl+P", "File")])]);
        object? chosen = null;
        palette.Completed += (_, value) => chosen = value;

        var caps = window.GetVisualDescendants().OfType<KeyCapsView>().Single();
        Assert.Equal(["Ctrl", "P"], caps.Keys);

        palette.Commit();
        Assert.Equal("p", Assert.IsType<CommandDefinition>(chosen).Id.Value);
        window.Close();
    }, CancellationToken.None);
}
