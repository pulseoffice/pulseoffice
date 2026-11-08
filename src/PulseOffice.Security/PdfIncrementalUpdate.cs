using System.Globalization;
using System.Text;

namespace PulseOffice.Security;

internal interface IPdfCrossReferenceSection
{
    string Write(IReadOnlyList<List<KeyValuePair<int, long>>> runs, PdfTrailerInfo trailer, int root, long startXref, int nextId);
}

internal sealed class PdfClassicCrossReference : IPdfCrossReferenceSection
{
    public string Write(IReadOnlyList<List<KeyValuePair<int, long>>> runs, PdfTrailerInfo trailer, int root, long startXref, int nextId)
    {
        var table = new StringBuilder("xref\n0 1\n0000000000 65535 f \n");

        foreach (var run in runs)
        {
            table.Append(CultureInfo.InvariantCulture, $"{run[0].Key} {run.Count}\n");
            foreach (var (_, offset) in run) table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {nextId} /Root {root} 0 R{trailer.Carried} /Prev {trailer.StartXref} >>\n");
        table.Append(CultureInfo.InvariantCulture, $"startxref\n{startXref}\n%%EOF\n");
        return table.ToString();
    }
}

internal sealed class PdfStreamCrossReference : IPdfCrossReferenceSection
{
    public string Write(IReadOnlyList<List<KeyValuePair<int, long>>> runs, PdfTrailerInfo trailer, int root, long startXref, int nextId)
    {
        var self = nextId;
        var entries = runs.SelectMany(run => run).Append(new KeyValuePair<int, long>(self, startXref)).OrderBy(pair => pair.Key).ToList();
        var index = new StringBuilder();
        var data = new StringBuilder();

        var position = 0;
        while (position < entries.Count)
        {
            var first = position;
            while (position + 1 < entries.Count && entries[position + 1].Key == entries[position].Key + 1) position++;
            index.Append(CultureInfo.InvariantCulture, $"{entries[first].Key} {position - first + 1} ");
            for (var i = first; i <= position; i++) data.Append(Entry(entries[i].Value));
            position++;
        }

        var body = new StringBuilder();
        body.Append(CultureInfo.InvariantCulture, $"{self} 0 obj\n<< /Type /XRef /Size {self + 1} /Index [{index.ToString().TrimEnd()}] /W [1 4 2] ");
        body.Append(CultureInfo.InvariantCulture, $"/Root {root} 0 R{trailer.Carried} /Prev {trailer.StartXref} /Length {data.Length} >>\nstream\n");
        body.Append(data).Append("\nendstream\nendobj\n");
        body.Append(CultureInfo.InvariantCulture, $"startxref\n{startXref}\n%%EOF\n");
        return body.ToString();
    }

    private string Entry(long offset)
    {
        if (offset is < 0 or > uint.MaxValue) throw new InvalidOperationException("The file is too large for the cross-reference stream.");
        var value = (uint)offset;
        return new string([(char)1, (char)(value >> 24), (char)((value >> 16) & 0xFF), (char)((value >> 8) & 0xFF), (char)(value & 0xFF), (char)0, (char)0]);
    }
}

internal sealed class PdfIncrementalUpdate
{
    private readonly MemoryStream _stream = new();
    private readonly SortedDictionary<int, long> _offsets = [];

    public PdfIncrementalUpdate(byte[] original)
    {
        _stream.Write(original);
        if (original.Length > 0 && original[^1] != (byte)'\n') Append("\n");
    }

    public void Add(int id, string body)
    {
        _offsets[id] = _stream.Length;
        Append($"{id} 0 obj\n{body}\nendobj\n");
    }

    public byte[] Finish(int root, PdfTrailerInfo trailer)
    {
        var startXref = _stream.Length;
        var nextId = Math.Max(trailer.Size, _offsets.Keys.Max() + 1);
        IPdfCrossReferenceSection section = trailer.CrossReferenceStream ? new PdfStreamCrossReference() : new PdfClassicCrossReference();
        Append(section.Write([.. Runs()], trailer, root, startXref, nextId));
        return _stream.ToArray();
    }

    private IEnumerable<List<KeyValuePair<int, long>>> Runs()
    {
        var run = new List<KeyValuePair<int, long>>();
        foreach (var entry in _offsets)
        {
            if (run.Count > 0 && entry.Key != run[^1].Key + 1)
            {
                yield return run;
                run = [];
            }

            run.Add(entry);
        }

        if (run.Count > 0) yield return run;
    }

    private void Append(string text) => _stream.Write(Encoding.Latin1.GetBytes(text));
}
