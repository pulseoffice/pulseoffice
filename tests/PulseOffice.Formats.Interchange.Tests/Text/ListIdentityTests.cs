using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Interchange.Text;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class ListIdentityTests
{
    private readonly TextInterchange _formats = new();

    private TextDocument Document(params TextParagraph[] paragraphs)
    {
        var document = _formats.Factory.Create();
        document.Body.Replace([.. paragraphs.Select((paragraph, index) => new ParagraphBlock("p" + index, paragraph))]);
        return document;
    }

    private static TextParagraph Item(string text, string? thread = null, int? restart = null, int level = 0) =>
        new TextParagraph(text) { Runs = [new TextRun(text)], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListId = thread, ListRestart = restart, BulletLevel = level };

    private static TextParagraph Plain(string text) => new(text) { Runs = [new TextRun(text)] };

    private static List<TextParagraph> Paragraphs(TextDocument document) => [.. document.Body.Blocks.OfType<ParagraphBlock>().Select(block => block.Content)];

    private TextDocument Sample() => Document(
        Item("a1"), Item("a2"),
        Plain("between"),
        Item("a3"),
        Item("b1", "list1"),
        Item("a4"),
        Item("c1", restart: 5),
        Item("c2"));

    private static void AssertIdentity(TextDocument read)
    {
        var items = Paragraphs(read).Where(paragraph => paragraph.ListStyleName is not null).ToList();
        Assert.Equal(7, items.Count);
        var main = items[0].ListId;
        Assert.Equal(main, items[1].ListId);
        Assert.Equal(main, items[2].ListId);
        Assert.Null(items[2].ListRestart);
        Assert.NotEqual(main, items[3].ListId);
        Assert.Equal(main, items[4].ListId);
        Assert.Equal(5, items[5].ListRestart);
        Assert.Null(items[6].ListRestart);
        Assert.Equal(items[5].ListId, items[6].ListId);
    }

    [Fact]
    public void Rtf_keeps_continued_separate_and_restarted_lists()
    {
        var bytes = TextInterchange.Bytes(Sample(), _formats.RtfWriter);
        AssertIdentity(TextInterchange.Read(bytes, _formats.RtfReader));
    }

    [Fact]
    public void Html_keeps_continued_separate_and_restarted_lists()
    {
        var bytes = TextInterchange.Bytes(Sample(), _formats.HtmlWriter);
        var html = Encoding.UTF8.GetString(bytes);
        Assert.Contains("<ol start=\"3\">", html, StringComparison.Ordinal);
        Assert.Contains("value=\"5\"", html, StringComparison.Ordinal);
        AssertIdentity(TextInterchange.Read(bytes, _formats.HtmlReader));
    }

    [Fact]
    public void Plain_text_numbers_follow_list_identity()
    {
        var text = Encoding.UTF8.GetString(TextInterchange.Bytes(Sample(), _formats.PlainWriter));
        Assert.Equal("1. a1\n2. a2\nbetween\n3. a3\n1. b1\n4. a4\n5. c1\n6. c2\n", text);
    }

    [Fact]
    public void Html_separate_ordered_lists_restart_and_start_attribute_continues()
    {
        const string html = "<html><body><ol><li>a</li><li>b</li></ol><p>x</p><ol><li>c</li></ol><p>y</p><ol start=\"2\"><li>d</li></ol></body></html>";
        var read = TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader);
        var items = Paragraphs(read).Where(paragraph => paragraph.ListStyleName is not null).ToList();
        Assert.Equal(1, items[2].ListRestart);
        Assert.Equal(items[2].ListId, items[3].ListId);
        Assert.Null(items[3].ListRestart);
    }

    [Fact]
    public void Rtf_override_with_start_restarts_the_list()
    {
        const string rtf = @"{\rtf1\ansi{\*\listtable{\list\listtemplateid1{\listlevel\levelnfc0\levelstartat1}\listid7}}{\*\listoverridetable{\listoverride\listid7\listoverridecount0\ls1}{\listoverride\listid7\listoverridecount1{\lfolevel\listoverridestartat\levelstartat4}\ls2}}\pard\ls1\ilvl0 one\par\pard\ls1\ilvl0 two\par\pard\ls2\ilvl0 four\par\pard\ls2\ilvl0 five\par}";
        var read = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);
        var items = Paragraphs(read);
        Assert.Equal(4, items.Count);
        Assert.Null(items[1].ListRestart);
        Assert.Equal(4, items[2].ListRestart);
        Assert.Null(items[3].ListRestart);
        Assert.All(items, item => Assert.Equal(items[0].ListId, item.ListId));
    }
}
