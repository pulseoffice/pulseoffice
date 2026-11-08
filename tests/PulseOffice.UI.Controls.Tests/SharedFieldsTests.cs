using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Lists;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SharedFieldsTests
{
    [Theory]
    [InlineData("#ff0000", "#FF0000")]
    [InlineData("0f0", "#00FF00")]
    [InlineData("#80112233", "#112233")]
    [InlineData("rosso", "")]
    [InlineData("  ", "")]
    public void ColourValuesNormalize(string text, string expected) =>
        Assert.Equal(expected, new ColorFieldValue().Normalize(text));

    [Fact]
    public void EditableListHonoursTheValidator()
    {
        var list = new EditableList(["A1", "bad"], accepts: text => text.StartsWith('A'));
        Assert.Equal(["A1"], list.Items);
        Assert.False(list.CanAdd("zz"));
        Assert.False(list.Replace(0, "zz"));
        Assert.True(list.Add("A2"));
    }
}
