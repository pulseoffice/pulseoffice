using System.Runtime.CompilerServices;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public enum MergeCommandType
{
    Table,
    Query,
    Command,
}

public sealed record MailMergeSource(string Database, string Command, MergeCommandType Type)
{
    public string? ConnectionId { get; init; }
}

public sealed record MergeData(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    public static readonly MergeData Empty = new([], []);

    public string? Value(int row, string column)
    {
        if (row < 0 || row >= Rows.Count) return null;
        var index = -1;
        for (var i = 0; i < Columns.Count; i++)
            if (string.Equals(Columns[i], column, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        return index < 0 || index >= Rows[row].Count ? null : Rows[row][index];
    }
}

public interface IMergeRecords
{
    MergeData? Data(TextDocument document);

    void Load(TextDocument document, MergeData data);

    int Current(TextDocument document);

    void Move(TextDocument document, int row);

    bool Previewing(TextDocument document);

    void Preview(TextDocument document, bool on);
}

public sealed class MergeRecords : IMergeRecords
{
    private readonly ConditionalWeakTable<TextDocument, State> _states = [];

    public MergeData? Data(TextDocument document) => Find(document)?.Data;

    public void Load(TextDocument document, MergeData data)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        _states.AddOrUpdate(document, new State { Data = data });
    }

    public int Current(TextDocument document) => Find(document)?.Row ?? 0;

    public void Move(TextDocument document, int row)
    {
        if (Find(document) is not { } state) return;
        state.Row = Math.Clamp(row, 0, Math.Max(0, state.Data.Rows.Count - 1));
    }

    public bool Previewing(TextDocument document) => Find(document)?.Preview ?? false;

    public void Preview(TextDocument document, bool on)
    {
        if (Find(document) is { } state) state.Preview = on;
    }

    private State? Find(TextDocument document) => _states.TryGetValue(document, out var state) ? state : null;

    private sealed class State
    {
        public required MergeData Data { get; init; }

        public int Row { get; set; }

        public bool Preview { get; set; }
    }
}

public interface IMergePlaceholder
{
    string For(string column);
}

public sealed class GuillemetPlaceholder : IMergePlaceholder
{
    public string For(string column) => "«" + column + "»";
}

public sealed class MergeFieldSource(IMergeRecords records, IMergePlaceholder placeholder) : IFieldValueSource
{
    public IReadOnlyCollection<TextFieldKind> Kinds { get; } = [TextFieldKind.Database];

    public FieldValue? Evaluate(TextRun run, FieldContext context)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);
        if (run.FieldArgument is not { Length: > 0 } column) return null;
        var document = context.Document;
        if (records.Previewing(document) && records.Data(document) is { } data && data.Value(records.Current(document), column) is { } value)
            return new FieldValue(value);
        return new FieldValue(placeholder.For(column));
    }
}

public interface IMailMergeEngine
{
    IReadOnlyList<string> Fields(TextDocument template);

    TextDocument Record(TextDocument template, MergeData data, int row);

    TextDocument Combine(TextDocument template, MergeData data, IReadOnlyList<int> rows);
}

public sealed class MailMergeEngine(IStoryCatalog stories, IFieldRunRewriter rewriter, IFragmentExtractor extractor, IFragmentPaster paster) : IMailMergeEngine
{
    public IReadOnlyList<string> Fields(TextDocument template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return [.. stories.Paragraphs(template)
            .SelectMany(paragraph => paragraph.Content.Runs)
            .Where(run => run.Field == TextFieldKind.Database && run.FieldArgument is { Length: > 0 })
            .Select(run => run.FieldArgument!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public TextDocument Record(TextDocument template, MergeData data, int row)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);
        var copy = template.Clone();
        copy.MergeSource = null;
        foreach (var paragraph in stories.Paragraphs(copy).ToList())
        {
            var runs = paragraph.Content.Runs;
            if (!runs.Any(run => run.Field == TextFieldKind.Database)) continue;
            var replaced = runs.Select(run => run.Field == TextFieldKind.Database
                ? run with { Text = data.Value(row, run.FieldArgument ?? string.Empty) ?? string.Empty, Field = TextFieldKind.None, FieldArgument = null, FieldDetail = null }
                : run).ToList();
            paragraph.State = rewriter.Rewrite(paragraph.State, replaced);
        }
        return copy;
    }

    public TextDocument Combine(TextDocument template, MergeData data, IReadOnlyList<int> rows)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return Record(template, data, -1);
        var combined = Record(template, data, rows[0]);
        var history = new PulseOffice.Document.Commands.UndoStack();
        foreach (var row in rows.Skip(1))
        {
            var record = Record(template, data, row);
            if (record.Body.Blocks.Count == 0) continue;
            if (record.Body.Blocks[0] is not ParagraphBlock) record.Body.Replace([new ParagraphBlock(record.NextId("p"), string.Empty), .. record.Body.Blocks]);
            if (record.Body.Blocks[^1] is not ParagraphBlock) record.Body.Replace([.. record.Body.Blocks, new ParagraphBlock(record.NextId("p"), string.Empty)]);
            var first = (ParagraphBlock)record.Body.Blocks[0];
            var last = (ParagraphBlock)record.Body.Blocks[^1];
            var fragment = extractor.Copy(record, new DocumentRange(new DocumentPosition(first.Id, 0), new DocumentPosition(last.Id, last.Length)));
            var opener = new ParagraphBlock(combined.NextId("p"), Emptied(first.Content) with { PageBreakBefore = true });
            combined.Body.Replace([.. combined.Body.Blocks, opener]);
            paster.Paste(combined, history, new DocumentPosition(opener.Id, 0), fragment);
        }
        return combined;
    }

    private TextParagraph Emptied(TextParagraph content) => content with { Text = string.Empty, Runs = [] };

}
