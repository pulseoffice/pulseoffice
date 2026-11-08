using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckPanelCommands(
    IActiveDeckContext context,
    ISidePanelHost panel) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("deck.view.navigator", _ =>
        {
            panel.Show("navigator");
            return Task.CompletedTask;
        }, "Ctrl+Shift+F5"),
        Command("deck.review.panel", _ =>
        {
            panel.Show("review");
            return Task.CompletedTask;
        }),
        Command("deck.view.colorBar", _ =>
        {
            Context.Target?.Options.ToggleColorBar();
            return Task.CompletedTask;
        }),
    ];

    private CommandDefinition Command(string id, Func<CancellationToken, Task> run, string? gesture = null) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            DefaultGesture = gesture,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => run(ct),
        };





}
