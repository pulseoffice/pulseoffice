using System.Collections;

namespace PulseOffice.Sheets.Engine.Evaluation;

public sealed class LazyArgumentList(int count, Func<int, EvalValue> evaluate) : IReadOnlyList<EvalValue>
{
    private readonly EvalValue?[] _values = new EvalValue?[count];

    public int Count => count;

    public EvalValue this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)count, nameof(index));
            return _values[index] ??= evaluate(index);
        }
    }

    public IEnumerator<EvalValue> GetEnumerator()
    {
        for (var index = 0; index < count; index++) yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
