using Avalonia.Media;
using PulseOffice.UI.Controls.Text;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round253TextDecorationFactoryTests
{
    private readonly TextDecorationFactory _factory = new();

    [Fact]
    public void Each_request_builds_a_fresh_collection()
    {
        var first = _factory.Underline();
        var second = _factory.Underline();

        Assert.NotSame(first, second);
        Assert.NotSame(first[0], second[0]);
        Assert.Equal(TextDecorationLocation.Underline, first[0].Location);
    }

    [Fact]
    public void Create_returns_the_requested_lines_or_nothing()
    {
        Assert.Null(_factory.Create(false, false));
        var all = _factory.Create(true, true, true)!;

        Assert.Equal([TextDecorationLocation.Underline, TextDecorationLocation.Strikethrough, TextDecorationLocation.Overline], all.Select(d => d.Location));
    }
}
