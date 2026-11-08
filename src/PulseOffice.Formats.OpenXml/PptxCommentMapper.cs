using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using A = DocumentFormat.OpenXml.Drawing;
using M = DocumentFormat.OpenXml.Office2021.PowerPoint.Comment;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxCommentMapper(PulseOffice.Formats.Packaging.XmlCharacterFilter characters)
{

    private readonly Dictionary<string, uint> _authorIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, uint> _authorNextIndex = new(StringComparer.Ordinal);
    private P.CommentAuthorList? _authorList;

    public void Reset()
    {
        _authorIds.Clear();
        _authorNextIndex.Clear();
        _authorList = null;
    }

    public void Write(SlidePart slidePart, PresentationPart presentationPart, Slide slide)
    {
        if (slide.Comments.Count == 0) return;

        var authorList = EnsureAuthorList(presentationPart);
        var commentList = new P.CommentList();

        foreach (var comment in slide.Comments)
        {
            var authorId = AuthorId(authorList, comment.Author);
            var index = NextIndex(comment.Author);
            UpdateLastIndex(authorList, authorId, index);

            commentList.Append(new P.Comment(
                new P.Position { X = comment.X.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Y = comment.Y.Value * PulseOffice.Foundation.Units.Emu.PerTwip },
                new P.Text { Text = characters.Valid(comment.Text) })
            {
                AuthorId = authorId,
                Index = index,
                DateTime = comment.CreatedAt.UtcDateTime,
            });
        }

        var commentsPart = slidePart.AddNewPart<SlideCommentsPart>();
        commentsPart.CommentList = commentList;
        commentsPart.CommentList.Save();
    }

    public IEnumerable<SlideComment> Read(SlidePart slidePart, PresentationPart presentationPart, string slideId) =>
        Legacy(slidePart, presentationPart, slideId).Concat(Modern(slidePart, presentationPart, slideId));

    private IEnumerable<SlideComment> Modern(SlidePart slidePart, PresentationPart presentationPart, string slideId)
    {
        var comments = slidePart.GetPartsOfType<PowerPointCommentPart>().SelectMany(part => part.CommentList?.Elements<M.Comment>() ?? []).ToList();
        if (comments.Count == 0) yield break;
        var authors = presentationPart.GetPartsOfType<PowerPointAuthorsPart>()
            .SelectMany(part => part.AuthorList?.Elements<M.Author>() ?? [])
            .Where(author => author.Id?.Value is { Length: > 0 })
            .GroupBy(author => author.Id!.Value!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name?.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        string Author(string? id) => id is not null && authors.TryGetValue(id, out var name) ? name : string.Empty;

        var order = 0;
        foreach (var comment in comments)
        {
            var replies = comment.GetFirstChild<M.CommentReplyList>()?.Elements<M.CommentReply>().Take(MaximumReplies) ?? [];
            var text = string.Join("\n", new[] { BodyText(comment.GetFirstChild<M.TextBodyType>()) }
                .Concat(replies.Select(reply => $"{ReplyAuthor(Author(reply.AuthorId?.Value))}: {BodyText(reply.GetFirstChild<M.TextBodyType>())}")));
            var position = comment.GetFirstChild<M.Point2DType>();
            yield return new SlideComment
            {
                Id = $"{slideId}.mcm{order++}",
                Author = Author(comment.AuthorId?.Value),
                Text = text,
                CreatedAt = comment.Created?.Value is { } created ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc)) : DateTimeOffset.Now,
                X = new Twip((int)Math.Clamp((position?.X?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip, int.MinValue, int.MaxValue)),
                Y = new Twip((int)Math.Clamp((position?.Y?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip, int.MinValue, int.MaxValue)),
            };
        }
    }

    private string ReplyAuthor(string name) => name.Length > 0 ? name : "?";

    public const int MaximumReplies = 10_000;

    private string BodyText(M.TextBodyType? body) =>
        body is null ? string.Empty : string.Join("\n", body.Elements<A.Paragraph>().Select(paragraph => string.Concat(paragraph.Descendants<A.Text>().Select(run => run.Text))));

    private IEnumerable<SlideComment> Legacy(SlidePart slidePart, PresentationPart presentationPart, string slideId)
    {
        if (slidePart.SlideCommentsPart?.CommentList is not { } list) yield break;

        var authors = presentationPart.CommentAuthorsPart?.CommentAuthorList?
            .Elements<P.CommentAuthor>()
            .Where(a => a.Id?.Value is not null)
            .GroupBy(a => a.Id!.Value)
            .ToDictionary(group => group.Key, group => group.First().Name?.Value ?? string.Empty) ?? [];

        var order = 0;
        foreach (var comment in list.Elements<P.Comment>())
        {
            var author = comment.AuthorId?.Value is { } aid && authors.TryGetValue(aid, out var name)
                ? name : string.Empty;

            yield return new SlideComment
            {
                Id = $"{slideId}.cm{order++}",
                Author = author,
                Text = comment.Text?.Text ?? string.Empty,
                CreatedAt = comment.DateTime?.Value is { } dt
                    ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)) : DateTimeOffset.Now,
                X = new Twip((int)((comment.Position?.X?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
                Y = new Twip((int)((comment.Position?.Y?.Value ?? 0) / PulseOffice.Foundation.Units.Emu.PerTwip)),
            };
        }
    }

    private P.CommentAuthorList EnsureAuthorList(PresentationPart presentationPart)
    {
        if (_authorList is not null) return _authorList;
        var part = presentationPart.AddNewPart<CommentAuthorsPart>();
        _authorList = new P.CommentAuthorList();
        part.CommentAuthorList = _authorList;
        return _authorList;
    }

    private uint AuthorId(P.CommentAuthorList authorList, string author)
    {
        if (_authorIds.TryGetValue(author, out var existing)) return existing;

        var id = (uint)_authorIds.Count;
        _authorIds[author] = id;
        var name = characters.Valid(author);
        authorList.Append(new P.CommentAuthor
        {
            Id = id,
            Name = name,
            Initials = Initials(name),
            ColorIndex = id % 6,
            LastIndex = 0,
        });
        return id;
    }

    private uint NextIndex(string author)
    {
        var next = _authorNextIndex.TryGetValue(author, out var n) ? n + 1 : 1;
        _authorNextIndex[author] = next;
        return next;
    }

    private void UpdateLastIndex(P.CommentAuthorList authorList, uint authorId, uint index)
    {
        if (authorList.Elements<P.CommentAuthor>().FirstOrDefault(a => a.Id?.Value == authorId) is { } author)
            author.LastIndex = index;
    }

    private string Initials(string author)
    {
        var parts = author.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        return parts.Length == 1
            ? FirstLetter(parts[0]).ToUpperInvariant()
            : (FirstLetter(parts[0]) + FirstLetter(parts[^1])).ToUpperInvariant();
    }

    private string FirstLetter(string word) =>
        word[..System.Globalization.StringInfo.GetNextTextElementLength(word)];
}
