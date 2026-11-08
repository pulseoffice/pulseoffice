namespace PulseOffice.UI.Commands;

public sealed record MenuSection(CommandNode Node, bool Inline, int Rows);

public sealed record MenuBlock(string? Title, IReadOnlyList<CommandNode> Nodes);

public interface IMenuPlanner
{
    IReadOnlyList<MenuSection> Plan(CommandNode parent);

    IReadOnlyList<MenuBlock> Blocks(CommandNode parent);
}

public sealed class MenuPlanner(int inlineLimit = 5, int menuLimit = 30, int smallestFoldable = 3) : IMenuPlanner
{
    public IReadOnlyList<MenuSection> Plan(CommandNode parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var sections = parent.Children
            .Where(section => section.Command is not null || section.Children.Count > 0)
            .Select(section => new MenuSection(section, Fits(section), RowsOf(section)))
            .ToList();

        while (Height(sections) > menuLimit && Widest(sections) is { } widest)
            sections[widest] = sections[widest] with { Inline = false };

        return sections;
    }

    public IReadOnlyList<MenuBlock> Blocks(CommandNode parent)
    {
        var blocks = new List<MenuBlock>();
        var run = new List<CommandNode>();

        void Flush()
        {
            if (run.Count == 0) return;

            blocks.Add(new MenuBlock(null, [.. run]));
            run.Clear();
        }

        foreach (var section in Plan(parent))
        {
            if (section.Node.Command is not null || !section.Inline)
            {
                run.Add(section.Node);
                continue;
            }

            Flush();
            blocks.Add(new MenuBlock(Titled(section.Node) ? section.Node.Segment : null, section.Node.Children));
        }

        Flush();
        return blocks;
    }

    private static bool Titled(CommandNode section) => section.Children.Count > 1;

    private bool Fits(CommandNode section) =>
        section.Command is not null
        || (RowsOf(section) <= inlineLimit && section.Children.All(child => child.Command is not null));

    private static int RowsOf(CommandNode section) =>
        section.Command is not null ? 1 : section.Children.Count;

    private static bool HasTitle(MenuSection section) =>
        section.Inline && section.Node.Command is null && Titled(section.Node);

    private static int Height(IReadOnlyList<MenuSection> sections) =>
        sections.Sum(section => section.Inline ? section.Rows + (HasTitle(section) ? 1 : 0) : 1);

    private int? Widest(IReadOnlyList<MenuSection> sections)
    {
        var found = -1;

        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];

            if (!section.Inline || section.Node.Command is not null) continue;
            if (section.Rows < smallestFoldable) continue;
            if (found < 0 || section.Rows > sections[found].Rows) found = index;
        }

        return found < 0 ? null : found;
    }
}
