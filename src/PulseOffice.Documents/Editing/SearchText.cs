using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public sealed class SearchText
{
    private readonly List<DocumentPosition> _starts = [];
    private readonly List<DocumentPosition> _ends = [];
    private readonly List<ParagraphBlock?> _paragraphs = [];
    private readonly List<TextRun?> _runs = [];
    private readonly List<SearchBreak?> _breaks = [];
    private readonly StringBuilder _text = new();

    public string Text { get; private set; } = string.Empty;

    public string? CommentId { get; init; }

    internal void Add(char character, DocumentPosition start, DocumentPosition end, ParagraphBlock? paragraph, TextRun? run, SearchBreak? kind = null)
    {
        _text.Append(character);
        _starts.Add(start);
        _ends.Add(end);
        _paragraphs.Add(paragraph);
        _runs.Add(run);
        _breaks.Add(kind);
    }

    internal SearchText Seal()
    {
        Text = _text.ToString();
        return this;
    }

    public bool IsMark(int index) => _paragraphs[index] is null;

    public ParagraphBlock? ParagraphAt(int index) => _paragraphs[index];

    public TextRun? RunAt(int index) => _runs[index];

    public SearchMatch Match(int start, int end, System.Text.RegularExpressions.Match? pattern = null)
    {
        var breaks = new List<SearchBreakAt>();
        for (var i = start; i < end; i++)
        {
            if (_breaks[i] is { } kind) breaks.Add(new SearchBreakAt(_starts[i].ParagraphId, kind));
        }
        return new SearchMatch(new DocumentRange(_starts[start], _ends[end - 1]), Text[start..end])
        {
            Breaks = breaks,
            CommentId = CommentId,
            Pattern = pattern,
        };
    }
}

public interface ISearchTextBuilder
{
    SearchText Build(SearchStory story);
}

public sealed class SearchTextBuilder : ISearchTextBuilder
{
    public SearchText Build(SearchStory story)
    {
        ArgumentNullException.ThrowIfNull(story);
        var text = new SearchText { CommentId = story.CommentId };
        ParagraphBlock? previous = null;
        foreach (var paragraph in story.Paragraphs)
        {
            var at = new DocumentPosition(paragraph.Id, 0);
            if (previous is not null) text.Add(SearchMarks.Paragraph, new DocumentPosition(previous.Id, previous.Length), at, null, null);
            if (paragraph.Content.PageBreakBefore == true) text.Add(SearchMarks.Page, at, at, null, null, SearchBreak.Page);
            if (paragraph.Content.ColumnBreakBefore == true) text.Add(SearchMarks.Column, at, at, null, null, SearchBreak.Column);
            if (previous is not null && paragraph.Content.PageStyleName is not null) text.Add(SearchMarks.Section, at, at, null, null, SearchBreak.Section);
            AddCharacters(text, paragraph);
            previous = paragraph;
        }
        return text.Seal();
    }

    private void AddCharacters(SearchText text, ParagraphBlock paragraph)
    {
        var characters = paragraph.Text;
        var offset = 0;
        TextRun? last = null;
        foreach (var run in paragraph.Content.EffectiveRuns())
        {
            last = run;
            for (var i = 0; i < run.Text.Length && offset < characters.Length; i++, offset++)
            {
                if (run.Hidden == true) continue;
                text.Add(characters[offset], new DocumentPosition(paragraph.Id, offset), new DocumentPosition(paragraph.Id, offset + 1), paragraph, run);
            }
        }
        for (; offset < characters.Length; offset++)
            text.Add(characters[offset], new DocumentPosition(paragraph.Id, offset), new DocumentPosition(paragraph.Id, offset + 1), paragraph, last);
    }
}
