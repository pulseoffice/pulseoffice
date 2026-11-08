using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Units;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Clipboard;

public interface IObjectFragments
{
    DocumentFragment? Of(TextDocument document, string objectId);

    DocumentFragment Wrap(IReadOnlyList<(EmbeddedObject Content, DocumentSize Size)> objects);
}

public sealed class ObjectFragments : IObjectFragments
{
    private const string ParagraphId = "object-fragment";

    public DocumentFragment? Of(TextDocument document, string objectId)
    {
        if (document.Objects.Find(objectId) is not { } item) return null;
        var copy = item.Clone();
        copy.Anchor = item.Anchor is AsCharacterAnchor ? new AsCharacterAnchor(ParagraphId) : new CharacterAnchor(ParagraphId);
        var paragraph = new ParagraphBlock(ParagraphId, new TextParagraph(string.Empty)) { Marks = [new ObjectMark(0, copy.Id)] };
        var styles = new StyleSheet();
        if (copy.StyleName is { } name && document.Styles.Frame.Find(name) is { } frame) styles.Frame.Add((FrameStyle)frame.Clone());
        return new DocumentFragment(new BlockContainer([paragraph]), styles, [], [], [copy]);
    }

    public DocumentFragment Wrap(IReadOnlyList<(EmbeddedObject Content, DocumentSize Size)> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        var anchored = objects.Select((item, index) => new AnchoredObject(
            "object-fragment-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new AsCharacterAnchor(ParagraphId),
            new EmbeddedContent(item.Content),
            item.Size.IsEmpty ? item.Content.NaturalSize : item.Size)).ToList();
        var paragraph = new ParagraphBlock(ParagraphId, new TextParagraph(string.Empty))
        {
            Marks = [.. anchored.Select(item => (InlineMark)new ObjectMark(0, item.Id))],
        };
        return new DocumentFragment(new BlockContainer([paragraph]), new StyleSheet(), [], [], anchored);
    }
}
