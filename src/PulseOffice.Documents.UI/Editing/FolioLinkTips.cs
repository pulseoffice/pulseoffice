using Avalonia.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.UI.Controls.Links;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioLinkTips
{
    string? At(FolioEditor editor, DocumentPosition position);

    string? OfObject(FolioEditor editor, FolioObjectLinkTarget target);

    void Show(Control target, string? text);
}

public sealed class FolioLinkTips(ILinkTips tips, IFolioObjectLinks links) : IFolioLinkTips
{
    public string? At(FolioEditor editor, DocumentPosition position)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var paragraph = editor.Services.Stories.Find(editor.Document, position.ParagraphId).Paragraph;
        var start = 0;
        foreach (var run in paragraph.Content.Runs)
        {
            var end = start + run.Text.Length;
            if (position.Offset >= start && position.Offset < end) return tips.Text(run.Hyperlink, run.HyperlinkTip);
            start = end;
        }

        return null;
    }

    public string? OfObject(FolioEditor editor, FolioObjectLinkTarget target)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(target);
        return tips.Text(links.EffectiveLink(editor, target), links.Tip(editor, target));
    }

    public void Show(Control target, string? text) => tips.Show(target, text);
}
