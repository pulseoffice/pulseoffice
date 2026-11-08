using PulseOffice.Document.Comparison;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.Revisions;

public interface IDocumentComparison
{
    int Compare(TextDocument current, TextDocument original, UndoStack history, RevisionSignature signature);

    int Merge(TextDocument current, TextDocument other, UndoStack history, RevisionSignature signature);
}

public sealed class DocumentComparison(
    IBlockTree tree,
    IStoryCatalog stories,
    IParagraphText text,
    ISequenceDiff diff,
    ITextTokens tokens,
    IChangeMarking marking,
    ITrackedEditing tracked) : IDocumentComparison
{
    private sealed record Gap(int OldStart, int NewStart, List<int> Deleted, List<int> Inserted)
    {
        public int Pairs => Math.Min(Deleted.Count, Inserted.Count);
    }

    private sealed record WordEdit(int OldStart, int OldEnd, int NewStart, int NewEnd);

    public int Compare(TextDocument current, TextDocument original, UndoStack history, RevisionSignature signature)
    {
        var mine = tree.Paragraphs(current.Body).ToList();
        var theirs = tree.Paragraphs(original.Body).ToList();
        var gaps = Gaps(theirs, mine);
        if (gaps.Count == 0) return 0;
        var before = current.Changes.All.Count;
        var recording = current.Changes.IsRecording;
        current.Changes.IsRecording = false;
        try
        {
            var recorder = new EditRecorder(current, "compare-documents");
            foreach (var gap in gaps)
            {
                for (var t = 0; t < gap.Pairs; t++) MarkWords(current, recorder, mine[gap.Inserted[t]], theirs[gap.Deleted[t]], signature);
                foreach (var index in gap.Inserted.Skip(gap.Pairs)) MarkParagraph(current, recorder, mine, index, signature);
                var gone = gap.Deleted.Skip(gap.Pairs).Select(index => theirs[index]).ToList();
                if (gone.Count > 0) MarkGone(current, recorder, mine, gap.NewStart + gap.Inserted.Count, gone, signature);
            }
            recorder.Commit(history);
        }
        finally
        {
            current.Changes.IsRecording = recording;
        }
        return current.Changes.All.Count - before;
    }

    public int Merge(TextDocument current, TextDocument other, UndoStack history, RevisionSignature signature)
    {
        var mine = tree.Paragraphs(current.Body).ToList();
        var theirs = tree.Paragraphs(other.Body).ToList();
        var gaps = Gaps(mine, theirs);
        if (gaps.Count == 0) return 0;
        var before = current.Changes.All.Count;
        using var transaction = history.BeginTransaction("merge-documents");
        foreach (var gap in Enumerable.Reverse(gaps))
        {
            var removed = gap.Deleted.Skip(gap.Pairs).Select(index => mine[index]).ToList();
            if (removed.Count > 0) RemoveParagraphs(current, history, mine, removed, signature);
            var added = gap.Inserted.Skip(gap.Pairs).Select(index => theirs[index].Text).ToList();
            if (added.Count > 0) AddParagraphs(current, history, mine, gap.OldStart + gap.Pairs - 1, gap.OldStart + gap.Pairs, added, signature);
            for (var t = gap.Pairs - 1; t >= 0; t--) MergeWords(current, history, mine[gap.Deleted[t]], theirs[gap.Inserted[t]].Text, signature);
        }
        return current.Changes.All.Count - before;
    }

    private List<Gap> Gaps(IReadOnlyList<ParagraphBlock> before, IReadOnlyList<ParagraphBlock> after)
    {
        var steps = diff.Compare(before.Select(paragraph => paragraph.Text).ToList(), after.Select(paragraph => paragraph.Text).ToList(), StringComparer.Ordinal);
        var gaps = new List<Gap>();
        Gap? open = null;
        foreach (var step in steps)
        {
            if (step.Operation == DiffOperation.Equal)
            {
                open = null;
                continue;
            }
            if (open is null)
            {
                open = new Gap(step.OldIndex, step.NewIndex, [], []);
                gaps.Add(open);
            }
            if (step.Operation == DiffOperation.Deleted) open.Deleted.Add(step.OldIndex);
            else open.Inserted.Add(step.NewIndex);
        }
        return gaps;
    }

    private IReadOnlyList<WordEdit> Words(string before, string after)
    {
        var old = tokens.Split(before);
        var fresh = tokens.Split(after);
        var steps = diff.Compare(old, fresh, StringComparer.Ordinal);
        var edits = new List<WordEdit>();
        var oldAt = 0;
        var newAt = 0;
        WordEdit? open = null;
        foreach (var step in steps)
        {
            switch (step.Operation)
            {
                case DiffOperation.Equal:
                    if (open is not null) edits.Add(open);
                    open = null;
                    oldAt += old[step.OldIndex].Length;
                    newAt += fresh[step.NewIndex].Length;
                    break;
                case DiffOperation.Deleted:
                    open ??= new WordEdit(oldAt, oldAt, newAt, newAt);
                    oldAt += old[step.OldIndex].Length;
                    open = open with { OldEnd = oldAt };
                    break;
                default:
                    open ??= new WordEdit(oldAt, oldAt, newAt, newAt);
                    newAt += fresh[step.NewIndex].Length;
                    open = open with { NewEnd = newAt };
                    break;
            }
        }
        if (open is not null) edits.Add(open);
        return edits;
    }

    private void MarkWords(TextDocument current, EditRecorder recorder, ParagraphBlock mine, ParagraphBlock theirs, RevisionSignature signature)
    {
        foreach (var edit in Words(theirs.Text, mine.Text))
        {
            if (edit.OldEnd > edit.OldStart)
            {
                var runs = text.Slice(theirs.State, edit.OldStart, edit.OldEnd);
                var removed = new ParagraphBlock(current.NextId("p"), theirs.Content.WithRuns(runs));
                marking.AddDeletion(current, recorder, new DocumentPosition(mine.Id, edit.NewStart), new BlockContainer([removed]), signature);
            }
            if (edit.NewEnd > edit.NewStart)
                marking.AddInsertion(current, recorder, new DocumentPosition(mine.Id, edit.NewStart), new DocumentPosition(mine.Id, edit.NewEnd), signature);
        }
    }

    private void MarkParagraph(TextDocument current, EditRecorder recorder, IReadOnlyList<ParagraphBlock> mine, int index, RevisionSignature signature)
    {
        var paragraph = mine[index];
        if (index > 0 && Siblings(current, mine[index - 1], paragraph))
            marking.AddInsertion(current, recorder, new DocumentPosition(mine[index - 1].Id, mine[index - 1].Length), new DocumentPosition(paragraph.Id, paragraph.Length), signature);
        else if (index + 1 < mine.Count && Siblings(current, paragraph, mine[index + 1]))
            marking.AddInsertion(current, recorder, new DocumentPosition(paragraph.Id, 0), new DocumentPosition(mine[index + 1].Id, 0), signature);
        else if (paragraph.Length > 0)
            marking.AddInsertion(current, recorder, new DocumentPosition(paragraph.Id, 0), new DocumentPosition(paragraph.Id, paragraph.Length), signature);
    }

    private void MarkGone(TextDocument current, EditRecorder recorder, IReadOnlyList<ParagraphBlock> mine, int next, IReadOnlyList<ParagraphBlock> gone, RevisionSignature signature)
    {
        var copies = gone.Select(paragraph => (Block)new ParagraphBlock(current.NextId("p"), paragraph.Content)).ToList();
        if (next < mine.Count)
        {
            marking.AddDeletion(current, recorder, new DocumentPosition(mine[next].Id, 0),
                new BlockContainer([.. copies, new ParagraphBlock(current.NextId("p"), string.Empty)]), signature);
            return;
        }
        if (mine.Count == 0) return;
        var last = mine[^1];
        marking.AddDeletion(current, recorder, new DocumentPosition(last.Id, last.Length),
            new BlockContainer([new ParagraphBlock(current.NextId("p"), string.Empty), .. copies]), signature);
    }

    private void MergeWords(TextDocument current, UndoStack history, ParagraphBlock mine, string theirs, RevisionSignature signature)
    {
        foreach (var edit in Words(mine.Text, theirs).Reverse())
        {
            var inserted = theirs[edit.NewStart..edit.NewEnd];
            if (inserted.Length > 0) tracked.InsertText(current, history, new DocumentPosition(mine.Id, edit.OldEnd), inserted, signature);
            if (edit.OldEnd > edit.OldStart)
                tracked.Delete(current, history, DocumentRange.In(mine.Id, edit.OldStart, edit.OldEnd), signature);
        }
    }

    private void AddParagraphs(TextDocument current, UndoStack history, IReadOnlyList<ParagraphBlock> mine, int previous, int next, IReadOnlyList<string> added, RevisionSignature signature)
    {
        if (previous >= 0 && previous < mine.Count)
        {
            var anchor = mine[previous];
            tracked.InsertText(current, history, new DocumentPosition(anchor.Id, anchor.Length), "\n" + string.Join("\n", added), signature);
        }
        else if (next >= 0 && next < mine.Count)
        {
            tracked.InsertText(current, history, new DocumentPosition(mine[next].Id, 0), string.Join("\n", added) + "\n", signature);
        }
    }

    private void RemoveParagraphs(TextDocument current, UndoStack history, IReadOnlyList<ParagraphBlock> mine, IReadOnlyList<ParagraphBlock> removed, RevisionSignature signature)
    {
        var first = removed[0];
        var last = removed[^1];
        var index = mine.ToList().IndexOf(first);
        var after = mine.ToList().IndexOf(last) + 1;
        if (index > 0 && Siblings(current, mine[index - 1], last))
        {
            var previous = mine[index - 1];
            tracked.Delete(current, history, new DocumentRange(new DocumentPosition(previous.Id, previous.Length), new DocumentPosition(last.Id, last.Length)), signature);
        }
        else if (after < mine.Count && Siblings(current, first, mine[after]))
        {
            tracked.Delete(current, history, new DocumentRange(new DocumentPosition(first.Id, 0), new DocumentPosition(mine[after].Id, 0)), signature);
        }
    }

    private bool Siblings(TextDocument document, ParagraphBlock first, ParagraphBlock second) =>
        ReferenceEquals(stories.Find(document, first.Id).Location.Container, stories.Find(document, second.Id).Location.Container);
}
