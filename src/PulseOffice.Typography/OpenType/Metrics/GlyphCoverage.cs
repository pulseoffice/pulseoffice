namespace PulseOffice.Typography.OpenType.Metrics;

public sealed class GlyphCoverage(FontTableData data, int offset)
{
    private const int MaximumEntries = 65536;

    public int IndexOf(int glyph)
    {
        var format = data.U16(offset);
        var count = Math.Min(data.U16(offset + 2), MaximumEntries);
        if (format == 1)
        {
            var low = 0;
            var high = count - 1;
            while (low <= high)
            {
                var middle = (low + high) / 2;
                var value = data.U16(offset + 4 + middle * 2);
                if (value == glyph) return middle;
                if (value < glyph) low = middle + 1;
                else high = middle - 1;
            }
            return -1;
        }
        if (format != 2) return -1;
        for (var i = 0; i < count; i++)
        {
            var record = offset + 4 + i * 6;
            var start = data.U16(record);
            var end = data.U16(record + 2);
            if (glyph >= start && glyph <= end) return data.U16(record + 4) + glyph - start;
        }
        return -1;
    }
}

public sealed class GlyphClasses(FontTableData data, int offset)
{
    public int ClassOf(int glyph)
    {
        if (offset <= 0) return 0;
        var format = data.U16(offset);
        if (format == 1)
        {
            var start = data.U16(offset + 2);
            var count = data.U16(offset + 4);
            return glyph >= start && glyph < start + count ? data.U16(offset + 6 + (glyph - start) * 2) : 0;
        }
        if (format != 2) return 0;
        var ranges = data.U16(offset + 2);
        for (var i = 0; i < ranges; i++)
        {
            var record = offset + 4 + i * 6;
            if (glyph >= data.U16(record) && glyph <= data.U16(record + 2)) return data.U16(record + 4);
        }
        return 0;
    }
}
