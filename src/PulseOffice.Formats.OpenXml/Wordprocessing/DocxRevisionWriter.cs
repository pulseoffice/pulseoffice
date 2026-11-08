using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxRevisionWriter(IDirectFormatting formatting, DocxCharacterCodec characters, DocxParagraphCodec paragraphs)
{
    public W.InsertedRun Inserted(TrackedChange change, DocxWriteContext context) => new()
    {
        Id = Next(context),
        Author = change.Author,
        Date = change.Date.UtcDateTime,
    };

    public W.DeletedRun DeletedRange(TrackedChange change, DocxWriteContext context) => new()
    {
        Id = Next(context),
        Author = change.Author,
        Date = change.Date.UtcDateTime,
    };

    public W.Inserted InsertedMark(TrackedChange change, DocxWriteContext context) => new()
    {
        Id = Next(context),
        Author = change.Author,
        Date = change.Date.UtcDateTime,
    };

    public W.Deleted DeletedMark(TrackedChange change, DocxWriteContext context) => new()
    {
        Id = Next(context),
        Author = change.Author,
        Date = change.Date.UtcDateTime,
    };

    public IReadOnlyList<(W.DeletedRun Element, string? Hyperlink)> Deleted(Deletion deletion, DocxWriteContext context, DocxInlineWriter inlines, DocxBlockSink blocks)
    {
        var children = new List<(OpenXmlElement Element, string? Hyperlink)>();
        string? link = null;
        var first = true;
        foreach (var paragraph in deletion.Removed.Blocks.OfType<ParagraphBlock>())
        {
            if (!first)
            {
                var separator = new W.Run();
                inlines.AppendText(separator, "\n", deleted: true);
                children.Add((separator, null));
            }

            first = false;
            var marked = paragraph.Marks.OfType<ObjectMark>().Select(mark => mark.ObjectId).ToHashSet(StringComparer.Ordinal);
            foreach (var item in context.Document.Objects.AnchoredTo(paragraph.Id).Where(item => !marked.Contains(item.Id)))
                if (inlines.Drawing(item, context, blocks) is { } leading) children.Add((leading, null));
            var references = paragraph.Marks.SelectMany(mark => References(mark, context, inlines, blocks).Select(element => (mark.Offset, Element: (OpenXmlElement?)element))).ToList();
            var offset = 0;
            var pending = 0;
            foreach (var run in paragraph.Content.Runs)
            {
                var cuts = references.Skip(pending).TakeWhile(item => item.Offset < offset + run.Text.Length).ToList();
                var start = 0;
                link = run.Hyperlink;
                if (run.Field != TextFieldKind.None && cuts.All(item => item.Offset <= offset) && inlines.ComplexField(run, context, deleted: true) is { } field)
                {
                    foreach (var (_, element) in cuts) children.Add((element!, link));
                    foreach (var piece in field) children.Add((piece, link));
                    pending += cuts.Count;
                    offset += run.Text.Length;
                    continue;
                }
                foreach (var (at, element) in cuts)
                {
                    var cut = Math.Clamp(at - offset, start, run.Text.Length);
                    if (cut > start) children.Add((DeletedRun(run, run.Text[start..cut], context, inlines), link));
                    children.Add((element!, link));
                    start = cut;
                }

                pending += cuts.Count;
                if (start < run.Text.Length) children.Add((DeletedRun(run, run.Text[start..], context, inlines), link));
                offset += run.Text.Length;
            }

            foreach (var (_, element) in references.Skip(pending)) children.Add((element!, link));
        }

        var groups = new List<(W.DeletedRun Element, string? Hyperlink)>();
        var index = 0;
        do
        {
            var target = index < children.Count ? children[index].Hyperlink : null;
            var deleted = new W.DeletedRun { Id = Next(context), Author = deletion.Author, Date = deletion.Date.UtcDateTime };
            while (index < children.Count && children[index].Hyperlink == target) deleted.Append(children[index++].Element);
            groups.Add((deleted, target));
        }
        while (index < children.Count);

        return groups;
    }

    private W.Run DeletedRun(TextRun run, string text, DocxWriteContext context, DocxInlineWriter inlines)
    {
        var element = new W.Run();
        var properties = characters.Elements(formatting.Run(run), context.Styles.IdOf(StyleFamily.Character, run.StyleName)).ToList();
        if (properties.Count > 0) element.Append(new W.RunProperties(properties));
        inlines.AppendText(element, text, deleted: true);
        return element;
    }

    private IEnumerable<OpenXmlElement> References(InlineMark mark, DocxWriteContext context, DocxInlineWriter inlines, DocxBlockSink blocks)
    {
        if (mark is CommentMark { Edge: MarkEdge.Start or MarkEdge.End } edge && context.Document.Comments.Find(edge.CommentId) is { IsReply: false } held)
        {
            var id = context.Comments[held.Id].ToString(CultureInfo.InvariantCulture);
            return edge.Edge == MarkEdge.Start
                ? [new W.CommentRangeStart { Id = id }]
                : [new W.CommentRangeEnd { Id = id }, new W.Run(new W.CommentReference { Id = id })];
        }
        return Reference(mark, context, inlines, blocks) is { } single ? [single] : [];
    }

    private OpenXmlElement? Reference(InlineMark mark, DocxWriteContext context, DocxInlineWriter inlines, DocxBlockSink blocks) => mark switch
    {
        NoteMark note when context.Document.Notes.Find(note.NoteId) is { Kind: NoteKind.Endnote } endnote
            => new W.Run(new W.EndnoteReference { Id = context.Endnotes[endnote.Id] }),
        NoteMark note when context.Document.Notes.Find(note.NoteId) is { } footnote
            => new W.Run(new W.FootnoteReference { Id = context.Footnotes[footnote.Id] }),
        CommentMark comment when context.Document.Comments.Find(comment.CommentId) is { IsReply: false } found
            => new W.Run(new W.CommentReference { Id = context.Comments[found.Id].ToString(CultureInfo.InvariantCulture) }),
        ObjectMark item when context.Document.Objects.Find(item.ObjectId) is { } anchored => inlines.Drawing(anchored, context, blocks),
        _ => null,
    };

    public W.RunPropertiesChange FormatChange(FormatChange change, TextRun previous, DocxWriteContext context) =>
        new(new W.PreviousRunProperties(characters.Elements(formatting.Run(previous), context.Styles.IdOf(StyleFamily.Character, previous.StyleName))))
        {
            Id = Next(context),
            Author = change.Author,
            Date = change.Date.UtcDateTime,
        };

    public W.ParagraphPropertiesChange ParagraphChange(ParagraphFormatChange change, DocxWriteContext context)
    {
        var previous = change.Previous;
        var extras = new DocxParagraphExtras(context.Styles.IdOf(StyleFamily.Paragraph, previous.StyleName),
            context.Lists.IdOf(previous.ListStyleName), previous.BulletLevel, previous.OutlineLevel);
        return new W.ParagraphPropertiesChange(new W.ParagraphPropertiesExtended(paragraphs.Elements(formatting.Paragraph(previous), extras)))
        {
            Id = Next(context),
            Author = change.Author,
            Date = change.Date.UtcDateTime,
        };
    }

    private string Next(DocxWriteContext context) =>
        context.Revisions.Fresh().ToString(CultureInfo.InvariantCulture);
}
