using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Sheet = PulseOffice.Sheets.Worksheet;
using Tc = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxThreadedComments(OoxmlTextEscape escape) : IXlsxSheetPartReader
{
    public const int MaximumReplies = 10_000;

    public const string LegacyAuthorPrefix = "tc=";

    public void Read(WorksheetPart part, Sheet sheet, XlsxReadContext context)
    {
        var entries = part.WorksheetThreadedCommentsParts
            .SelectMany(threads => threads.ThreadedComments?.Elements<Tc.ThreadedComment>() ?? [])
            .ToList();
        if (entries.Count == 0) return;

        var people = People(part);
        var threads = new Dictionary<CellAddress, (Tc.ThreadedComment Root, List<Tc.ThreadedComment> Replies)>();
        foreach (var entry in entries)
        {
            if (entry.Ref?.Value is not { } reference || !CellAddress.TryParseA1(reference, out var address)) continue;
            if (threads.TryGetValue(address, out var thread))
            {
                if (thread.Replies.Count < MaximumReplies) thread.Replies.Add(entry);
                continue;
            }
            threads[address] = (entry, []);
        }

        foreach (var (address, (root, replies)) in threads)
        {
            var legacy = sheet.Comments.GetValueOrDefault(address);
            sheet.Comments[address] = new CellComment
            {
                Text = Text(root),
                Author = Author(root, people),
                CreatedAt = Created(root),
                IsAlwaysVisible = legacy?.IsAlwaysVisible ?? false,
                Box = legacy?.Box,
                IsResolved = root.Done?.Value == true,
                Replies = [.. replies.Select(reply => new CommentReply { Text = Text(reply), Author = Author(reply, people), CreatedAt = Created(reply) })],
            };
        }
    }

    public void Write(WorksheetPart part, Sheet sheet, int sheetIndex)
    {
        var threads = sheet.Comments.Where(pair => pair.Value.IsThread).OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column).ToList();
        if (threads.Count == 0) return;

        var persons = Persons(part, threads.SelectMany(pair => new[] { pair.Value.Author }.Concat(pair.Value.Replies.Select(reply => reply.Author))).Select(author => author ?? string.Empty).Distinct(StringComparer.Ordinal));
        var entries = new List<Tc.ThreadedComment>();
        foreach (var (address, comment) in threads)
        {
            var rootId = ThreadId(sheetIndex, address);
            entries.Add(Entry(address, rootId, null, comment.Text, comment.Author, comment.CreatedAt, persons, comment.IsResolved));
            for (var index = 0; index < comment.Replies.Count; index++)
            {
                var reply = comment.Replies[index];
                entries.Add(Entry(address, Identifier($"{sheetIndex}:{address.ToA1()}:{index}"), rootId, reply.Text, reply.Author, reply.CreatedAt, persons, null));
            }
        }

        var threadsPart = part.AddNewPart<WorksheetThreadedCommentsPart>();
        threadsPart.ThreadedComments = new Tc.ThreadedComments(entries);
        threadsPart.ThreadedComments.Save();
    }

    public string ThreadId(int sheetIndex, CellAddress address) => Identifier($"{sheetIndex}:{address.ToA1()}");

    public string LegacyText(CellComment comment)
    {
        var text = new StringBuilder("[Threaded comment]\n\nYour version of Excel allows you to read this threaded comment; however, any edits to it will get removed if the file is opened in a newer version of Excel. Learn more: https://go.microsoft.com/fwlink/?linkid=870924\n\nComment:\n    ");
        text.Append(comment.Text);
        foreach (var reply in comment.Replies) text.Append("\nReply:\n    ").Append(reply.Text);
        return text.ToString();
    }

    private Tc.ThreadedComment Entry(CellAddress address, string id, string? parent, string text, string? author, DateTimeOffset created, Dictionary<string, string> persons, bool? done)
    {
        var entry = new Tc.ThreadedComment(new Tc.ThreadedCommentText(escape.Encode(text)))
        {
            Ref = address.ToA1(),
            Id = id,
            PersonId = persons[author ?? string.Empty],
            DT = created.UtcDateTime,
        };
        if (parent is not null) entry.ParentId = parent;
        if (done == true) entry.Done = true;
        return entry;
    }

    private Dictionary<string, string> Persons(WorksheetPart part, IEnumerable<string> authors)
    {
        var workbook = part.GetParentParts().OfType<WorkbookPart>().First();
        var personPart = workbook.WorkbookPersonParts.FirstOrDefault() ?? workbook.AddNewPart<WorkbookPersonPart>();
        personPart.PersonList ??= new Tc.PersonList();
        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var person in personPart.PersonList.Elements<Tc.Person>())
            if (person.DisplayName?.Value is { } name && person.Id?.Value is { } id) known.TryAdd(name, id);

        foreach (var author in authors)
        {
            if (known.ContainsKey(author)) continue;
            var id = Identifier("person:" + author);
            personPart.PersonList.Append(new Tc.Person { DisplayName = author.Length == 0 ? "?" : author, Id = id, UserId = author, ProviderId = "None" });
            known[author] = id;
        }

        personPart.PersonList.Save();
        return known;
    }

    private string Identifier(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash.AsSpan(0, 16)).ToString("B").ToUpperInvariant();
    }

    private Dictionary<string, string> People(WorksheetPart part)
    {
        var people = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var workbook in part.GetParentParts().OfType<WorkbookPart>())
            foreach (var person in workbook.WorkbookPersonParts.SelectMany(persons => persons.PersonList?.Elements<Tc.Person>() ?? []))
                if (person.Id?.Value is { Length: > 0 } id && person.DisplayName?.Value is { Length: > 0 } name) people.TryAdd(id, name);
        return people;
    }

    private DateTimeOffset Created(Tc.ThreadedComment comment) =>
        comment.DT?.Value is { } created ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc)) : DateTimeOffset.Now;

    private string? Author(Tc.ThreadedComment comment, Dictionary<string, string> people) =>
        comment.PersonId?.Value is { } id && people.TryGetValue(id, out var name) ? name : null;

    private string Text(Tc.ThreadedComment comment) => escape.Decode(comment.ThreadedCommentText?.Text ?? string.Empty);
}
