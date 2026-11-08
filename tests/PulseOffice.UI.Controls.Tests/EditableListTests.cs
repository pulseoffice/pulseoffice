using PulseOffice.UI.Controls.Lists;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class EditableListTests
{
    [Fact]
    public void AddTrimsAndRejectsBlanks()
    {
        var list = new EditableList([" Nord ", "", "  "]);

        Assert.Equal(["Nord"], list.Items);
        Assert.False(list.Add("   "));
        Assert.True(list.Add(" Sud"));
        Assert.Equal(["Nord", "Sud"], list.Items);
    }

    [Fact]
    public void UniqueListRejectsDuplicatesOnAddAndReplace()
    {
        var list = new EditableList(["A", "B"], unique: true, StringComparer.Ordinal);

        Assert.False(list.CanAdd("A"));
        Assert.False(list.Replace(1, "A"));
        Assert.True(list.Replace(1, "C"));
        Assert.Equal(["A", "C"], list.Items);
    }

    [Fact]
    public void MovesStayInsideBounds()
    {
        var list = new EditableList(["1", "2", "3"]);

        Assert.False(list.MoveUp(0));
        Assert.False(list.MoveDown(2));
        Assert.True(list.MoveDown(0));
        Assert.True(list.MoveUp(2));
        Assert.Equal(["2", "3", "1"], list.Items);
    }

    [Fact]
    public void ChangedFiresOnlyOnRealChanges()
    {
        var list = new EditableList(["x"]);
        var count = 0;
        list.Changed += (_, _) => count++;

        list.Replace(0, "x");
        list.Remove(5);
        list.MoveUp(0);
        list.Remove(0);

        Assert.Equal(1, count);
        Assert.Equal(0, list.Count);
    }
}
