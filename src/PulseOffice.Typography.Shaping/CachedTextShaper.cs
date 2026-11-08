using PulseOffice.Typography.OpenType.Metrics;

namespace PulseOffice.Typography.Shaping;

public sealed record ShapedRunCacheOptions
{
    public int CachedRuns { get; init; } = 8192;
}

public sealed class CachedTextShaper(ITextShaper inner, ShapedRunCacheOptions options) : ITextShaper
{
    private readonly object _gate = new();
    private readonly Dictionary<ShapingKey, ShapedText> _runs = [];

    public CachedTextShaper(ITextShaper inner) : this(inner, new ShapedRunCacheOptions())
    {
    }

    public int Count
    {
        get
        {
            lock (_gate) return _runs.Count;
        }
    }

    public ShapedText Shape(FontMetricsFace face, string text, ShapingSpan span)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(text);
        var key = new ShapingKey(face, text, span);
        lock (_gate)
        {
            if (_runs.TryGetValue(key, out var cached)) return cached;
        }
        var shaped = inner.Shape(face, text, span);
        lock (_gate)
        {
            if (_runs.Count >= Math.Max(1, options.CachedRuns)) _runs.Clear();
            _runs[key] = shaped;
        }
        return shaped;
    }

    private readonly record struct ShapingKey(FontMetricsFace Face, string Text, ShapingSpan Span)
    {
        public bool Equals(ShapingKey other) =>
            ReferenceEquals(Face, other.Face) && Span == other.Span && string.Equals(Text, other.Text, StringComparison.Ordinal);

        public override int GetHashCode() =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Face), Span, string.GetHashCode(Text, StringComparison.Ordinal));
    }
}
