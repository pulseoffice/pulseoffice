using System.Globalization;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public enum ReferenceTargetKind
{
    Bookmarks,
    Headings,
    Captions,
    Footnotes,
    Endnotes,
}

public sealed record ReferenceTarget(ReferenceTargetKind Kind, string Key, string Title);

public interface IReferenceTargets
{
    IReadOnlyList<string> Sequences(TextDocument document);

    IReadOnlyList<ReferenceTarget> Targets(TextDocument document, ReferenceTargetKind kind, string? sequence = null);

    string Prepare(TextDocument document, EditRecorder recorder, ReferenceTarget target);

    string? Touches(TextDocument document, ReferenceTarget target);
}

public sealed class ReferenceTargets(IBookmarkCatalog bookmarks, IDocumentOutline outline, IDocumentReadingOrder order, IStoryCatalog stories, IFieldRunRewriter rewriter) : IReferenceTargets
{
    private const string HeadingPrefix = "__RefHeading__";

    public IReadOnlyList<string> Sequences(TextDocument document) =>
        [.. order.Paragraphs(document).SelectMany(paragraph => paragraph.Content.Runs)
            .Where(run => run.Field == TextFieldKind.Sequence && run.FieldArgument is { Length: > 0 })
            .Select(run => run.FieldArgument!).Distinct(StringComparer.Ordinal)];

    public IReadOnlyList<ReferenceTarget> Targets(TextDocument document, ReferenceTargetKind kind, string? sequence = null) => kind switch
    {
        ReferenceTargetKind.Bookmarks => [.. bookmarks.Bookmarks(document).Where(bookmark => !bookmark.Name.StartsWith('_'))
            .Select(bookmark => new ReferenceTarget(kind, bookmark.Name, bookmark.Name))],
        ReferenceTargetKind.Headings => [.. outline.Headings(document).Select(heading =>
            new ReferenceTarget(kind, heading.Paragraph.Id, new string(' ', (heading.Level - 1) * 2) + heading.Text))],
        ReferenceTargetKind.Captions => [.. order.Paragraphs(document)
            .Where(paragraph => paragraph.Content.Runs.Any(run => run.Field == TextFieldKind.Sequence && string.Equals(run.FieldArgument, sequence, StringComparison.Ordinal)))
            .Select(paragraph => new ReferenceTarget(kind, paragraph.Id, paragraph.Text))],
        ReferenceTargetKind.Footnotes => Notes(document, NoteKind.Footnote, kind),
        _ => Notes(document, NoteKind.Endnote, kind),
    };

    private IReadOnlyList<ReferenceTarget> Notes(TextDocument document, NoteKind noteKind, ReferenceTargetKind kind) =>
        [.. document.Notes.OfKind(noteKind).Select((note, i) => new ReferenceTarget(kind, note.Id,
            (i + 1).ToString(CultureInfo.CurrentCulture) + ". " + string.Join(' ', note.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text))))];

    public string? Touches(TextDocument document, ReferenceTarget target) => target.Kind switch
    {
        ReferenceTargetKind.Headings => stories.Find(document, target.Key).Paragraph.Marks.OfType<BookmarkMark>().Any(mark => mark.Name == HeadingPrefix + target.Key) ? null : target.Key,
        ReferenceTargetKind.Captions => stories.Find(document, target.Key).Paragraph.Content.Runs.FirstOrDefault(run => run.Field == TextFieldKind.Sequence)?.FieldDetail?.Format is { Length: > 0 } ? null : target.Key,
        _ => null,
    };

    public string Prepare(TextDocument document, EditRecorder recorder, ReferenceTarget target)
    {
        switch (target.Kind)
        {
            case ReferenceTargetKind.Headings:
                var heading = stories.Find(document, target.Key).Paragraph;
                var name = HeadingPrefix + heading.Id;
                if (heading.Marks.OfType<BookmarkMark>().Any(mark => mark.Name == name)) return name;
                recorder.Touch(heading);
                heading.State = heading.State with
                {
                    Marks = [.. heading.Marks, new BookmarkMark(0, name, MarkEdge.Start), new BookmarkMark(heading.Length, name, MarkEdge.End)],
                };
                return name;
            case ReferenceTargetKind.Captions:
                var caption = stories.Find(document, target.Key).Paragraph;
                var runs = caption.Content.Runs.ToList();
                var index = runs.FindIndex(run => run.Field == TextFieldKind.Sequence);
                if (runs[index].FieldDetail?.Format is { Length: > 0 } existing) return existing;
                var taken = bookmarks.SequenceTargets(document).Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
                var number = 0;
                string reference;
                do reference = "Ref" + runs[index].FieldArgument + (number++).ToString(CultureInfo.InvariantCulture);
                while (taken.Contains(reference));
                recorder.Touch(caption);
                runs[index] = runs[index] with { FieldDetail = (runs[index].FieldDetail ?? new FieldDetail()) with { Format = reference } };
                caption.State = rewriter.Rewrite(caption.State, runs);
                return reference;
            default:
                return target.Key;
        }
    }
}
