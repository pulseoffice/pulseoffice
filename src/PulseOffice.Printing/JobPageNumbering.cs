namespace PulseOffice.Printing;

public readonly record struct JobPage(int Number, int Total, bool StartsSection);

public sealed record NumberedPart(IPrintableDocument Document, bool StartsSection, int? FirstNumber);

public interface IJobPageNumbering
{
    JobPage For(IPrintableDocument part, int index);
}

public sealed class OwnPageNumbering(int? firstNumber = null) : IJobPageNumbering
{
    public JobPage For(IPrintableDocument part, int index)
    {
        ArgumentNullException.ThrowIfNull(part);
        return new JobPage((firstNumber ?? 1) + index, part.PageCount, index == 0);
    }
}

public sealed class JobPageNumbering : IJobPageNumbering
{
    private readonly Dictionary<IPrintableDocument, (int First, bool StartsSection)> _starts = new(ReferenceEqualityComparer.Instance);
    private int _total;

    public void Number(IReadOnlyList<NumberedPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        _starts.Clear();
        _total = 0;
        var next = 1;

        foreach (var part in parts)
        {
            if (part.StartsSection && part.FirstNumber is { } restart) next = restart;
            _starts[part.Document] = (next, part.StartsSection);
            next += part.Document.PageCount;
            _total += part.Document.PageCount;
        }
    }

    public JobPage For(IPrintableDocument part, int index)
    {
        ArgumentNullException.ThrowIfNull(part);
        return _starts.TryGetValue(part, out var start)
            ? new JobPage(start.First + index, _total, start.StartsSection && index == 0)
            : new JobPage(index + 1, part.PageCount, index == 0);
    }
}
