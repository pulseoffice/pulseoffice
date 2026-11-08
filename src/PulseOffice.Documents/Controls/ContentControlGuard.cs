using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.Controls;

public interface IContentControlGuard
{
    bool LocksTyping(TextDocument document, DocumentPosition at);

    bool Protects(TextDocument document, DocumentRange range);

    bool AcceptsTyping(ContentControl control);
}

public sealed class ContentControlGuard(IContentControlCatalog catalog) : IContentControlGuard
{
    public bool AcceptsTyping(ContentControl control) =>
        !control.ContentLocked && control.Kind is ContentControlKind.RichText or ContentControlKind.PlainText or ContentControlKind.ComboBox or ContentControlKind.Date;

    public bool LocksTyping(TextDocument document, DocumentPosition at) =>
        catalog.At(document, at) is { } span && !AcceptsTyping(span.Control);

    public bool Protects(TextDocument document, DocumentRange range)
    {
        if (range.IsCollapsed) return false;
        var spans = catalog.All(document);
        if (spans.Count == 0) return false;
        var from = (catalog.Order(document, range.Start.ParagraphId), range.Start.Offset);
        var to = (catalog.Order(document, range.End.ParagraphId), range.End.Offset);
        foreach (var span in spans)
        {
            var start = (span.StartOrder, span.Start.Offset);
            var end = (span.EndOrder, span.End.Offset);
            var removesControl = from.CompareTo(start) <= 0 && end.CompareTo(to) <= 0 && (from.CompareTo(start) < 0 || to.CompareTo(end) > 0);
            if (removesControl)
            {
                if (span.Control.ControlLocked) return true;
                continue;
            }
            var touchesContent = from.CompareTo(end) < 0 && to.CompareTo(start) > 0;
            if (touchesContent && !AcceptsTyping(span.Control)) return true;
        }
        return false;
    }
}
