using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round268ShapeListKeysTests
{
    private readonly ShapeListKeys _keys = new();

    private static ShapeObject Listed(string text, params ShapeListItem?[] items) =>
        new() { Id = "forma", Shape = ShapeKind.Rectangle, Text = text, Lists = items };

    [Fact]
    public void Tab_and_shift_tab_change_the_level_of_the_item_under_the_caret()
    {
        var shape = Listed("Uno\nDue", new ShapeListItem(), new ShapeListItem());

        var indented = _keys.Press(shape, 4, 4, ShapeListKey.Indent)!;
        var outdented = _keys.Press(indented, 5, 5, ShapeListKey.Outdent)!;

        Assert.Equal([0, 1], indented.Lists!.Select(item => item!.Level));
        Assert.Equal([0, 0], outdented.Lists!.Select(item => item!.Level));
    }

    [Fact]
    public void Tab_outside_a_list_is_not_taken()
    {
        Assert.Null(_keys.Press(Listed("Uno"), 0, 0, ShapeListKey.Indent));
    }

    [Fact]
    public void Enter_on_an_empty_item_outdents_it_then_ends_the_list()
    {
        var shape = Listed("Uno\n", new ShapeListItem(), new ShapeListItem { Level = 1 });

        var outdented = _keys.Press(shape, 4, 4, ShapeListKey.Return)!;
        var ended = _keys.Press(outdented, 4, 4, ShapeListKey.Return)!;

        Assert.Equal(0, outdented.Lists![1]!.Level);
        Assert.Null(ended.Lists!.ElementAtOrDefault(1));
        Assert.Null(_keys.Press(shape, 2, 2, ShapeListKey.Return));
    }

    [Fact]
    public void Backspace_at_the_start_of_an_item_removes_its_bullet_and_keeps_the_text()
    {
        var shape = Listed("Uno\nDue", new ShapeListItem(), new ShapeListItem());

        var removed = _keys.Press(shape, 4, 4, ShapeListKey.Backspace)!;

        Assert.Equal("Uno\nDue", removed.Text);
        Assert.NotNull(removed.Lists![0]);
        Assert.Null(removed.Lists.ElementAtOrDefault(1));
        Assert.Null(_keys.Press(shape, 5, 5, ShapeListKey.Backspace));
    }
}
