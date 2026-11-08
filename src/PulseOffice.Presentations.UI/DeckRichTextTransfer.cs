using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public interface IDeckRichTextTransfer
{
    IReadOnlyList<TextParagraph>? Import(string? rtf, string? html);

    (string? Html, string? Rtf) Export(IReadOnlyList<TextParagraph> paragraphs);
}

public sealed class DeckRichTextTransfer(ITextDocumentInterchange interchange) : IDeckRichTextTransfer
{
    private const string HtmlId = "html";
    private const string RtfId = "rtf";

    public IReadOnlyList<TextParagraph>? Import(string? rtf, string? html)
    {
        var document = Read(RtfId, rtf, Encoding.Latin1) ?? Read(HtmlId, html, Encoding.UTF8);
        if (document is null) return null;

        var paragraphs = Flattened(document.Body).Select(block => Plain(block.Content)).ToList();
        while (paragraphs.Count > 0 && paragraphs[^1].Text.Length == 0) paragraphs.RemoveAt(paragraphs.Count - 1);
        while (paragraphs.Count > 0 && paragraphs[0].Text.Length == 0) paragraphs.RemoveAt(0);
        return paragraphs.Count == 0 || !paragraphs.Any(paragraph => paragraph.Runs.Any(Formatted)) ? null : paragraphs;
    }

    public (string? Html, string? Rtf) Export(IReadOnlyList<TextParagraph> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        if (paragraphs.Count == 0) return (null, null);
        var document = new TextDocument();
        var index = 0;
        foreach (var paragraph in paragraphs)
            document.Body.Add(new ParagraphBlock($"p{index++}", Resolved(paragraph)));
        return (Write(HtmlId, document, Encoding.UTF8), Write(RtfId, document, Encoding.Latin1));
    }

    private IEnumerable<ParagraphBlock> Flattened(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        ParagraphBlock paragraph => [paragraph],
        SectionBlock section => Flattened(section.Content),
        TableBlock table => table.Rows.SelectMany(row => row.Cells).Where(cell => !cell.Covered).SelectMany(cell => Flattened(cell.Content)),
        _ => (IEnumerable<ParagraphBlock>)[],
    });

    private TextParagraph Resolved(TextParagraph paragraph) =>
        new TextParagraph(paragraph.Text) { Align = paragraph.Align, Bullet = paragraph.Bullet, Numbered = paragraph.Numbered, BulletLevel = paragraph.BulletLevel }
            .WithRuns([.. paragraph.EffectiveRuns().Select(run => run with
            {
                FontSize = run.FontSize ?? paragraph.FontSize,
                Bold = run.Bold || paragraph.Bold,
                Italic = run.Italic || paragraph.Italic,
                Underline = run.Underline || paragraph.Underline,
                Strikethrough = run.Strikethrough || paragraph.Strikethrough,
                Color = run.Color ?? paragraph.Color,
            })]);

    private string? Write(string id, TextDocument document, Encoding encoding) =>
        interchange.Write(id, document) is { } bytes ? encoding.GetString(bytes) : null;

    private TextParagraph Plain(TextParagraph source) =>
        new TextParagraph(source.Text)
        {
            Align = source.Align,
            Bullet = source.Bullet,
            Numbered = source.Numbered,
            NumberFormat = source.NumberFormat,
            BulletLevel = source.BulletLevel,
        }.WithRuns(source.Runs.Count > 0 ? [.. source.Runs.Where(run => run.Text.Length > 0 || run.Field != TextFieldKind.None)] : [new TextRun(source.Text)]);

    private bool Formatted(TextRun run) =>
        run.Bold || run.Italic || run.Underline || run.Strikethrough || run.Color is not null || run.Font is not null
        || run.FontSize is not null || run.Highlight is not null || run.Baseline != TextBaseline.Normal;

    private TextDocument? Read(string id, string? text, Encoding encoding) =>
        string.IsNullOrWhiteSpace(text) ? null : interchange.Read(id, encoding.GetBytes(text));
}
