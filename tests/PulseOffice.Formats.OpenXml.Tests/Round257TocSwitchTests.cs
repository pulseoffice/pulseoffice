using PulseOffice.Documents.Indexes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round257TocSwitchTests
{
    private readonly DocxIndexCodec _codec = new(new IndexStyles(), new DocxIndexSettings());

    [Theory]
    [InlineData(" TOC \\o \\h \\z \\u ", 9)]
    [InlineData(" TOC \\o \"1-4\" \\h \\z \\u ", 4)]
    [InlineData(" TOC \\o \"1-12\" \\h ", 9)]
    [InlineData(" TOC \\h \\z \\u ", 3)]
    public void The_outline_switch_gives_the_levels_Word_uses(string instruction, int levels) =>
        Assert.Equal(levels, _codec.Parse(instruction)!.Levels);
}
