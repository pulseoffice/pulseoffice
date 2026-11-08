using DocumentFormat.OpenXml.Packaging;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxDynamicArrays
{
    private readonly SafeXmlLoader _xml = new();
    public const uint CellMetadataIndex = 1;

    private const string Metadata =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<metadata xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:xda=\"http://schemas.microsoft.com/office/spreadsheetml/2017/dynamicarray\">" +
        "<metadataTypes count=\"1\"><metadataType name=\"XLDAPR\" minSupportedVersion=\"120000\" copy=\"1\" pasteAll=\"1\" pasteValues=\"1\" merge=\"1\" splitFirst=\"1\" rowColShift=\"1\" clearFormats=\"1\" clearComments=\"1\" assign=\"1\" coerce=\"1\" cellMeta=\"1\"/></metadataTypes>" +
        "<futureMetadata name=\"XLDAPR\" count=\"1\"><bk><extLst><ext uri=\"{bdbb8cdc-fa1e-496e-a857-3c3f30c029c3}\"><xda:dynamicArrayProperties fDynamic=\"1\" fCollapsed=\"0\"/></ext></extLst></bk></futureMetadata>" +
        "<cellMetadata count=\"1\"><bk><rc t=\"1\" v=\"0\"/></bk></cellMetadata>" +
        "</metadata>";

    public void Write(WorkbookPart workbook)
    {
        var part = workbook.AddNewPart<CellMetadataPart>();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Metadata));
        part.FeedData(stream);
    }

    public IReadOnlySet<uint> DynamicCells(WorkbookPart workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var dynamic = new HashSet<uint>();
        if (workbook.CellMetadataPart is not { } part) return dynamic;
        System.Xml.Linq.XDocument document;
        try
        {
            using var stream = part.GetStream();
            document = _xml.Document(stream);
        }
        catch (System.Xml.XmlException)
        {
            return dynamic;
        }
        if (document.Root is not { } root) return dynamic;

        var types = Children(root, "metadataTypes", "metadataType").Select(type => (string?)type.Attribute("name")).ToList();
        var flags = Children(root, "futureMetadata", null)
            .Where(future => (string?)future.Attribute("name") == TypeName)
            .SelectMany(future => future.Elements().Where(element => element.Name.LocalName == "bk"))
            .Select(block => block.Descendants().FirstOrDefault(element => element.Name.LocalName == "dynamicArrayProperties")
                ?.Attribute("fDynamic")?.Value is "1" or "true")
            .ToList();

        var index = 0u;
        foreach (var block in Children(root, "cellMetadata", "bk"))
        {
            index++;
            var record = block.Elements().FirstOrDefault(element => element.Name.LocalName == "rc");
            if (!int.TryParse((string?)record?.Attribute("t"), out var type) || !int.TryParse((string?)record?.Attribute("v"), out var value)) continue;
            if (type < 1 || type > types.Count || types[type - 1] != TypeName) continue;
            if (value >= 0 && value < flags.Count && flags[value]) dynamic.Add(index);
        }
        return dynamic;
    }

    private const string TypeName = "XLDAPR";

    private IEnumerable<System.Xml.Linq.XElement> Children(System.Xml.Linq.XElement root, string container, string? child) =>
        root.Elements().Where(element => element.Name.LocalName == container)
            .SelectMany(element => child is null ? [element] : element.Elements().Where(inner => inner.Name.LocalName == child));
}
