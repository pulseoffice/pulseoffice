using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using W = DocumentFormat.OpenXml.Wordprocessing;
using W15 = DocumentFormat.OpenXml.Office2013.Word;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxAnnotationsCodec(DocxBlockWriter writer, DocxBlockReader reader)
{
    public void WriteNotes(MainDocumentPart main, DocxWriteContext context)
    {
        var footnotes = context.Document.Notes.All.Where(note => note.Kind == NoteKind.Footnote).ToList();
        var endnotes = context.Document.Notes.All.Where(note => note.Kind == NoteKind.Endnote).ToList();
        if (footnotes.Count > 0)
        {
            var part = main.AddNewPart<FootnotesPart>();
            var previous = context.Part;
            context.Part = part;
            var root = new W.Footnotes(
                new W.Footnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                new W.Footnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 });
            foreach (var note in footnotes)
            {
                context.OpenInsertion = null;
                context.OpenDeletion = null;
                root.Append(new W.Footnote(writer.Write(note.Content, context)) { Id = context.Footnotes[note.Id] });
            }
            part.Footnotes = root;
            context.Part = previous;
        }

        if (endnotes.Count > 0)
        {
            var part = main.AddNewPart<EndnotesPart>();
            var previous = context.Part;
            context.Part = part;
            var root = new W.Endnotes(
                new W.Endnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                new W.Endnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 });
            foreach (var note in endnotes)
            {
                context.OpenInsertion = null;
                context.OpenDeletion = null;
                root.Append(new W.Endnote(writer.Write(note.Content, context)) { Id = context.Endnotes[note.Id] });
            }
            part.Endnotes = root;
            context.Part = previous;
        }
    }

    public void ReadNotes(MainDocumentPart main, DocxReadContext context)
    {
        var previous = context.Part;
        if (main.FootnotesPart is { Footnotes: { } footnotes } footnotesPart)
        {
            context.Part = footnotesPart;
            ReadNotes(footnotes.Elements<W.Footnote>(), NoteKind.Footnote, context.Footnotes, context,
                main.Document?.Descendants<W.FootnoteReference>().Select(reference => reference.Id?.Value) ?? []);
        }

        if (main.EndnotesPart is { Endnotes: { } endnotes } endnotesPart)
        {
            context.Part = endnotesPart;
            ReadNotes(endnotes.Elements<W.Endnote>(), NoteKind.Endnote, context.Endnotes, context,
                main.Document?.Descendants<W.EndnoteReference>().Select(reference => reference.Id?.Value) ?? []);
        }

        context.Part = previous;
    }

    private void ReadNotes(IEnumerable<W.FootnoteEndnoteType> notes, NoteKind kind, Dictionary<string, string> map, DocxReadContext context, IEnumerable<long?> references)
    {
        var read = new List<(long Id, string Note)>();
        foreach (var note in notes.Where(item => item.Type is null || item.Type.Value == W.FootnoteEndnoteValues.Normal))
        {
            if (note.Id?.Value is not { } id) continue;
            var model = new Note(context.Document.NextId("note"), kind, new BlockContainer(reader.Read(note.ChildElements, context)));
            context.Document.Notes.Add(model);
            map[id.ToString(CultureInfo.InvariantCulture)] = model.Id;
            read.Add((id, model.Id));
        }

        var cited = references.OfType<long>().ToList();
        if (cited.Count != read.Count || cited.Distinct().Count() != cited.Count
            || cited.All(id => map.ContainsKey(id.ToString(CultureInfo.InvariantCulture)))) return;
        map.Clear();
        foreach (var (id, note) in cited.Zip(read.OrderBy(entry => entry.Id).Select(entry => entry.Note)))
            map[id.ToString(CultureInfo.InvariantCulture)] = note;
    }

    public void WriteComments(MainDocumentPart main, DocxWriteContext context)
    {
        var comments = context.Document.Comments.All;
        if (comments.Count == 0) return;
        var part = main.AddNewPart<WordprocessingCommentsPart>();
        var previous = context.Part;
        context.Part = part;
        var root = new W.Comments();
        var paraIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var comment in comments)
        {
            var element = new W.Comment
            {
                Id = context.Comments[comment.Id].ToString(CultureInfo.InvariantCulture),
                Author = comment.Author,
                Date = comment.Date.UtcDateTime,
            };
            if (comment.Initials is not null) element.Initials = comment.Initials;
            context.OpenInsertion = null;
            context.OpenDeletion = null;
            var blocks = writer.Write(comment.Content, context).ToList();
            if (blocks.LastOrDefault() is not W.Paragraph) blocks.Add(new W.Paragraph());
            var last = (W.Paragraph)blocks[^1];
            var paraId = context.Paragraphs[comment.Id].ToString("X8", CultureInfo.InvariantCulture);
            last.ParagraphId = paraId;
            paraIds[comment.Id] = paraId;
            element.Append(blocks);
            root.Append(element);
        }

        part.Comments = root;
        context.Part = previous;
        var extended = main.AddNewPart<WordprocessingCommentsExPart>();
        extended.CommentsEx = new W15.CommentsEx(comments.Select(comment =>
        {
            var entry = new W15.CommentEx { ParaId = paraIds[comment.Id], Done = comment.Resolved };
            if (comment.ParentId is { } parent && paraIds.TryGetValue(parent, out var parentPara)) entry.ParaIdParent = parentPara;
            return entry;
        }));
        var durable = main.AddNewPart<WordprocessingCommentsIdsPart>();
        durable.CommentsIds = new DocumentFormat.OpenXml.Office2019.Word.Cid.CommentsIds(comments.Select(comment => new DocumentFormat.OpenXml.Office2019.Word.Cid.CommentId
        {
            ParaId = paraIds[comment.Id],
            DurableId = (context.Paragraphs[comment.Id] ^ 0x5A5A5A5A).ToString("X8", CultureInfo.InvariantCulture),
        }));
    }

    public void ReadComments(MainDocumentPart main, DocxReadContext context)
    {
        if (main.WordprocessingCommentsPart is not { Comments: { } comments } part) return;
        var previous = context.Part;
        context.Part = part;
        var byPara = new Dictionary<string, Comment>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in comments.Elements<W.Comment>())
        {
            if (element.Id?.Value is not { } id) continue;
            var date = element.Date?.Value is { } value ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)) : DateTimeOffset.MinValue;
            var comment = new Comment(context.Document.NextId("comment"), element.Author?.Value ?? string.Empty, date,
                new BlockContainer(reader.Read(element.ChildElements, context)))
            {
                Initials = element.Initials?.Value,
            };
            context.Document.Comments.Add(comment);
            context.Comments[id] = comment.Id;
            if (element.Elements<W.Paragraph>().LastOrDefault()?.ParagraphId?.Value is { } paraId) byPara[paraId] = comment;
        }

        context.Part = previous;
        if (main.WordprocessingCommentsExPart?.CommentsEx is not { } extended) return;
        foreach (var entry in extended.Elements<W15.CommentEx>())
        {
            if (entry.ParaId?.Value is not { } paraId || !byPara.TryGetValue(paraId, out var comment)) continue;
            comment.Resolved = entry.Done?.Value ?? false;
            if (entry.ParaIdParent?.Value is { } parent && byPara.TryGetValue(parent, out var parentComment)) comment.ParentId = parentComment.Id;
        }
        context.Document.Comments.DetachBrokenReplies();
    }
}
