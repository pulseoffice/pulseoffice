using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
namespace PulseOffice.Printing;

public sealed class CompositePrintDocument : IPrintableDocument
{
    private readonly IReadOnlyList<IPrintableDocument> _parts;
    private readonly int[] _offsets;

    public CompositePrintDocument(IReadOnlyList<IPrintableDocument> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        _parts = [.. parts.Where(part => part.PageCount > 0)];

        if (_parts.Count == 0)
            throw new ArgumentException("No document with pages to print.", nameof(parts));

        _offsets = new int[_parts.Count];
        var total = 0;

        for (var index = 0; index < _parts.Count; index++)
        {
            _offsets[index] = total;
            total += _parts[index].PageCount;
        }

        PageCount = total;
    }

    public PageSetup Setup => _parts[0].Setup;

    public int PageCount { get; }

    public string? Language => _parts[0].Language;

    public DocumentSize PageSize(int index)
    {
        for (var part = _parts.Count - 1; part >= 0; part--)
            if (index >= _offsets[part]) return _parts[part].PageSize(index - _offsets[part]);
        return _parts[0].PageSize(0);
    }

    public PrintPageLabel? PageLabel(int index)
    {
        for (var part = _parts.Count - 1; part >= 0; part--)
            if (index >= _offsets[part]) return _parts[part].PageLabel(index - _offsets[part]);
        return null;
    }

    public void PaintPage(int index, IPageCanvas canvas)
    {
        for (var part = _parts.Count - 1; part >= 0; part--)
        {
            if (index < _offsets[part]) continue;

            _parts[part].PaintPage(index - _offsets[part], canvas);
            return;
        }
    }
}
