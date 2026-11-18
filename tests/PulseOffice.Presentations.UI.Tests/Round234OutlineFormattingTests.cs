using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round234OutlineFormattingTests
{
    private readonly Presentation _deck = new();
    private readonly UndoStack _history = new();
    private readonly DeckActions _actions;
    private readonly Slide _slide;

    public Round234OutlineFormattingTests()
    {
        _deck.Master = new MasterFactory().CreateStandard(_deck.Theme);
        _actions = new DeckActions(_deck, _history, new SlideFactory(), new FakeTextCatalog());
        _slide = new SlideFactory().Create(_deck.NextId(), _deck.Master, SlideLayout.TitleAndContent);
        _deck.AddExisting(_slide);
        var body = _slide.Placeholder(PlaceholderRole.Body)!;
        _slide.Replace(body with
        {
            Text = body.Text with
            {
                Paragraphs =
                [
                    new TextParagraph("Alpha") { Runs = [new TextRun("Alpha") { Bold = true }] },
                    new TextParagraph("Beta") { Runs = [new TextRun("Beta") { Italic = true }] },
                ],
            },
        });
        var title = _slide.Placeholder(PlaceholderRole.Title)!;
        _slide.Replace(title with { Text = title.Text with { Paragraphs = [new TextParagraph("Main") { Bold = true }, new TextParagraph("Sub")] } });
    }

    [Fact]
    public void Inserting_a_line_before_a_paragraph_keeps_the_formatting_of_each_paragraph()
    {
        _actions.SetSlideOutline([(_slide, "Main\nSub", [new OutlineLine(0, "New"), new OutlineLine(0, "Alpha"), new OutlineLine(0, "Beta!")])]);

        var paragraphs = _slide.Placeholder(PlaceholderRole.Body)!.Text.Paragraphs;
        Assert.Equal(["New", "Alpha", "Beta!"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.True(paragraphs[1].Runs.Single().Bold);
        Assert.True(paragraphs[2].EffectiveRuns().All(run => run.Italic));
    }

    [Fact]
    public void A_title_with_several_paragraphs_is_one_outline_line_and_keeps_its_paragraphs()
    {
        var outline = new DeckOutlineText();
        var text = outline.Render(_deck.Slides);
        Assert.StartsWith("Main ¶ Sub", text, StringComparison.Ordinal);

        var entry = outline.Parse(text.Replace("Sub", "Second", StringComparison.Ordinal))[0];
        _actions.SetSlideOutline([(_slide, entry.Title, entry.Body)]);

        var title = _slide.Placeholder(PlaceholderRole.Title)!.Text.Paragraphs;
        Assert.Equal(["Main", "Second"], title.Select(paragraph => paragraph.Text));
        Assert.True(title[0].Bold);
    }
}
