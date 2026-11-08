using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Rendering;

public interface IFolioSpotlight
{
    bool ParagraphStyles { get; set; }

    bool CharacterStyles { get; set; }

    bool DirectFormatting { get; set; }

    event EventHandler? Changed;

    PageMarks Apply(PageMarks marks, TextDocument document);
}

public sealed class FolioSpotlight(PulseOffice.Documents.Editing.IStoryCatalog stories) : IFolioSpotlight
{
    private const uint Direct = 0xFFF59E0B;

    private readonly uint[] _palette = [0xFF2563EB, 0xFF16A34A, 0xFFDC2626, 0xFF9333EA, 0xFF0891B2, 0xFFDB2777, 0xFF65A30D, 0xFFEA580C];

    public bool ParagraphStyles { get; set => Set(ref field, value); }

    public bool CharacterStyles { get; set => Set(ref field, value); }

    public bool DirectFormatting { get; set => Set(ref field, value); }

    public event EventHandler? Changed;

    public PageMarks Apply(PageMarks marks, TextDocument document)
    {
        if (!ParagraphStyles && !CharacterStyles && !DirectFormatting) return marks;
        var paragraphs = stories.Paragraphs(document).GroupBy(paragraph => paragraph.Id).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return marks with
        {
            ParagraphSpotlight = ParagraphStyles ? id => paragraphs.TryGetValue(id, out var paragraph) && paragraph.StyleName is { } name ? Color(name) : null : null,
            RunSpotlight = CharacterStyles || DirectFormatting ? (id, offset) => paragraphs.TryGetValue(id, out var paragraph) ? Run(paragraph, offset) : null : null,
        };
    }

    private uint? Run(ParagraphBlock paragraph, int offset)
    {
        var position = 0;
        foreach (var run in paragraph.Content.Runs)
        {
            if (offset < position + run.Text.Length || run == paragraph.Content.Runs[^1])
            {
                if (DirectFormatting && Directly(run)) return Direct;
                return CharacterStyles && run.StyleName is { } style ? Color(style) : null;
            }
            position += run.Text.Length;
        }
        return null;
    }

    private bool Directly(TextRun run) =>
        run with { Text = string.Empty } != new TextRun(string.Empty)
        {
            Field = run.Field,
            FieldArgument = run.FieldArgument,
            FieldDetail = run.FieldDetail,
            StyleName = run.StyleName,
            Language = run.Language,
            Hyperlink = run.Hyperlink,
            Explicit = run.Explicit,
        };

    private uint Color(string name) => _palette[(int)((uint)name.Aggregate(17, (hash, character) => unchecked(hash * 31 + character)) % (uint)_palette.Length)];

    private void Set(ref bool field, bool value)
    {
        if (field == value) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public interface IFolioFieldNames
{
    bool Show { get; set; }

    event EventHandler? Changed;

    PageMarks Apply(PageMarks marks);
}

public sealed class FolioFieldNames(PulseOffice.Localization.ITextCatalog texts, Commands.FolioFieldCatalog catalog) : IFolioFieldNames
{
    public bool Show
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;

    public PageMarks Apply(PageMarks marks) =>
        Show ? marks with { FieldName = run => Name(run) } : marks;

    private string Name(TextRun run)
    {
        var choice = catalog.Choices.FirstOrDefault(entry => entry.Kind == run.Field && entry.Argument == run.FieldArgument)
            ?? catalog.Choices.FirstOrDefault(entry => entry.Kind == run.Field);
        var name = choice is null ? run.Field.ToString() : texts["folio.field.kind." + choice.Key];
        return run.FieldArgument is { Length: > 0 } argument && run.Field is not (TextFieldKind.ConditionalText or TextFieldKind.HiddenText or TextFieldKind.HiddenParagraph)
            ? name + " " + argument
            : name;
    }
}
