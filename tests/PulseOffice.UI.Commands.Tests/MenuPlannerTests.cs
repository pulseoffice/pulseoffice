using Xunit;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Commands.Tests;

public sealed class MenuPlannerTests
{
    private static CommandNode Leaf(string name) =>
        new(name, new CommandDefinition { Id = new CommandId(name), Execute = (_, _) => Task.CompletedTask }, []);

    private static CommandNode Section(string name, int items) =>
        new(name, null, [.. Enumerable.Range(1, items).Select(index => Leaf($"{name}{index}"))]);

    private static CommandNode Top(params CommandNode[] sections) => new("root", null, sections);

    [Fact]
    public void ShortSectionsStayInline()
    {
        var plan = new MenuPlanner().Plan(Top(Section("a", 3), Section("b", 5)));

        Assert.All(plan, section => Assert.True(section.Inline));
    }

    [Fact]
    public void LongSectionBecomesSubmenu()
    {
        var plan = new MenuPlanner().Plan(Top(Section("a", 3), Section("b", 12)));

        Assert.True(plan[0].Inline);
        Assert.False(plan[1].Inline);
    }

    [Fact]
    public void WidestSectionFoldsFirstWhenTheMenuIsTooTall()
    {
        var plan = new MenuPlanner(inlineLimit: 20, menuLimit: 12)
            .Plan(Top(Section("a", 5), Section("b", 4), Section("c", 3)));

        Assert.False(plan[0].Inline);
        Assert.True(plan[1].Inline);
        Assert.True(plan[2].Inline);
    }

    [Fact]
    public void TinySectionsAreNeverFolded()
    {
        var plan = new MenuPlanner(inlineLimit: 20, menuLimit: 1)
            .Plan(Top(Section("a", 2), Section("b", 2)));

        Assert.All(plan, section => Assert.True(section.Inline));
    }

    [Fact]
    public void ASectionOfSectionsStaysASubmenu()
    {
        var nested = new CommandNode("outer", null, [Section("a", 2), Section("b", 2)]);

        Assert.False(new MenuPlanner().Plan(Top(nested))[0].Inline);
    }

    [Fact]
    public void BlocksKeepLeavesTogetherAndSeparateInlineSections()
    {
        var leaf = Leaf("x");
        var blocks = new MenuPlanner().Blocks(Top(leaf, leaf, Section("a", 3), Section("b", 12)));

        Assert.Equal(3, blocks.Count);
        Assert.Equal(2, blocks[0].Nodes.Count);
        Assert.Equal("a", blocks[1].Title);
        Assert.Null(blocks[2].Title);
    }

    [Fact]
    public void ACommandDirectlyUnderTheRootIsAlwaysARow()
    {
        var leaf = Leaf("x");
        var plan = new MenuPlanner(inlineLimit: 1, menuLimit: 1).Plan(Top(leaf));

        Assert.True(plan[0].Inline);
        Assert.Equal(1, plan[0].Rows);
    }

    [Fact]
    public void UnGruppoVuotoNonProduceUnaSezione()
    {
        var empty = new CommandNode("vuoto", null, []);

        var plan = new MenuPlanner().Plan(Top(Section("a", 3), empty));

        Assert.Single(plan);
    }
}
