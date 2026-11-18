using PulseOffice.Document.Metadata;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round176KeywordTextTests
{
    [Fact]
    public void The_properties_dialog_text_keeps_a_keyword_holding_a_comma()
    {
        var text = new KeywordListText();

        Assert.Equal(["Rome, Italy", "travel"], text.Split(text.Join(["Rome, Italy", "travel"])));
        Assert.Equal(["a", "b"], text.Split(text.Join(["a", "b"])));
        Assert.Empty(text.Split(string.Empty));
    }
}
