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

public sealed record ReferenceTarget(ReferenceTargetKind Kind, string Key, string Title)
{
    public string? Sequence { get; init; }
}

public interface IReferenceTargets
{
    IReadOnlyList<string> Sequences(TextDocument document);

    IReadOnlyList<ReferenceTarget> Targets(TextDocument document, ReferenceTargetKind kind, string? sequence = null);

    string Prepare(TextDocument document, EditRecorder recorder, ReferenceTarget target);

    string? Touches(TextDocument document, ReferenceTarget target);

    string? HeadingOf(TextDocument document, string bookmarkName);

    string HeadingAnchor(TextDocument document, ParagraphBlock heading);
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
            .Select(paragraph => new ReferenceTarget(kind, paragraph.Id, paragraph.Text) { Sequence = sequence })],
        ReferenceTargetKind.Footnotes => Notes(document, NoteKind.Footnote, kind),
        _ => Notes(document, NoteKind.Endnote, kind),
    };

    private IReadOnlyList<ReferenceTarget> Notes(TextDocument document, NoteKind noteKind, ReferenceTargetKind kind) =>
        [.. document.Notes.OfKind(noteKind).Select((note, i) => new ReferenceTarget(kind, note.Id,
            (i + 1).ToString(CultureInfo.CurrentCulture) + ". " + string.Join(' ', note.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text))))];

    public string? Touches(TextDocument document, ReferenceTarget target) => target.Kind switch
    {
        ReferenceTargetKind.Headings => HeadingBookmark(stories.Find(document, target.Key).Paragraph) is null ? target.Key : null,
        ReferenceTargetKind.Captions => CaptionNamed(stories.Find(document, target.Key).Paragraph.Content.Runs, target.Sequence) ? null : target.Key,
        _ => null,
    };

    public string? HeadingOf(TextDocument document, string bookmarkName) =>
        bookmarkName.StartsWith(HeadingPrefix, StringComparison.Ordinal) && bookmarks.Find(document, bookmarkName) is { } bookmark ? bookmark.ParagraphId : null;

    private bool CaptionNamed(IReadOnlyList<TextRun> runs, string? sequence) =>
        SequenceIndex(runs, sequence) is var index && index >= 0 && runs[index].FieldDetail?.Format is { Length: > 0 };

    private int SequenceIndex(IReadOnlyList<TextRun> runs, string? sequence)
    {
        for (var index = 0; index < runs.Count; index++)
            if (runs[index].Field == TextFieldKind.Sequence && string.Equals(runs[index].FieldArgument, sequence, StringComparison.Ordinal)) return index;
        for (var index = 0; index < runs.Count; index++)
            if (runs[index].Field == TextFieldKind.Sequence) return index;
        return -1;
    }

    public string HeadingAnchor(TextDocument document, ParagraphBlock heading) =>
        HeadingBookmark(heading) ?? FreeHeadingName(document, heading);

    private string FreeHeadingName(TextDocument document, ParagraphBlock heading)
    {
        var used = bookmarks.Bookmarks(document).Select(bookmark => bookmark.Name).ToHashSet(StringComparer.Ordinal);
        var name = HeadingPrefix + heading.Id;
        for (var suffix = 1; used.Contains(name); suffix++)
            name = HeadingPrefix + heading.Id + "_" + suffix.ToString(CultureInfo.InvariantCulture);
        return name;
    }

    private string? HeadingBookmark(ParagraphBlock heading) =>
        heading.Marks.OfType<BookmarkMark>().FirstOrDefault(mark => mark.Edge == MarkEdge.Start && mark.Offset == 0 && mark.Name.StartsWith(HeadingPrefix, StringComparison.Ordinal))?.Name;

    public string Prepare(TextDocument document, EditRecorder recorder, ReferenceTarget target)
    {
        switch (target.Kind)
        {
            case ReferenceTargetKind.Headings:
                var heading = stories.Find(document, target.Key).Paragraph;
                if (HeadingBookmark(heading) is { } existing) return existing;
                var name = FreeHeadingName(document, heading);
                recorder.Touch(heading);
                heading.State = heading.State with
                {
                    Marks = [.. heading.Marks, new BookmarkMark(0, name, MarkEdge.Start), new BookmarkMark(heading.Length, name, MarkEdge.End)],
                };
                return name;
            case ReferenceTargetKind.Captions:
                var caption = stories.Find(document, target.Key).Paragraph;
                var runs = caption.Content.Runs.ToList();
                var index = SequenceIndex(runs, target.Sequence);
                if (index < 0) return target.Key;
                if (runs[index].FieldDetail?.Format is { Length: > 0 } captionName) return captionName;
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
