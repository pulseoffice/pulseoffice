using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public sealed class TextFieldTests
{
    [Fact]
    public void ResolvesNumberDateTime()
    {
        var fields = new TextFields(7, new DateTime(2026, 9, 29, 14, 5, 0));

        Assert.Equal("7", new TextFieldResolver().Resolve(TextFieldKind.SlideNumber, fields));
        Assert.NotEqual(string.Empty, new TextFieldResolver().Resolve(TextFieldKind.Date, fields));
        Assert.NotEqual(string.Empty, new TextFieldResolver().Resolve(TextFieldKind.Time, fields));
        Assert.Equal(string.Empty, new TextFieldResolver().Resolve(TextFieldKind.None, fields));
    }
}
