using System.Collections;

namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed class CompactCellSet : IEnumerable<SheetCell>
{
    private const int InlineCapacity = 4;

    private SheetCell[]? _few = new SheetCell[InlineCapacity];
    private int _count;
    private HashSet<SheetCell>? _many;

    public int Count => _many?.Count ?? _count;

    public void Add(SheetCell cell)
    {
        if (_many is not null)
        {
            _many.Add(cell);
            return;
        }
        if (Array.IndexOf(_few!, cell, 0, _count) >= 0) return;
        if (_count < InlineCapacity)
        {
            _few![_count++] = cell;
            return;
        }
        _many = [.. _few!, cell];
        _few = null;
        _count = 0;
    }

    public void Remove(SheetCell cell)
    {
        if (_many is not null)
        {
            _many.Remove(cell);
            return;
        }
        var index = Array.IndexOf(_few!, cell, 0, _count);
        if (index < 0) return;
        _few![index] = _few[--_count];
    }

    public IEnumerator<SheetCell> GetEnumerator()
    {
        if (_many is not null)
        {
            foreach (var cell in _many) yield return cell;
            yield break;
        }
        for (var index = 0; index < _count; index++) yield return _few![index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
