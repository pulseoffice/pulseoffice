using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxRowChanges
{
    public string? Read(W.TableRowProperties? properties, DocxReadContext context)
    {
        if (properties is null) return null;
        TrackChangeKind? found = properties.GetFirstChild<W.Deleted>() is { } deleted ? new TrackChangeKind(deleted, false)
            : properties.GetFirstChild<W.Inserted>() is { } inserted ? new TrackChangeKind(inserted, true)
            : null;
        if (found is not { } kind) return null;
        var author = kind.Element.Author?.Value ?? string.Empty;
        var date = kind.Element.Date?.Value is { } when ? new DateTimeOffset(DateTime.SpecifyKind(when, DateTimeKind.Utc)) : DateTimeOffset.UnixEpoch;
        var id = context.Document.NextId("change");
        TrackedChange change = kind.Inserted ? new Insertion(id, author, date) : new Deletion(id, author, date, new BlockContainer());
        context.Document.Changes.Add(change);
        return change.Id;
    }

    public OpenXmlElement? Write(TableRow row, DocxWriteContext context)
    {
        if (row.ChangeId is not { } id) return null;
        W.TrackChangeType? element = context.Document.Changes.Find(id) switch
        {
            Insertion => new W.Inserted(),
            Deletion => new W.Deleted(),
            _ => null,
        };
        if (element is null) return null;
        var change = context.Document.Changes.Find(id)!;
        element.Id = context.Revisions.Fresh().ToString(CultureInfo.InvariantCulture);
        element.Author = change.Author;
        element.Date = change.Date.UtcDateTime;
        return element;
    }

    private readonly record struct TrackChangeKind(W.TrackChangeType Element, bool Inserted);
}
