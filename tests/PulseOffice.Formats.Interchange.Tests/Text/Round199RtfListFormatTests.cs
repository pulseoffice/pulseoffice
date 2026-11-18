using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round199RtfListFormatTests
{
    private const string Hebrew = @"{\rtf1\ansi{\*\listtable{\list\listtemplateid7\listsimple0{\listlevel\levelnfc45\levelnfcn45\levelstartat1{\leveltext\'02\'00.;}{\levelnumbers\'01;}}{\listlevel\levelnfc7\levelnfcn7\levelstartat1{\leveltext\'02\'01.;}{\levelnumbers\'01;}}\listid7}}{\*\listoverridetable{\listoverride\listid7\listoverridecount0\ls1}}\pard\ls1\ilvl0 One\par\pard\ls1\ilvl1 Two\par}";

    private readonly TextInterchange _formats = new();

    [Fact]
    public void Extended_level_number_formats_are_read_into_a_list_style()
    {
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(Hebrew), _formats.RtfReader);

        var paragraph = ((ParagraphBlock)document.Body[0]).Content;
        var style = Assert.IsType<ListStyle>(document.Styles.List.Find(paragraph.ListStyleName));
        Assert.Equal(NumberFormat.Hebrew, style.Level(0).Format);
        Assert.Equal(NumberFormat.OrdinalText, style.Level(1).Format);
        Assert.Equal(paragraph.ListStyleName, ((ParagraphBlock)document.Body[1]).Content.ListStyleName);
    }

    [Fact]
    public void Extended_formats_survive_an_rtf_round_trip()
    {
        var first = TextInterchange.Read(Encoding.ASCII.GetBytes(Hebrew), _formats.RtfReader);
        var second = TextInterchange.Read(TextInterchange.Bytes(first, _formats.RtfWriter), _formats.RtfReader);

        var style = second.Styles.List.Find(((ParagraphBlock)second.Body[0]).Content.ListStyleName)!;
        Assert.Equal(NumberFormat.Hebrew, style.Level(0).Format);
        Assert.Equal(NumberFormat.OrdinalText, style.Level(1).Format);
    }

    [Fact]
    public void Hiragana_lists_are_written_as_katakana_and_declared()
    {
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(Hebrew), _formats.RtfReader);
        var style = document.Styles.List.Find(((ParagraphBlock)document.Body[0]).Content.ListStyleName)!;
        style.Levels = [new ListLevel(NumberFormat.Hiragana)];

        Assert.Contains(_formats.RtfWriter.CheckFidelity(document).Losses, loss => loss.Code == "RTF007");
        var text = Encoding.ASCII.GetString(TextInterchange.Bytes(document, _formats.RtfWriter));
        Assert.Contains(@"\levelnfc20", text, StringComparison.Ordinal);
    }
}
