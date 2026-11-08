using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Commands;

public sealed class RibbonProjection(CommandRegistry registry, IRibbonDisplayMap displays)
{
    public RibbonProjection(CommandRegistry registry) : this(registry, RibbonDisplayMap.None)
    {
    }

    public RibbonModel Build(CommandContext context)
    {
        var tabs = new List<RibbonTab>();
        var contextual = new List<RibbonTab>();

        foreach (var tabNode in registry.BuildRibbon(context))
        {
            var groups = tabNode.Children
                .Select(groupNode => BuildGroup(groupNode, context))
                .OfType<RibbonGroup>()
                .ToList();

            if (groups.Count == 0) continue;

            var tab = BuildTab(tabNode.Segment, groups);

            if (tab.IsContextual && !tab.Groups.Any(group => group.Items.Any(item => item.IsEnabled)))
                continue;

            (tab.IsContextual ? contextual : tabs).Add(tab);
        }

        return new RibbonModel([.. tabs, .. contextual], context.Module);
    }

    private RibbonGroup? BuildGroup(CommandNode node, CommandContext context)
    {
        var items = node.Children
            .Select(child => child.Command is not null ? Item(child, context) : Menu(child, context))
            .OfType<RibbonItem>()
            .ToList();

        return items.Count == 0 ? null : new RibbonGroup(node.Segment, items);
    }

    private RibbonItem? Menu(CommandNode node, CommandContext context)
    {
        var entries = node.Children
            .Where(child => child.Command is not null)
            .Select(child => Item(child, context))
            .ToList();

        return entries.Count == 0
            ? null
            : new RibbonItem(entries[0].Command, entries.Any(entry => entry.IsEnabled), node.Segment, null)
            {
                Menu = entries,
                Display = RibbonDisplay.Large,
            };
    }

    private RibbonItem Item(CommandNode node, CommandContext context)
    {
        var command = node.Command!;
        var enabled = command.IsEnabled(context);

        return new RibbonItem(command, enabled, node.Segment, registry.DescriptionOf(command))
        {
            IsChecked = command.CheckedWhen is { } check ? enabled && check(context) : null,
            Display = displays.DisplayOf(command.Id.Value),
            Gesture = command.DefaultGesture,
        };
    }

    private static RibbonTab BuildTab(string title, IReadOnlyList<RibbonGroup> groups)
    {
        var commands = groups.SelectMany(g => g.Items).Select(i => i.Command).ToList();
        var contextual = commands.All(c => c.RequiredSelection != SelectionKind.None);

        var owner = commands
            .Select(c => c.Module)
            .FirstOrDefault(m => m != ModuleKind.Shell);

        return new RibbonTab(title, groups)
        {
            IsContextual = contextual,
            AccentHex = contextual && owner != ModuleKind.Shell
                ? ModuleIdentity.For(owner).AccentHex
                : null,
        };
    }
}
