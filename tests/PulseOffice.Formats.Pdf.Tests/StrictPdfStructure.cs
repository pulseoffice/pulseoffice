using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed partial class StrictPdfStructure
{
    [GeneratedRegex(@"startxref\n(\d+)\n%%EOF\n$")]
    private partial Regex StartXref();

    [GeneratedRegex(@"(\d+) 0 R\b")]
    private partial Regex Reference();

    [GeneratedRegex(@"(\d+) 0 obj\n")]
    private partial Regex ObjectHeader();

    [GeneratedRegex(@"[-+]?(?:NaN|Infinity|\d[\d.]*E[-+]\d+)")]
    private partial Regex BadNumber();

    public void Validate(byte[] file)
    {
        var text = Encoding.Latin1.GetString(file);
        Assert.StartsWith("%PDF-1.7\n", text, StringComparison.Ordinal);

        var start = StartXref().Match(text);
        Assert.True(start.Success, "The file does not end with startxref and %%EOF.");
        var xref = int.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.Equal("xref\n", text.Substring(xref, 5));

        var lines = text[(xref + 5)..].Split('\n');
        var header = lines[0].Split(' ');
        Assert.Equal("0", header[0]);
        var count = int.Parse(header[1], CultureInfo.InvariantCulture);
        Assert.Equal("0000000000 65535 f ", lines[1]);

        var offsets = new Dictionary<int, int>();
        for (var id = 1; id < count; id++)
        {
            var entry = lines[id + 1];
            Assert.Equal(19, entry.Length);
            Assert.EndsWith(" 00000 n ", entry, StringComparison.Ordinal);
            var offset = int.Parse(entry[..10], CultureInfo.InvariantCulture);
            Assert.True(offset > 0 && offset < xref, $"Object {id} has offset {offset}.");
            Assert.StartsWith($"{id} 0 obj\n", text[offset..], StringComparison.Ordinal);
            offsets[id] = offset;
        }

        Assert.Equal("trailer", lines[count + 1]);
        Assert.Contains($"/Size {count} ", lines[count + 2], StringComparison.Ordinal);
        Assert.Equal(count - 1, ObjectHeader().Matches(text).Count);

        foreach (var (id, offset) in offsets)
        {
            var end = text.IndexOf("\nendobj\n", offset, StringComparison.Ordinal);
            Assert.True(end > 0, $"Object {id} is not closed.");
            var body = text[offset..end];
            var stream = body.IndexOf("\nstream\n", StringComparison.Ordinal);
            var dictionary = stream < 0 ? body : body[..stream];
            if (stream >= 0)
            {
                var length = int.Parse(Regex.Match(dictionary, @"/Length (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
                Assert.Equal("\nendstream", body[(stream + 8 + length)..]);
            }
            foreach (Match reference in Reference().Matches(dictionary))
                Assert.True(offsets.ContainsKey(int.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture)), $"Object {id} refers to a missing object.");
            Assert.False(BadNumber().IsMatch(dictionary), $"Object {id} has a number PDF cannot read: {dictionary}");
        }
    }
}
