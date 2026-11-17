using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round151NotesBreakTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void A_line_break_inside_a_notes_paragraph_is_read_as_a_new_line()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Notes = "first";

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        using (var document = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(buffer, true))
        {
            var notes = document.PresentationPart!.SlideParts.First().NotesSlidePart!.NotesSlide!;
            var paragraph = notes.Descendants<D.Paragraph>().First(p => p.Descendants<D.Text>().Any(t => t.Text == "first"));
            var end = paragraph.GetFirstChild<D.EndParagraphRunProperties>();
            if (end is null)
            {
                paragraph.Append(new D.Break());
                paragraph.Append(new D.Run(new D.Text("second")));
            }
            else
            {
                paragraph.InsertBefore(new D.Break(), end);
                paragraph.InsertBefore(new D.Run(new D.Text("second")), end);
            }
            notes.Save();
        }

        buffer.Position = 0;
        var back = new PptxReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal("first\nsecond", back.Slides[0].Notes);
    }
}
