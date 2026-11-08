using System.Globalization;
using System.Text;
using PulseOffice.Printing;
using PulseOffice.Security;

namespace PulseOffice.Formats.Pdf.Fonts;

public sealed class EmbeddedPdfSignatureFont(IPdfFontSetFactory fonts) : IPdfSignatureFont
{
    public const string Family = "Noto Sans";

    public PdfSignatureText Typeset(IReadOnlyList<string> lines, double size, IPdfAppendedObjects objects)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(objects);
        var set = fonts.Create();
        var print = new PrintFont(Family, size);
        var shown = lines.Select(line => set.Show(line, print)).ToList();
        var sink = new AppendedObjectSink(objects);
        var names = new Dictionary<IPdfFontResource, (string Name, int Id)>(ReferenceEqualityComparer.Instance);
        foreach (var resource in set.Resources) names[resource] = ($"F{names.Count + 1}", sink.Reserve());
        var points = Math.Round(size, 2).ToString("0.##", CultureInfo.InvariantCulture);
        var operators = shown.Select(pieces =>
        {
            var line = new StringBuilder();
            foreach (var piece in pieces)
            {
                if (!names.TryGetValue(piece.Font, out var name)) continue;
                line.Append('/').Append(name.Name).Append(' ').Append(points).Append(" Tf [").Append(piece.Font.Show(piece.Glyphs)).Append("] TJ ");
            }
            return line.ToString().TrimEnd();
        }).ToList();
        foreach (var (resource, (_, id)) in names) resource.Write(sink, id, false);
        var dictionary = new StringBuilder("<<");
        foreach (var (name, id) in names.Values) dictionary.Append(CultureInfo.InvariantCulture, $" /{name} {id} 0 R");
        return new PdfSignatureText(dictionary.Append(" >>").ToString(), operators);
    }

    private sealed class AppendedObjectSink(IPdfAppendedObjects objects) : IPdfObjectSink
    {
        public int Reserve() => objects.Reserve();

        public int WriteObject(string body)
        {
            var id = objects.Reserve();
            objects.Write(id, body);
            return id;
        }

        public void WriteObject(int id, string body) => objects.Write(id, body);

        public int WriteStream(byte[] payload, bool compress, string? extraDict) => objects.WriteStream(payload, extraDict);
    }
}
