using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Sheet = PulseOffice.Sheets.Worksheet;
using Tc = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxThreadedComments(OoxmlTextEscape escape) : IXlsxSheetPartReader
{
    public const int MaximumReplies = 10_000;

    public const string FlattenedCode = "XLSX235";

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

        var flattened = 0;
        foreach (var (address, (root, replies)) in threads)
        {
            var visible = sheet.Comments.TryGetValue(address, out var legacy) && legacy.IsAlwaysVisible;
            sheet.Comments[address] = new CellComment
            {
                Text = string.Join("\n", new[] { Text(root) }.Concat(replies.Select(reply => $"{Author(reply, people) ?? "?"}: {Text(reply)}"))),
                Author = Author(root, people),
                CreatedAt = root.DT?.Value is { } created ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc)) : DateTimeOffset.Now,
                IsAlwaysVisible = visible,
            };
            if (replies.Count > 0 || root.Done?.Value == true) flattened++;
        }

        if (flattened == 0 || context.Diagnostics.Any(diagnostic => diagnostic.Code == FlattenedCode)) return;
        context.Diagnostics.Add(Diagnostic.Info(FlattenedCode,
            "Some threaded comments have replies or are resolved: Ledger cell comments have no threads, so the replies are joined to the comment text with their authors and the resolved state is lost."));
    }

    private Dictionary<string, string> People(WorksheetPart part)
    {
        var people = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var workbook in part.GetParentParts().OfType<WorkbookPart>())
            foreach (var person in workbook.WorkbookPersonParts.SelectMany(persons => persons.PersonList?.Elements<Tc.Person>() ?? []))
                if (person.Id?.Value is { Length: > 0 } id && person.DisplayName?.Value is { Length: > 0 } name) people.TryAdd(id, name);
        return people;
    }

    private string? Author(Tc.ThreadedComment comment, Dictionary<string, string> people) =>
        comment.PersonId?.Value is { } id && people.TryGetValue(id, out var name) ? name : null;

    private string Text(Tc.ThreadedComment comment) => escape.Decode(comment.ThreadedCommentText?.Text ?? string.Empty);
}
