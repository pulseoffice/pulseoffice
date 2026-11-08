using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public abstract class DeckCommandSet(IActiveDeckContext context)
{
    protected IActiveDeckContext Context => context;

    protected CommandDefinition Prototype(string id) => new()
    {
        Id = new CommandId(id),
        Module = ModuleKind.Presentations,
        Execute = (_, _) => Task.CompletedTask,
    };

    protected CommandDefinition InTableCells(CommandDefinition command, IDeckTableCellEditing cells, Action<DeckTarget?> edit)
    {
        var enabled = command.EnabledWhen;
        var execute = command.Execute;
        return command with
        {
            EnabledWhen = ctx => (enabled?.Invoke(ctx) ?? true) || cells.Available(Context.Target),
            Execute = (ctx, ct) =>
            {
                if (!cells.Available(Context.Target)) return execute(ctx, ct);
                edit(Context.Target);
                return Task.CompletedTask;
            },
        };
    }

    protected bool Holds(DeckTarget target, Slide slide) => target.Deck.IndexOf(slide) >= 0;

    protected bool Holds(DeckTarget target, Slide slide, SlideElement element) =>
        Holds(target, slide) && ReferenceEquals(slide.Find(element.Id), element);
}
