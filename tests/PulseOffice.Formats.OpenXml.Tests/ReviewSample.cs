using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ReviewSample
{
    private static readonly DateTimeOffset When = new(2026, 10, 10, 9, 30, 0, TimeSpan.Zero);

    public TextDocument Build()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([]);
        var first = new ParagraphBlock("p1", new TextParagraph("Uno due") { Runs = [new TextRun("Uno "), new TextRun("due") { Bold = true }] })
        {
            Marks =
            [
                new ChangeMark(0, "ct1", MarkEdge.Point),
                new ChangeMark(4, "ct2", MarkEdge.Start),
                new ChangeMark(7, "ct2", MarkEdge.End),
                new ChangeMark(7, "ct3", MarkEdge.Start),
                new CommentMark(0, "c1", MarkEdge.Start),
                new CommentMark(3, "c1", MarkEdge.End),
            ],
        };
        var second = new ParagraphBlock("p2", new TextParagraph("tre quattro") { Runs = [new TextRun("tre quattro")] })
        {
            Marks = [new ChangeMark(3, "ct3", MarkEdge.End), new ChangeMark(4, "ct4", MarkEdge.Point)],
        };
        document.Body.Replace([first, second]);
        document.Changes.Add(new ParagraphFormatChange("ct1", "Anna", When, new TextParagraph("Uno due") { StyleName = DefaultStyleNames.Standard }));
        document.Changes.Add(new FormatChange("ct2", "Anna", When, [new TextRun("due")]));
        document.Changes.Add(new Insertion("ct3", "Bruno", When));
        document.Changes.Add(new Deletion("ct4", "Bruno", When, new BlockContainer([new ParagraphBlock("px", "cinque ")])));
        document.Changes.IsRecording = true;
        document.Comments.Add(new Comment("c1", "Carla", When, new BlockContainer([new ParagraphBlock("cp1", "Perché uno?")])));
        document.Comments.Add(new Comment("c2", "Anna", When.AddMinutes(5), new BlockContainer([new ParagraphBlock("cp2", "Così è.")])) { ParentId = "c1", Resolved = true });
        document.Protection = new DocumentProtection(DocumentRestriction.Comments, null);
        return document;
    }

}
