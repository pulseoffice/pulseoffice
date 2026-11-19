using System.Globalization;
using System.Text;
using PulseOffice.Printing;

namespace PulseOffice.Formats.Pdf;

public sealed class PdfStructureElement(PrintTag tag, PdfStructureElement? parent)
{
    public PrintTag Tag { get; } = tag;

    public PdfStructureElement? Parent { get; } = parent;

    public List<object> Kids { get; } = [];

    public int Id { get; set; }
}

public sealed record PdfMarkedReference(int PageSlot, int Mcid);

public sealed class PdfStructureTree
{
    private readonly Dictionary<string, PdfStructureElement> _elements = new(StringComparer.Ordinal);
    private readonly List<PdfStructureElement> _roots = [];
    private readonly List<List<PdfStructureElement>> _pages = [];

    public IReadOnlyList<PdfStructureElement> Roots => _roots;

    public IEnumerable<PdfStructureElement> Elements => _elements.Values;

    public IReadOnlyList<IReadOnlyList<PdfStructureElement>> Pages => _pages;

    public bool HasContent => _pages.Any(page => page.Count > 0);

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
        return mcid;
    }
}

public sealed class PdfMarkedContent(PdfStructureTree tree, int pageSlot, IPdfStructureRoles roles)
{
    private PdfStructureElement? _current;
    private PdfStructureElement? _open;
    private bool _artifactOpen;

    public void Tag(PrintTag? tag) => _current = tag is null ? null : tree.Declare(tag);

    public void Enter(StringBuilder content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_current is null)
        {
            if (_artifactOpen) return;
            Close(content);
            content.Append("/Artifact BMC\n");
            _artifactOpen = true;
            return;
        }
        if (ReferenceEquals(_open, _current)) return;
        Close(content);
        var mcid = tree.Mark(_current, pageSlot);
        content.Append(CultureInfo.InvariantCulture, $"/{roles.Name(_current.Tag)} <</MCID {mcid}>> BDC\n");
        _open = _current;
    }

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

    int? Write(IPdfObjectSink file, PdfStructureTree tree, IReadOnlyList<int> pageIds);
}

public sealed class PdfStructureWriter(IPdfStructureRoles roles) : IPdfStructureWriter
{
    public PdfStructureWriter() : this(new PdfStructureRoles())
    {
    }

    public PdfMarkedContent Marker(PdfStructureTree tree, int pageSlot) => new(tree, pageSlot, roles);

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
        parentTree.Append("] >>");
        var parentTreeId = file.WriteObject(parentTree.ToString());

        var roleMap = string.Join(' ', roles.RoleMap.Select(entry => $"/{entry.Key} /{entry.Value}"));
        file.WriteObject(rootId,
            $"<< /Type /StructTreeRoot /K [{documentId} 0 R] /ParentTree {parentTreeId} 0 R " +
            $"/ParentTreeNextKey {tree.Pages.Count} /RoleMap << {roleMap} >> >>");
        return rootId;
    }

    private string Element(PdfStructureElement element, int parentId, IReadOnlyList<int> pageIds)
    {
        var kids = element.Kids.Select(kid => kid switch
        {
            PdfStructureElement child => $"{child.Id} 0 R",
            PdfMarkedReference marked => $"<< /Type /MCR /Pg {pageIds[marked.PageSlot]} 0 R /MCID {marked.Mcid} >>",
            _ => string.Empty,
        });
        var body = new StringBuilder($"<< /Type /StructElem /S /{roles.Name(element.Tag)} /P {parentId} 0 R /K [{string.Join(' ', kids)}]");
        if (element.Kids.OfType<PdfMarkedReference>().FirstOrDefault() is { } first)
            body.Append(CultureInfo.InvariantCulture, $" /Pg {pageIds[first.PageSlot]} 0 R");
        if (!string.IsNullOrWhiteSpace(element.Tag.Alternate))
            body.Append(" /Alt ").Append(PdfFile.TextString(element.Tag.Alternate.Trim()));
        if (!string.IsNullOrWhiteSpace(element.Tag.Language))
            body.Append(" /Lang ").Append(PdfFile.TextString(element.Tag.Language));
        if (element.Tag.Role == PrintRole.TableHeader) body.Append(" /A << /O /Table /Scope /Column >>");
        return body.Append(" >>").ToString();
    }
}
