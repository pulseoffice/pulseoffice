using DocumentFormat.OpenXml;
using PulseOffice.Documents;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxFieldRangeScan(DocxFieldInstructions fields)
{
    private sealed class Open(W.FieldChar begin, W.Paragraph? paragraph)
    {
        public W.FieldChar Begin { get; } = begin;

        public W.Paragraph? Paragraph { get; } = paragraph;

        public bool Separated { get; set; }

        public bool Ranged { get; set; }

        public bool Holds { get; set; }

        public bool ResultHere { get; set; }

        public System.Text.StringBuilder Instruction { get; } = new();
    }

    public IReadOnlySet<W.FieldChar> Ranges(OpenXmlElement root)
    {
        var found = new HashSet<W.FieldChar>(ReferenceEqualityComparer.Instance);
        var open = new Stack<Open>();
        foreach (var element in root.Descendants())
        {
            if (element is W.Text { Text.Length: > 0 } && open.TryPeek(out var showing) && showing.Separated && !showing.ResultHere && !showing.Ranged)
            {
                if (ReferenceEquals(element.Ancestors<W.Paragraph>().FirstOrDefault(), showing.Paragraph) && !Skipped(element)) showing.ResultHere = true;
                continue;
            }
            if (element is not (W.FieldChar or W.FieldCode) || Skipped(element)) continue;
            if (element is W.FieldCode code)
            {
                if (open.TryPeek(out var coding) && !coding.Separated) coding.Instruction.Append(code.Text);
                continue;
            }
            var fieldChar = (W.FieldChar)element;
            var paragraph = fieldChar.Ancestors<W.Paragraph>().FirstOrDefault();
            var type = fieldChar.FieldCharType?.Value;
            if (type == W.FieldCharValues.Begin)
            {
                if (open.TryPeek(out var outer) && outer.Separated) outer.Holds = true;
                open.Push(new Open(fieldChar, paragraph));
            }
            else if (type == W.FieldCharValues.Separate && open.TryPeek(out var separated))
            {
                separated.Separated = true;
                if (!ReferenceEquals(paragraph, separated.Paragraph)) separated.Ranged = true;
            }
            else if (type == W.FieldCharValues.End && open.TryPop(out var closed))
            {
                var spans = !ReferenceEquals(paragraph, closed.Paragraph);
                if ((closed.Holds || closed.Ranged || spans && !Kept(closed)) && Rangeable(closed)) found.Add(closed.Begin);
            }
        }
        return found;
    }

    private bool Kept(Open field) =>
        field.ResultHere && fields.Parse(field.Instruction.ToString()) is { Kind: not PulseOffice.Text.Rich.TextFieldKind.Preserved };

    private bool Rangeable(Open field)
    {
        var instruction = field.Instruction.ToString();
        return field.Begin.FormFieldData is null
               && !fields.Structural(instruction)
               && fields.HyperlinkTarget(instruction) is null
               && !instruction.TrimStart().StartsWith("XE ", StringComparison.OrdinalIgnoreCase);
    }

    private bool Skipped(OpenXmlElement element) =>
        element.Ancestors().Any(ancestor => ancestor is W.DeletedRun or W.MoveFromRun or AlternateContentFallback);
}

public sealed class DocxOpenFieldRange(string id, string? data, bool locked, int offset)
{
    public string Id { get; } = id;

    public string? Data { get; } = data;

    public bool Locked { get; } = locked;

    public bool InCode { get; set; } = true;

    public int Depth { get; set; } = 1;

    public FieldCodePart Pending { get; set; } = FieldCodePart.Begin;

    public int PendingOffset { get; set; } = offset;

    public System.Text.StringBuilder Code { get; } = new();
}

public sealed class DocxFieldRangeState
{
    private readonly Dictionary<OpenXmlElement, IReadOnlySet<W.FieldChar>> _roots = new(ReferenceEqualityComparer.Instance);

    public Stack<DocxOpenFieldRange> Open { get; } = new();

    public DocxOpenFieldRange? Coding => Open.TryPeek(out var top) && top.InCode ? top : null;

    public IReadOnlySet<W.FieldChar> RangesOf(OpenXmlElement root, Func<OpenXmlElement, IReadOnlySet<W.FieldChar>> scan)
    {
        if (!_roots.TryGetValue(root, out var ranges)) _roots[root] = ranges = scan(root);
        return ranges;
    }
}

public sealed class DocxFieldRangeReader(DocxFieldRangeScan scan)
{
    public bool Begins(W.FieldChar begin, DocxReadContext context, int offset)
    {
        var root = begin.Ancestors().LastOrDefault() ?? begin;
        if (!context.FieldRanges.RangesOf(root, scan.Ranges).Contains(begin)) return false;
        var id = context.Document.NextId("field");
        context.FieldRanges.Open.Push(new DocxOpenFieldRange(id, begin.GetFirstChild<W.FieldData>()?.Text, begin.FieldLock?.Value == true, offset));
        return true;
    }

    public IReadOnlyList<FieldCodeMark> Code(OpenXmlElement child, DocxReadContext context, int offset)
    {
        if (context.FieldRanges.Coding is not { } range) return [];
        if (range.PendingOffset < 0) range.PendingOffset = offset;
        switch (child)
        {
            case W.FieldChar { FieldCharType.Value: var type } when type == W.FieldCharValues.Begin:
                range.Code.Append(FieldCodeMark.NestedBegin);
                range.Depth++;
                break;
            case W.FieldChar { FieldCharType.Value: var type } when type == W.FieldCharValues.Separate && range.Depth > 1:
                range.Code.Append(FieldCodeMark.NestedSeparate);
                break;
            case W.FieldChar { FieldCharType.Value: var type } when type == W.FieldCharValues.Separate:
                range.InCode = false;
                return [Pending(range), new FieldCodeMark(offset, range.Id, FieldCodePart.Separate, string.Empty)];
            case W.FieldChar { FieldCharType.Value: var type } when type == W.FieldCharValues.End && range.Depth > 1:
                range.Code.Append(FieldCodeMark.NestedEnd);
                range.Depth--;
                break;
            case W.FieldChar { FieldCharType.Value: var type } when type == W.FieldCharValues.End:
                context.FieldRanges.Open.Pop();
                return [Pending(range), new FieldCodeMark(offset, range.Id, FieldCodePart.End, string.Empty)];
            case W.FieldCode code:
                AppendLiteral(range.Code, code.Text);
                break;
            case W.DeletedFieldCode deleted:
                AppendLiteral(range.Code, deleted.Text);
                break;
            case W.Text text:
                AppendLiteral(range.Code, text.Text);
                break;
            case W.TabChar:
                range.Code.Append('\t');
                break;
        }
        return [];
    }

    public FieldCodeMark? Ends(DocxReadContext context, int offset)
    {
        if (!context.FieldRanges.Open.TryPeek(out var range) || range.InCode) return null;
        context.FieldRanges.Open.Pop();
        return new FieldCodeMark(offset, range.Id, FieldCodePart.End, string.Empty);
    }

    public IReadOnlyList<FieldCodeMark> ParagraphEnd(DocxReadContext context)
    {
        if (context.FieldRanges.Coding is not { } range) return [];
        FieldCodeMark[] marks = range.Pending == FieldCodePart.Begin || range.Code.Length > 0 ? [Pending(range)] : [];
        range.Pending = FieldCodePart.Code;
        range.PendingOffset = -1;
        return marks;
    }

    private void AppendLiteral(System.Text.StringBuilder code, string text)
    {
        foreach (var character in text)
            if (character is not (FieldCodeMark.NestedBegin or FieldCodeMark.NestedSeparate or FieldCodeMark.NestedEnd)) code.Append(character);
    }

    private FieldCodeMark Pending(DocxOpenFieldRange range)
    {
        var mark = new FieldCodeMark(Math.Max(0, range.PendingOffset), range.Id, range.Pending, range.Code.ToString())
        {
            Data = range.Pending == FieldCodePart.Begin ? range.Data : null,
            Locked = range.Pending == FieldCodePart.Begin && range.Locked,
        };
        range.Code.Clear();
        range.Pending = FieldCodePart.Code;
        range.PendingOffset = -1;
        return mark;
    }
}

public sealed class DocxFieldCodeWriter(IStoryCatalog stories, PulseOffice.Documents.Blocks.IBlockTree tree)
{
    public IReadOnlyList<W.Run> Write(FieldCodeMark mark, DocxWriteContext context)
    {
        context.BalancedFields ??= Balanced(context.Document);
        if (!context.BalancedFields.Contains(mark.FieldId)) return [];
        if (mark.Part != FieldCodePart.Begin && !context.OpenFields.Contains(mark.FieldId)) return [];
        var runs = new List<W.Run>();
        if (mark.Part == FieldCodePart.Begin)
        {
            if (!context.OpenFields.Add(mark.FieldId)) return [];
            var begin = new W.FieldChar { FieldCharType = W.FieldCharValues.Begin, FieldLock = mark.Locked ? true : null };
            if (mark.Data is { Length: > 0 } data) begin.Append(new W.FieldData(data));
            runs.Add(new W.Run(begin));
        }
        Code(mark, context, runs);
        if (mark.Part is FieldCodePart.Separate or FieldCodePart.End) CloseNested(mark.FieldId, context, runs);
        if (mark.Part == FieldCodePart.Separate) runs.Add(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
        if (mark.Part == FieldCodePart.End)
        {
            runs.Add(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
            context.OpenFields.Remove(mark.FieldId);
            context.FieldNesting.Remove(mark.FieldId);
        }
        return runs;
    }

    private void Code(FieldCodeMark mark, DocxWriteContext context, List<W.Run> runs)
    {
        if (mark.Code.Length == 0) return;
        if (!context.FieldNesting.TryGetValue(mark.FieldId, out var nesting)) context.FieldNesting[mark.FieldId] = nesting = new Stack<bool>();
        var buffer = new System.Text.StringBuilder();
        void Flush()
        {
            if (buffer.Length == 0) return;
            OpenXmlElement piece = nesting.Count > 0 && nesting.Peek()
                ? new W.Text(buffer.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                : new W.FieldCode(buffer.ToString()) { Space = SpaceProcessingModeValues.Preserve };
            runs.Add(new W.Run(piece));
            buffer.Clear();
        }
        foreach (var character in mark.Code)
        {
            switch (character)
            {
                case FieldCodeMark.NestedBegin:
                    Flush();
                    runs.Add(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }));
                    nesting.Push(false);
                    break;
                case FieldCodeMark.NestedSeparate when nesting.Count > 0:
                    Flush();
                    runs.Add(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
                    nesting.Pop();
                    nesting.Push(true);
                    break;
                case FieldCodeMark.NestedEnd when nesting.Count > 0:
                    Flush();
                    runs.Add(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
                    nesting.Pop();
                    break;
                case FieldCodeMark.NestedSeparate or FieldCodeMark.NestedEnd:
                    break;
                default:
                    buffer.Append(character);
                    break;
            }
        }
        Flush();
    }

    private void CloseNested(string fieldId, DocxWriteContext context, List<W.Run> runs)
    {
        if (!context.FieldNesting.TryGetValue(fieldId, out var nesting)) return;
        while (nesting.TryPop(out _)) runs.Add(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
    }

    private HashSet<string> Balanced(TextDocument document)
    {
        var balanced = new HashSet<string>(StringComparer.Ordinal);
        var broken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var story in stories.Stories(document))
        {
            var open = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mark in tree.Paragraphs(story).SelectMany(paragraph => paragraph.Marks.OrderBy(item => item.Offset)).OfType<FieldCodeMark>())
            {
                if (mark.Part == FieldCodePart.Begin && (balanced.Contains(mark.FieldId) || !open.Add(mark.FieldId))) broken.Add(mark.FieldId);
                if (mark.Part == FieldCodePart.End && open.Remove(mark.FieldId)) balanced.Add(mark.FieldId);
            }
            broken.UnionWith(open);
        }
        balanced.ExceptWith(broken);
        return balanced;
    }
}
