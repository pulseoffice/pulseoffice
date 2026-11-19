using System.Globalization;
using System.Text;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public sealed class PdfStructureElement(PrintTag tag, PdfStructureElement? parent)
{
    public PrintTag Tag { get; } = tag;

    public PdfStructureElement? Parent { get; } = parent;

    public List<object> Kids { get; } = [];

    public int Id { get; set; }

    public PdfBox? Bounds { get; private set; }

    public void Extend(int pageSlot, PdfBox area)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (Bounds is null) Bounds = area;
        else if (Bounds.PageSlot == pageSlot) Bounds = Bounds.Union(area);
        else SpansPages = true;
    }

    public bool SpansPages { get; private set; }
}

public sealed record PdfBox(int PageSlot, double Left, double Bottom, double Right, double Top)
{
    public PdfBox Union(PdfBox other) => this with
    {
        Left = Math.Min(Left, other.Left),
        Bottom = Math.Min(Bottom, other.Bottom),
        Right = Math.Max(Right, other.Right),
        Top = Math.Max(Top, other.Top),
    };

    public double Size => Math.Max(0, Right - Left) * Math.Max(0, Top - Bottom);

    public double Overlap(PdfBox other) =>
        Math.Max(0, Math.Min(Right, other.Right) - Math.Max(Left, other.Left)) * Math.Max(0, Math.Min(Top, other.Top) - Math.Max(Bottom, other.Bottom));
}

public sealed record PdfMarkedReference(int PageSlot, int Mcid);

public sealed record PdfAnnotationReference(int PageSlot, int ObjectId);

public sealed class PdfStructureTree
{
    private readonly Dictionary<string, PdfStructureElement> _elements = new(StringComparer.Ordinal);
    private readonly List<PdfStructureElement> _roots = [];
    private readonly List<List<PdfStructureElement>> _pages = [];
    private readonly List<PdfStructureElement> _annotations = [];
    private readonly Dictionary<(int PageSlot, string Target), PdfStructureElement> _links = [];
    private readonly Dictionary<int, Dictionary<PdfStructureElement, PdfBox>> _covered = [];
    private readonly Dictionary<int, double> _heights = [];

    public int AnnotationKeyBase { get; init; }

    public IReadOnlyList<PdfStructureElement> Annotations => _annotations;

    public int NextKey => Math.Max(_pages.Count, AnnotationKeyBase + _annotations.Count);

    public IReadOnlyList<PdfStructureElement> Roots => _roots;

    public IEnumerable<PdfStructureElement> Elements => _elements.Values;

    public IReadOnlyList<IReadOnlyList<PdfStructureElement>> Pages => _pages;

    public bool HasContent => _pages.Any(page => page.Count > 0) || _annotations.Count > 0;

    public bool HasPageContent(int pageSlot) =>
        _pages[pageSlot].Count > 0 || _annotations.SelectMany(element => element.Kids.OfType<PdfAnnotationReference>()).Any(kid => kid.PageSlot == pageSlot);

    public PdfStructureElement? LinkAt(int pageSlot, string target) => _links.GetValueOrDefault((pageSlot, target));

    public void Measure(int pageSlot, double heightPoints) => _heights[pageSlot] = heightPoints;

    public double? HeightOf(int pageSlot) => _heights.TryGetValue(pageSlot, out var height) ? height : null;

    public void Cover(PdfStructureElement element, int pageSlot, PdfBox area)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(area);
        if (!_covered.TryGetValue(pageSlot, out var boxes)) _covered[pageSlot] = boxes = [];
        for (var covering = element; covering is not null; covering = covering.Parent)
            boxes[covering] = boxes.TryGetValue(covering, out var box) ? box.Union(area) : area;
    }

    public PdfStructureElement? CoveringAt(int pageSlot, PdfBox area)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (!_covered.TryGetValue(pageSlot, out var boxes)) return null;
        return boxes.Where(entry => entry.Key.Tag.Role is not (PrintRole.Link or PrintRole.Annotation or PrintRole.Span))
            .Select(entry => (entry.Key, Shared: entry.Value.Overlap(area), entry.Value.Size))
            .Where(entry => entry.Shared > 0 && entry.Size > 0)
            .OrderByDescending(entry => Math.Round(entry.Shared / entry.Size, 2))
            .ThenByDescending(entry => entry.Shared)
            .Select(entry => entry.Key)
            .FirstOrDefault();
    }

    public int Annotate(PdfStructureElement element, int pageSlot, int objectId)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Kids.Add(new PdfAnnotationReference(pageSlot, objectId));
        _annotations.Add(element);
        return AnnotationKeyBase + _annotations.Count - 1;
    }

    public int AddPage()
    {
        _pages.Add([]);
        return _pages.Count - 1;
    }

    public PdfStructureElement Declare(PrintTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        PdfStructureElement? parent = null;
        foreach (var link in tag.Lineage())
        {
            if (!_elements.TryGetValue(link.Key, out var element))
            {
                element = new PdfStructureElement(link, parent);
                _elements[link.Key] = element;
                if (parent is null) _roots.Add(element);
                else parent.Kids.Add(element);
            }
            parent = element;
        }
        return parent!;
    }

    public int Mark(PdfStructureElement element, int pageSlot)
    {
        ArgumentNullException.ThrowIfNull(element);
        var page = _pages[pageSlot];
        page.Add(element);
        var mcid = page.Count - 1;
        element.Kids.Add(new PdfMarkedReference(pageSlot, mcid));
        if (element.Tag.Target is { Length: > 0 } target) _links.TryAdd((pageSlot, target), element);
        return mcid;
    }
}

public sealed class PdfMarkedContent(PdfStructureTree tree, int pageSlot, IPdfStructureRoles roles)
{
    private PdfStructureElement? _current;
    private PdfStructureElement? _open;
    private bool _artifactOpen;
    private int _artifactDepth;

    public void Tag(PrintTag? tag) => _current = tag is null ? null : tree.Declare(tag);

    public void Measure(double pageHeightPoints) => tree.Measure(pageSlot, pageHeightPoints);

    public void BeginArtifact() => _artifactDepth++;

    public void EndArtifact() => _artifactDepth = Math.Max(0, _artifactDepth - 1);

    public void Enter(StringBuilder content, PdfBox? area = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var box = area is null ? null : area with { PageSlot = pageSlot };
        if (_current is null || _artifactDepth > 0)
        {
            if (box is not null && _current is not null) TableOf(_current)?.Extend(pageSlot, box);
            if (_artifactOpen) return;
            Close(content);
            content.Append("/Artifact BMC\n");
            _artifactOpen = true;
            return;
        }
        if (box is not null)
        {
            Bounded(_current)?.Extend(pageSlot, box);
            tree.Cover(_current, pageSlot, box);
        }
        if (ReferenceEquals(_open, _current)) return;
        Close(content);
        var mcid = tree.Mark(_current, pageSlot);
        content.Append(CultureInfo.InvariantCulture, $"/{roles.Name(_current.Tag)} <</MCID {mcid}>> BDC\n");
        _open = _current;
    }

    private static PdfStructureElement? Bounded(PdfStructureElement element)
    {
        for (var bounded = element; bounded is not null; bounded = bounded.Parent)
            if (bounded.Tag.Role is PrintRole.Figure or PrintRole.Table) return bounded;
        return null;
    }

    private static PdfStructureElement? TableOf(PdfStructureElement element) =>
        Bounded(element) is { Tag.Role: PrintRole.Table } table ? table : null;

    public void Close(StringBuilder content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_open is null && !_artifactOpen) return;
        content.Append("EMC\n");
        _open = null;
        _artifactOpen = false;
    }
}

public interface IPdfStructureRoles
{
    string Name(PrintTag tag);

    IReadOnlyDictionary<string, string> RoleMap { get; }
}

public sealed class PdfStructureRoles : IPdfStructureRoles
{
    private const int StandardHeadingLevels = 6;

    private const int DeepestHeading = 10;

    public IReadOnlyDictionary<string, string> RoleMap { get; } = BuildRoleMap();

    public string Name(PrintTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag.Role switch
        {
            PrintRole.Sheet => "Sheet",
            PrintRole.Slide => "Slide",
            PrintRole.Title => "Title",
            PrintRole.Paragraph => "P",
            PrintRole.Heading => "H" + Math.Clamp(tag.Level, 1, DeepestHeading).ToString(CultureInfo.InvariantCulture),
            PrintRole.List => "L",
            PrintRole.ListItem => "LI",
            PrintRole.ListLabel => "Lbl",
            PrintRole.ListBody => "LBody",
            PrintRole.Table => "Table",
            PrintRole.TableRow => "TR",
            PrintRole.TableHeader => "TH",
            PrintRole.TableCell => "TD",
            PrintRole.Figure => "Figure",
            PrintRole.Link => "Link",
            PrintRole.Note => "Note",
            PrintRole.Span => "Span",
            PrintRole.Annotation => "Annot",
            PrintRole.Form => "Form",
            _ => "Sect",
        };
    }

    private static Dictionary<string, string> BuildRoleMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Sheet"] = "Sect",
            ["Slide"] = "Sect",
            ["Title"] = "H1",
        };
        for (var level = StandardHeadingLevels + 1; level <= DeepestHeading; level++)
            map["H" + level.ToString(CultureInfo.InvariantCulture)] = "H6";
        return map;
    }
}

public interface IPdfStructureWriter
{
    PdfMarkedContent Marker(PdfStructureTree tree, int pageSlot);

    string PageEntries(bool tagged, int pageSlot);

    IPdfAnnotationTagging Annotations(PdfStructureTree tree, int pageSlot);

    int? Write(IPdfObjectSink file, PdfStructureTree tree, IReadOnlyList<int> pageIds);
}

public sealed class PdfStructureWriter(IPdfStructureRoles roles) : IPdfStructureWriter
{
    public PdfStructureWriter() : this(new PdfStructureRoles())
    {
    }

    public PdfMarkedContent Marker(PdfStructureTree tree, int pageSlot) => new(tree, pageSlot, roles);

    public IPdfAnnotationTagging Annotations(PdfStructureTree tree, int pageSlot) => new PdfAnnotationTagging(tree, pageSlot);

    public string PageEntries(bool tagged, int pageSlot) =>
        tagged ? string.Create(CultureInfo.InvariantCulture, $" /StructParents {pageSlot} /Tabs /S") : string.Empty;

    public int? Write(IPdfObjectSink file, PdfStructureTree tree, IReadOnlyList<int> pageIds)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(pageIds);
        if (!tree.HasContent) return null;

        var rootId = file.Reserve();
        var documentId = file.Reserve();
        foreach (var element in tree.Elements) element.Id = file.Reserve();

        foreach (var element in tree.Elements)
            file.WriteObject(element.Id, Element(element, element.Parent?.Id ?? documentId, pageIds));

        var top = string.Join(' ', tree.Roots.Select(element => $"{element.Id} 0 R"));
        file.WriteObject(documentId, $"<< /Type /StructElem /S /Document /P {rootId} 0 R /K [{top}] >>");

        var parentTree = new StringBuilder("<< /Nums [");
        for (var slot = 0; slot < tree.Pages.Count; slot++)
            parentTree.Append(CultureInfo.InvariantCulture, $"{slot} [")
                .Append(string.Join(' ', tree.Pages[slot].Select(element => $"{element.Id} 0 R")))
                .Append("] ");
        for (var index = 0; index < tree.Annotations.Count; index++)
            parentTree.Append(CultureInfo.InvariantCulture, $"{tree.AnnotationKeyBase + index} {tree.Annotations[index].Id} 0 R ");
        parentTree.Append("] >>");
        var parentTreeId = file.WriteObject(parentTree.ToString());

        var roleMap = string.Join(' ', roles.RoleMap.Select(entry => $"/{entry.Key} /{entry.Value}"));
        file.WriteObject(rootId,
            $"<< /Type /StructTreeRoot /K [{documentId} 0 R] /ParentTree {parentTreeId} 0 R " +
            $"/ParentTreeNextKey {tree.NextKey} /RoleMap << {roleMap} >> >>");
        return rootId;
    }

    private string Element(PdfStructureElement element, int parentId, IReadOnlyList<int> pageIds)
    {
        var kids = element.Kids.Select(kid => kid switch
        {
            PdfStructureElement child => $"{child.Id} 0 R",
            PdfMarkedReference marked => $"<< /Type /MCR /Pg {pageIds[marked.PageSlot]} 0 R /MCID {marked.Mcid} >>",
            PdfAnnotationReference annotation => $"<< /Type /OBJR /Pg {pageIds[annotation.PageSlot]} 0 R /Obj {annotation.ObjectId} 0 R >>",
            _ => string.Empty,
        });
        var body = new StringBuilder($"<< /Type /StructElem /S /{roles.Name(element.Tag)} /P {parentId} 0 R /K [{string.Join(' ', kids)}]");
        if (FirstPage(element) is { } first)
            body.Append(CultureInfo.InvariantCulture, $" /Pg {pageIds[first]} 0 R");
        if (!string.IsNullOrWhiteSpace(element.Tag.Alternate))
            body.Append(" /Alt ").Append(PdfFile.TextString(element.Tag.Alternate.Trim()));
        if (element.Tag.ActualText is { } actual)
            body.Append(" /ActualText ").Append(PdfFile.TextString(actual));
        if (!string.IsNullOrWhiteSpace(element.Tag.Language))
            body.Append(" /Lang ").Append(PdfFile.TextString(element.Tag.Language));
        if (Attributes(element) is { Length: > 0 } attributes) body.Append(" /A ").Append(attributes);
        return body.Append(" >>").ToString();
    }

    private static int? FirstPage(PdfStructureElement element) =>
        element.Kids.Select(kid => kid switch
        {
            PdfMarkedReference marked => marked.PageSlot,
            PdfAnnotationReference annotation => annotation.PageSlot,
            _ => (int?)null,
        }).FirstOrDefault(slot => slot is not null);

    private static string Attributes(PdfStructureElement element)
    {
        var tag = element.Tag;
        var table = new StringBuilder();
        if (tag.Role == PrintRole.TableHeader)
            table.Append(" /Scope /").Append(tag.Scope switch { PrintHeaderScope.Row => "Row", PrintHeaderScope.Both => "Both", _ => "Column" });
        if (tag.Role is PrintRole.TableHeader or PrintRole.TableCell)
        {
            if (tag.ColumnSpan > 1) table.Append(CultureInfo.InvariantCulture, $" /ColSpan {tag.ColumnSpan}");
            if (tag.RowSpan > 1) table.Append(CultureInfo.InvariantCulture, $" /RowSpan {tag.RowSpan}");
        }
        var dictionaries = new List<string>();
        if (table.Length > 0) dictionaries.Add("<< /O /Table" + table + " >>");
        if (tag.Role is PrintRole.Figure or PrintRole.Table && element.Bounds is { } box && !(tag.Role == PrintRole.Table && element.SpansPages))
            dictionaries.Add(string.Create(CultureInfo.InvariantCulture,
                $"<< /O /Layout /BBox [{Number(box.Left)} {Number(box.Bottom)} {Number(box.Right)} {Number(box.Top)}] >>"));
        return dictionaries.Count switch
        {
            0 => string.Empty,
            1 => dictionaries[0],
            _ => "[" + string.Join(' ', dictionaries) + "]",
        };
    }

    private static string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}

public interface IPdfAnnotationTagging
{
    string Link(int objectId, PrintLink link);

    string Note(int objectId, PrintMarkedNote note);

    string Field(int objectId, PrintMarkedField field);
}

public sealed class PdfAnnotationTagging(PdfStructureTree tree, int pageSlot) : IPdfAnnotationTagging
{
    public string Link(int objectId, PrintLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var key = FormattableString.Invariant($"annotation:link:{objectId}");
        var element = tree.LinkAt(pageSlot, link.Target)
            ?? tree.Declare(new PrintTag(key, PrintRole.Link, Covering(link)?.Tag) { Alternate = link.Target });
        return Entry(element, objectId) + " /Contents " + PdfFile.TextString(link.Target);
    }

    public string Note(int objectId, PrintMarkedNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return Entry(tree.Declare(new PrintTag(FormattableString.Invariant($"annotation:note:{objectId}"), PrintRole.Annotation)), objectId);
    }

    private PdfStructureElement? Covering(PrintLink link) => Covering(link.Area);

    private PdfStructureElement? Covering(DocumentRect rect)
    {
        if (tree.HeightOf(pageSlot) is not { } height) return null;
        var area = new PdfBox(pageSlot, rect.Left.ToPoints(), height - rect.Bottom.ToPoints(), rect.Right.ToPoints(), height - rect.Top.ToPoints());
        return tree.CoveringAt(pageSlot, area);
    }

    public string Field(int objectId, PrintMarkedField marked)
    {
        ArgumentNullException.ThrowIfNull(marked);
        var field = marked.Field;
        var tag = new PrintTag(FormattableString.Invariant($"annotation:form:{objectId}"), PrintRole.Form, Covering(marked.Area)?.Tag)
        {
            Alternate = string.IsNullOrWhiteSpace(field.Tooltip) ? field.Name : field.Tooltip,
        };
        return Entry(tree.Declare(tag), objectId);
    }

    private string Entry(PdfStructureElement element, int objectId) =>
        string.Create(CultureInfo.InvariantCulture, $" /StructParent {tree.Annotate(element, pageSlot, objectId)}");
}
