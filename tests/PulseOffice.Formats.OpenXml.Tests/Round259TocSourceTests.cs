using PulseOffice.Documents.Indexes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round259TocSourceTests
{
    private readonly DocxIndexCodec _codec = new(new IndexStyles(), new DocxIndexSettings());

    [Fact]
    public void The_first_outline_level_is_read_and_written()
    {
        var parsed = _codec.Parse(" TOC \\o \"2-4\" \\h \\z \\u ")!;

        Assert.Equal(2, parsed.FirstLevel);
        Assert.Equal(4, parsed.Levels);
        Assert.Contains("\\o \"2-4\"", _codec.Instruction(parsed), StringComparison.Ordinal);
    }

    [Fact]
    public void Additional_styles_carry_their_levels_both_ways()
    {
        var parsed = _codec.Parse(" TOC \\o \"1-3\" \\t \"Appendix,1,Sub Appendix,2\" \\h ")!;

        Assert.Equal(1, parsed.SourceStyles.LevelOf("Appendix"));
        Assert.Equal(2, parsed.SourceStyles.LevelOf("Sub Appendix"));
        Assert.Equal(parsed.SourceStyles, _codec.Parse(_codec.Instruction(parsed))!.SourceStyles);
    }

    [Fact]
    public void A_toc_of_styles_only_is_not_built_from_the_outline()
    {
        var parsed = _codec.Parse(" TOC \\h \\z \\t \"Title;1\" ")!;

        Assert.False(parsed.FromOutline);
        Assert.Equal(1, parsed.SourceStyles.LevelOf("Title"));
        Assert.DoesNotContain("\\o", _codec.Instruction(parsed), StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_keep_source_styles_and_first_level()
    {
        var settings = new DocxIndexSettings();
        var index = new IndexDefinition(IndexKind.Contents) { FirstLevel = 2, SourceStyles = IndexSourceStyles.None.With("A=b", 3) };

        var decoded = settings.Decode(settings.Encode(index), new IndexDefinition(IndexKind.Contents));

        Assert.Equal(2, decoded.FirstLevel);
        Assert.Equal(3, decoded.SourceStyles.LevelOf("A=b"));
    }
}
