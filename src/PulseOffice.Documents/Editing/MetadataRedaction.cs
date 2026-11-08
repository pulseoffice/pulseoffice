using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Metadata;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public sealed record MetadataRedactionReport(int Properties, int Bookmarks, int Authors, int Deletions = 0, int Versions = 0)
{
    public bool IsEmpty => Properties == 0 && Bookmarks == 0 && Authors == 0 && Deletions == 0 && Versions == 0;
}

public interface IMetadataRedaction
{
    MetadataRedactionReport Redact(TextDocument document, UndoStack history, string neutralAuthor, Func<string, string> text);
}

public sealed class MetadataRedaction(IStoryCatalog stories, IFieldRunRewriter rewriter, IBlockTree tree) : IMetadataRedaction
{
    private const string BookmarkPrefix = "Bookmark";

    private const string LinkPrefix = "#";

    public MetadataRedactionReport Redact(TextDocument document, UndoStack history, string neutralAuthor, Func<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(text);
        var recorder = new EditRecorder(document, "redact-metadata");
        var properties = Properties(document.Properties, recorder);
        var bookmarks = Bookmarks(document, recorder);
        var (authors, deletions) = Changes(document, recorder, neutralAuthor, text);
        var versions = Versions(document, recorder);
        recorder.Commit(history);
        return new MetadataRedactionReport(properties, bookmarks, authors, deletions, versions);
    }

    private int Versions(TextDocument document, EditRecorder recorder)
    {
        var versions = document.Versions;
        var count = versions.Count;
        if (count == 0) return 0;
        recorder.Touch<PulseOffice.Document.Versions.DocumentVersion>(versions, () => versions.All, versions.Replace);
        versions.Clear();
        return count;
    }

    private int RedactDeleted(TrackedChange change, Func<string, string> text)
    {
        if (change is not Deletion deletion) return 0;
        var count = 0;
        foreach (var paragraph in tree.Paragraphs(deletion.Removed).ToList())
        {
            var before = paragraph.Text;
            var after = text(before);
            if (after == before) continue;
            var first = paragraph.Content.Runs.FirstOrDefault() ?? new TextRun(string.Empty);
            paragraph.State = new ParagraphState(paragraph.Content.WithRuns([first with { Text = after, Field = TextFieldKind.None, FieldArgument = null, Hyperlink = null }]), []);
            count++;
        }
        return count;
    }

    private int Properties(DocumentProperties properties, EditRecorder recorder)
    {
        recorder.Touch(properties);
        var count = 0;
        string? Clear(string? value)
        {
            if (value is { Length: > 0 }) count++;
            return null;
        }
        properties.Title = Clear(properties.Title);
        properties.Subject = Clear(properties.Subject);
        properties.Description = Clear(properties.Description);
        properties.Author = Clear(properties.Author);
        properties.LastModifiedBy = Clear(properties.LastModifiedBy);
        if (properties.Keywords.Count > 0)
        {
            count++;
            properties.Keywords.Clear();
        }
        foreach (var name in properties.Custom.Keys.ToList())
        {
            if (properties.KindOf(name) != CustomPropertyKind.Text || properties.Custom[name].Length == 0) continue;
            properties.Custom[name] = string.Empty;
            count++;
        }
        return count;
    }

    private int Bookmarks(TextDocument document, EditRecorder recorder)
    {
        var paragraphs = stories.Paragraphs(document).ToList();
        var names = paragraphs.SelectMany(paragraph => paragraph.Marks).OfType<BookmarkMark>().Select(mark => mark.Name).Distinct(StringComparer.Ordinal).ToList();
        var taken = names.ToHashSet(StringComparer.Ordinal);
        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
        var next = 0;
        foreach (var name in names.Where(Visible))
        {
            string neutral;
            do neutral = BookmarkPrefix + (++next).ToString(CultureInfo.InvariantCulture);
            while (taken.Contains(neutral) && neutral != name);
            if (neutral == name) continue;
            taken.Add(neutral);
            renamed[name] = neutral;
        }
        if (renamed.Count == 0) return 0;
        foreach (var paragraph in paragraphs)
        {
            var marks = paragraph.Marks.Select(mark => mark is BookmarkMark bookmark && renamed.TryGetValue(bookmark.Name, out var name) ? bookmark with { Name = name } : mark).ToList();
            var runs = paragraph.Content.Runs.Select(run => Retarget(run, renamed)).ToList();
            var marksChanged = !marks.SequenceEqual(paragraph.Marks);
            var runsChanged = !runs.SequenceEqual(paragraph.Content.Runs);
            if (!marksChanged && !runsChanged) continue;
            recorder.Touch(paragraph);
            var state = runsChanged ? rewriter.Rewrite(paragraph.State, runs) : paragraph.State;
            paragraph.State = state with { Marks = marks };
        }
        return renamed.Count;
    }

    private bool Visible(string name) => !name.StartsWith('_');

    private TextRun Retarget(TextRun run, IReadOnlyDictionary<string, string> renamed)
    {
        if (run.Field is TextFieldKind.Bookmark or TextFieldKind.Reference && run.FieldArgument is { } argument && renamed.TryGetValue(argument, out var target))
            run = run with { FieldArgument = target };
        if (run.Hyperlink is { } link && link.StartsWith(LinkPrefix, StringComparison.Ordinal) && renamed.TryGetValue(link[LinkPrefix.Length..], out var anchor))
            run = run with { Hyperlink = LinkPrefix + anchor };
        return run;
    }

    private (int Authors, int Deletions) Changes(TextDocument document, EditRecorder recorder, string neutralAuthor, Func<string, string> text)
    {
        var deletions = 0;
        var redacted = new HashSet<string>(StringComparer.Ordinal);
        var comments = document.Comments;
        if (comments.All.Any(comment => comment.Author != neutralAuthor || comment.Initials is not null))
        {
            recorder.Touch<Comment>(comments, () => comments.All, comments.Replace);
            foreach (var comment in comments.All.Where(comment => comment.Author != neutralAuthor && comment.Author.Length > 0)) redacted.Add(comment.Author);
            comments.Replace([.. comments.All.Select(comment => new Comment(comment.Id, neutralAuthor, comment.Date, comment.Content)
            {
                ParentId = comment.ParentId,
                Resolved = comment.Resolved,
            })]);
        }
        var changes = document.Changes;
        if (changes.All.Count > 0)
        {
            recorder.Touch<TrackedChange>(changes, () => changes.All, changes.Replace);
            foreach (var change in changes.All.Where(change => change.Author != neutralAuthor && change.Author.Length > 0)) redacted.Add(change.Author);
            changes.Replace([.. changes.All.Select(change =>
            {
                var copy = change.Clone();
                copy.Author = neutralAuthor;
                deletions += RedactDeleted(copy, text);
                return copy;
            })]);
        }
        return (redacted.Count, deletions);
    }
}
