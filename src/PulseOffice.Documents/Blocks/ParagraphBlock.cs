using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Blocks;

public sealed record ParagraphState(TextParagraph Content, IReadOnlyList<InlineMark> Marks)
{
    public int Length => Content.Text.Length;
}

public sealed class ParagraphBlock(string id, TextParagraph content) : Block(id)
{
    public ParagraphBlock(string id, string text, string? styleName = null)
        : this(id, new TextParagraph(text) { StyleName = styleName, Runs = text.Length == 0 ? [] : [new TextRun(text)] })
    {
    }

    public TextParagraph Content { get; set; } = content;

    public IReadOnlyList<InlineMark> Marks { get; set; } = [];

    public string Text => Content.Text;

    public int Length => Content.Text.Length;

    public string? StyleName => Content.StyleName;

    public ParagraphState State
    {
        get => new(Content, Marks);
        set
        {
            Content = value.Content;
            Marks = value.Marks;
        }
    }

    public override T Accept<T>(IBlockVisitor<T> visitor) => visitor.VisitParagraph(this);

    public override Block Clone() => new ParagraphBlock(Id, Content) { Marks = Marks };
}
