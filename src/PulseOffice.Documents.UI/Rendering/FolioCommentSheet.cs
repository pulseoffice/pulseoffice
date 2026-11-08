using System.Globalization;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Styles;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Rendering;

public interface IFolioCommentSheet
{
    TextDocument? Build(TextDocument source);
}

public sealed class FolioCommentSheet(ICommentAnchors anchors, ITextCatalog texts) : IFolioCommentSheet
{
    public TextDocument? Build(TextDocument source)
    {
        if (source.Comments.All.Count == 0) return null;
        var sheet = new TextDocument(source.Styles.Clone()) { InitialPageStyle = source.InitialPageStyle, Language = source.Language };
        var blocks = new List<Block> { new ParagraphBlock(sheet.NextId("p"), texts["folio.print.comments.heading"], DefaultStyleNames.HeadingLevel(1)) };
        var anchored = anchors.All(source);
        var roots = anchored.Select(anchor => anchor.Comment).Concat(source.Comments.All.Where(comment => comment.ParentId is null)).DistinctBy(comment => comment.Id).ToList();
        var number = 0;
        foreach (var root in roots)
        {
            number++;
            if (anchored.FirstOrDefault(anchor => anchor.Comment.Id == root.Id) is { } anchor && Quote(source, anchor) is { Length: > 0 } quoted)
                blocks.Add(Paragraph(sheet, number.ToString(CultureInfo.CurrentCulture) + ". «" + quoted + "»", italic: true, indent: 0));
            foreach (var comment in source.Comments.Thread(root.Id))
            {
                var depth = comment.ParentId is null ? 0 : 1;
                var date = comment.Date == default ? string.Empty : ", " + comment.Date.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
                var resolved = comment.Resolved ? " (" + texts["review.panel.resolved"] + ")" : string.Empty;
                var body = string.Join(" ", comment.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
                blocks.Add(Paragraph(sheet, comment.Author + date + resolved + ": " + body, italic: false, indent: 18 + depth * 18));
            }
        }
        sheet.Body.Replace(blocks);
        return sheet;
    }

    private string Quote(TextDocument source, CommentAnchor anchor)
    {
        if (anchor.Start.ParagraphId != anchor.End.ParagraphId) return string.Empty;
        var paragraph = source.Body.Blocks.OfType<ParagraphBlock>().FirstOrDefault(block => block.Id == anchor.Start.ParagraphId);
        if (paragraph is null || anchor.End.Offset <= anchor.Start.Offset || anchor.End.Offset > paragraph.Length) return string.Empty;
        var text = paragraph.Text[anchor.Start.Offset..anchor.End.Offset];
        return text.Length > 120 ? text[..120] + "…" : text;
    }

    private ParagraphBlock Paragraph(TextDocument sheet, string text, bool italic, double indent) =>
        new(sheet.NextId("p"), new TextParagraph(text) { Runs = [new TextRun(text) { Italic = italic }], MarginLeft = indent, SpaceAfter = 4 });
}
