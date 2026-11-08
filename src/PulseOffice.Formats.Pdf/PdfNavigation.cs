using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public sealed record PdfHeading(int Page, int Level, string Title, double Top);

public sealed record PdfDestination(int Page, string Name, double Top);

public sealed class PdfDocumentMarks
{
    private readonly List<PdfHeading> _headings = [];
    private readonly List<PdfDestination> _destinations = [];
    private readonly Dictionary<int, IReadOnlyList<PrintLink>> _links = [];
    private readonly Dictionary<int, IReadOnlyList<PrintMarkedNote>> _notes = [];

    public IReadOnlyList<PdfHeading> Headings => _headings;

    public IReadOnlyList<PdfDestination> Destinations => _destinations;

    public IReadOnlyList<PrintLink> PageLinks(int page) => _links.GetValueOrDefault(page, []);

    public IReadOnlyList<PrintMarkedNote> PageNotes(int page) => _notes.GetValueOrDefault(page, []);

    public void Collect(int page, PdfContentCanvas canvas, double pageHeight, PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Bookmarks)
            _headings.AddRange(canvas.Headings.Select(heading => new PdfHeading(page, heading.Level, heading.Title, pageHeight - heading.TopPoints)));
        if (options.Links)
        {
            _destinations.AddRange(canvas.Destinations.Select(target => new PdfDestination(page, target.Name, pageHeight - target.TopPoints)));
            _links[page] = canvas.Links;
        }
        if (options.Notes) _notes[page] = canvas.Notes;
    }
}

public interface IPdfNavigationWriter
{
    IReadOnlyList<int> Annotations(PdfFile file, IReadOnlyList<PrintLink> links, IReadOnlyList<PrintMarkedNote> notes, double pageHeight);

    int? Outline(PdfFile file, IReadOnlyList<PdfHeading> headings);

    int? Destinations(PdfFile file, IReadOnlyList<PdfDestination> destinations);
}

public sealed class PdfNavigationWriter : IPdfNavigationWriter
{
    private const double NoteSize = 16;

    public IReadOnlyList<int> Annotations(PdfFile file, IReadOnlyList<PrintLink> links, IReadOnlyList<PrintMarkedNote> notes, double pageHeight)
    {
        ArgumentNullException.ThrowIfNull(file);
        var ids = new List<int>();
        foreach (var link in links)
        {
            var action = link.Target.StartsWith('#')
                ? "/Dest " + PdfFile.TextString(link.Target[1..])
                : "/A << /S /URI /URI " + Ascii(link.Target) + " >>";
            ids.Add(file.WriteObject($"<< /Type /Annot /Subtype /Link /Rect {Rect(link.Area, pageHeight)} /Border [0 0 0] /F 4 {action} >>"));
        }
        if (notes.Count > 0)
        {
            var appearance = file.WriteStream(Encoding.ASCII.GetBytes("1 0.84 0.25 rg 0.6 0.45 0 RG 1 w 0.5 0.5 15 15 re B 0.6 0.45 0 rg 4 11 8 1 re f 4 8 8 1 re f 4 5 5 1 re f\n"), false,
                FormattableString.Invariant($"/Type /XObject /Subtype /Form /BBox [0 0 {NoteSize} {NoteSize}]"));
            foreach (var (area, note) in notes)
            {
                var left = area.Left.ToPoints();
                var top = pageHeight - area.Top.ToPoints();
                var rect = FormattableString.Invariant($"[{Number(left)} {Number(top - NoteSize)} {Number(left + NoteSize)} {Number(top)}]");
                var when = note.When is { } moment ? " /M " + PdfFile.DateString(moment) : string.Empty;
                ids.Add(file.WriteObject(
                    $"<< /Type /Annot /Subtype /Text /Rect {rect} /F 4 /Name /Comment /Contents {PdfFile.TextString(note.Text)} " +
                    $"/T {PdfFile.TextString(note.Author)}{when} /AP << /N {appearance} 0 R >> >>"));
            }
        }
        return ids;
    }

    public int? Outline(PdfFile file, IReadOnlyList<PdfHeading> headings)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (headings.Count == 0) return null;
        var root = new OutlineNode(null, 0, string.Empty, 0, 0);
        var stack = new Stack<OutlineNode>();
        stack.Push(root);
        foreach (var heading in headings)
        {
            while (stack.Peek() != root && stack.Peek().Level >= heading.Level) stack.Pop();
            var node = new OutlineNode(stack.Peek(), heading.Level, heading.Title, heading.Page, heading.Top);
            stack.Peek().Children.Add(node);
            stack.Push(node);
        }
        Assign(file, root);
        foreach (var node in Flatten(root).Skip(1))
        {
            var siblings = node.Parent!.Children;
            var position = siblings.IndexOf(node);
            var body = new StringBuilder($"<< /Title {PdfFile.TextString(node.Title)} /Parent {node.Parent.Id} 0 R");
            if (position > 0) body.Append(CultureInfo.InvariantCulture, $" /Prev {siblings[position - 1].Id} 0 R");
            if (position < siblings.Count - 1) body.Append(CultureInfo.InvariantCulture, $" /Next {siblings[position + 1].Id} 0 R");
            if (node.Children.Count > 0)
                body.Append(CultureInfo.InvariantCulture, $" /First {node.Children[0].Id} 0 R /Last {node.Children[^1].Id} 0 R /Count {(Open(node) ? 1 : -1) * Visible(node)}");
            body.Append(CultureInfo.InvariantCulture, $" /Dest [{node.Page} 0 R /XYZ 0 {Number(node.Top)} 0] >>");
            file.WriteObject(node.Id, body.ToString());
        }
        file.WriteObject(root.Id, FormattableString.Invariant(
            $"<< /Type /Outlines /First {root.Children[0].Id} 0 R /Last {root.Children[^1].Id} 0 R /Count {Visible(root)} >>"));
        return root.Id;
    }

    public int? Destinations(PdfFile file, IReadOnlyList<PdfDestination> destinations)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (destinations.Count == 0) return null;
        var entries = destinations
            .GroupBy(destination => destination.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(destination => (Key: PdfFile.TextString(destination.Name), Bytes: KeyBytes(destination.Name), Destination: destination))
            .OrderBy(entry => entry.Bytes, Comparer<byte[]>.Create((first, second) => first.AsSpan().SequenceCompareTo(second)))
            .ToList();
        var body = new StringBuilder("<< /Names [");
        foreach (var (key, _, destination) in entries)
            body.Append(CultureInfo.InvariantCulture, $"{key} [{destination.Page} 0 R /XYZ 0 {Number(destination.Top)} 0] ");
        body.Append("] >>");
        return file.WriteObject(body.ToString());
    }

    private void Assign(PdfFile file, OutlineNode node)
    {
        node.Id = file.Reserve();
        foreach (var child in node.Children) Assign(file, child);
    }

    private IEnumerable<OutlineNode> Flatten(OutlineNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Flatten(child)) yield return descendant;
    }

    private bool Open(OutlineNode node) => node.Level < 2;

    private int Visible(OutlineNode node) => node.Children.Sum(child => 1 + (Open(child) ? Visible(child) : 0));

    private byte[] KeyBytes(string name) =>
        name.All(character => character is >= ' ' and < '\u007F')
            ? Encoding.ASCII.GetBytes(name)
            : [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(name.Normalize(NormalizationForm.FormC))];

    private string Rect(DocumentRect area, double pageHeight) => FormattableString.Invariant(
        $"[{Number(area.Left.ToPoints())} {Number(pageHeight - area.Bottom.ToPoints())} {Number(area.Right.ToPoints())} {Number(pageHeight - area.Top.ToPoints())}]");

    private string Ascii(string uri)
    {
        var builder = new StringBuilder("(");
        foreach (var unit in Encoding.UTF8.GetBytes(uri))
        {
            if (unit is (byte)'(' or (byte)')' or (byte)'\\') builder.Append('\\').Append((char)unit);
            else if (unit is < 0x20 or > 0x7E) builder.Append('%').Append(unit.ToString("X2", CultureInfo.InvariantCulture));
            else builder.Append((char)unit);
        }
        return builder.Append(')').ToString();
    }

    private string Number(double value) =>
        (double.IsFinite(value) ? Math.Round(value, 2) : 0).ToString("0.##", CultureInfo.InvariantCulture);

    private sealed class OutlineNode(OutlineNode? parent, int level, string title, int page, double top)
    {
        public OutlineNode? Parent { get; } = parent;

        public int Level { get; } = level;

        public string Title { get; } = title;

        public int Page { get; } = page;

        public double Top { get; } = top;

        public int Id { get; set; }

        public List<OutlineNode> Children { get; } = [];
    }
}
