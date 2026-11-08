using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public sealed record EnvelopeFormat(string Name, double WidthMillimetres, double HeightMillimetres);

public sealed record EnvelopeRequest(string Addressee, string Sender, EnvelopeFormat Format);

public interface IEnvelopes
{
    IReadOnlyList<EnvelopeFormat> Formats { get; }

    void Insert(TextDocument document, UndoStack history, EnvelopeRequest request);
}

public sealed class Envelopes(IStyleEditing styles) : IEnvelopes
{
    private const string StyleName = "Envelope";

    public IReadOnlyList<EnvelopeFormat> Formats { get; } =
    [
        new("DL", 220, 110), new("C6/5", 229, 114), new("C5", 229, 162), new("C6", 162, 114), new("C4", 324, 229),
        new("#10", 241.3, 104.8), new("Monarch", 190.5, 98.4),
    ];

    public void Insert(TextDocument document, UndoStack history, EnvelopeRequest request)
    {
        using (history.BeginTransaction("insert-envelope"))
        {
            var style = new PageStyle(StyleName)
            {
                Size = new DocumentSize(Twip.FromMillimeters(request.Format.WidthMillimetres), Twip.FromMillimeters(request.Format.HeightMillimetres)),
                Margins = Margins.Uniform(Twip.FromCentimeters(1)),
            };
            if (document.Styles.Page.Contains(StyleName))
                styles.Change(document, history, document.Styles.Page, StyleName, existing =>
                {
                    existing.Size = style.Size;
                    existing.Margins = style.Margins;
                }, "envelope-style");
            else styles.Add(document, history, document.Styles.Page, style, "envelope-style");

            var recorder = new EditRecorder(document, "insert-envelope").Touch(document.Body);
            var first = document.Body.Blocks.OfType<ParagraphBlock>().FirstOrDefault();
            if (first is not null)
            {
                recorder.Touch(first);
                first.Content = first.Content with { PageStyleName = first.Content.PageStyleName ?? document.InitialPageStyle };
            }
            var indent = request.Format.WidthMillimetres * 0.45 / 25.4 * 72;
            var blocks = new List<Block>();
            var sender = Lines(request.Sender);
            foreach (var line in sender.Count > 0 ? sender : [string.Empty])
                blocks.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph(line)));
            var addressee = Lines(request.Addressee);
            for (var index = 0; index < addressee.Count; index++)
                blocks.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph(addressee[index])
                {
                    MarginLeft = indent,
                    SpaceBefore = index == 0 ? request.Format.HeightMillimetres * 0.3 / 25.4 * 72 : 0,
                }));
            var opening = (ParagraphBlock)blocks[0];
            opening.Content = opening.Content with { PageStyleName = StyleName };
            document.Body.InsertRange(0, blocks);
            recorder.Commit(history);
        }
    }

    private List<string> Lines(string text) =>
        [.. text.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()).Where((line, index) => line.Length > 0 || index > 0)];
}
